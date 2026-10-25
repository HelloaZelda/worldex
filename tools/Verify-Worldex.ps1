param([switch]$Live)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$configPath = Join-Path $root '.runtime\run.json'
$settings = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
$hash = (Get-FileHash -LiteralPath (Join-Path $root 'src\Worldex.Engine.cs') -Algorithm SHA256).Hash.Substring(0,16)
Add-Type -AssemblyName System.Windows.Forms,System.Drawing,System.Web.Extensions
[Reflection.Assembly]::LoadFrom((Join-Path $root ('.runtime\Worldex.' + $hash + '.dll'))) | Out-Null
$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$config = $serializer.Deserialize((Get-Content -LiteralPath $configPath -Raw -Encoding UTF8), [Worldex.Settings])
$visual = New-Object Worldex.Visual($config)
$frames = 0
for ($scene=0; $scene -lt $visual.Scenes.Length; $scene++) {
    $cue = $visual.Scenes[$scene]
    $time = $cue.time - $config.CueOffset + 0.02
    if ($visual.SceneAt($time) -ne $scene) { throw ('Scene lookup mismatch: ' + $scene) }
    foreach ($size in @(@(24,10),@(52,18),@(90,42))) {
        foreach ($index in 0..6) {
            $frame = $visual.Draw($index,$size[0],$size[1],$time,$false)
            if ($frame.Cells.Length -ne $size[0]*$size[1]) { throw 'Frame dimensions mismatch' }
            $box = $visual.Position($index,$time)
            if ($box.x -lt 0 -or $box.y -lt 0 -or ($box.x+$box.w) -gt 1.01 -or ($box.y+$box.h) -gt 1.01) { throw 'Window outside working area' }
            $frames++
        }
    }
}
Write-Host ('PASS: ' + $frames + ' frames across all 32 scenes, seven roles, three viewport sizes.')
if ($Live) {
    $memory = [System.IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting('Local\Worldex.' + $settings.Run)
    $map = $memory.CreateViewAccessor()
    try {
        foreach ($index in 0..6) { if ($map.ReadInt32(64+$index*4) -ne 1) { throw ('Panel not ready: ' + $index) } }
        if ($map.ReadInt32(12) -ne 0) { throw 'Live test requires playing state.' }
        $map.Write(16,[int]1)
        Start-Sleep -Milliseconds 350
        $a=$map.ReadDouble(0)
        Start-Sleep -Milliseconds 700
        $b=$map.ReadDouble(0)
        if ([Math]::Abs($b-$a) -gt 0.06 -or $map.ReadInt32(12) -ne 1) { throw 'Pause did not freeze the audio master clock.' }
        $map.Write(16,[int]3)
        Start-Sleep -Milliseconds 350
        $c=$map.ReadDouble(0)
        if ([Math]::Abs(($c-$b)-10) -gt 0.15) { throw 'Seeking did not move the audio master clock by ten seconds.' }
        $map.Write(16,[int]1)
        Start-Sleep -Milliseconds 700
        if ($map.ReadDouble(0) -lt $c+0.3) { throw 'Resume did not restart the audio clock.' }
        Write-Host ('PASS: seven workers ready; pause drift={0:0.000}s, seek={1:0.000}s; resume advances.' -f ($b-$a),($c-$b))
    } finally { $map.Dispose();$memory.Dispose() }
}
