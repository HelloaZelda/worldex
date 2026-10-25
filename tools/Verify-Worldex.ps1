param([switch]$Live)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$configPath = Join-Path $root '.runtime\run.json'
$settings = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
$sources=Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs'|Sort-Object Name
$text=($sources|ForEach-Object {Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8}) -join "`n"
$sha=[Security.Cryptography.SHA256]::Create()
$hash=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)))).Replace('-','').Substring(0,16)
$sha.Dispose()
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
        foreach ($index in 0..17) {
            $frame = $visual.Draw($index,$size[0],$size[1],$time,$false)
            if ($frame.Cells.Length -ne $size[0]*$size[1]) { throw 'Frame dimensions mismatch' }
            $box = $visual.Position($index,$time)
            if ($box.x -lt 0 -or $box.y -lt 0 -or ($box.x+$box.w) -gt 1.01 -or ($box.y+$box.h) -gt 1.01) { throw 'Window outside working area' }
            $frames++
        }
    }
}
Write-Host ('PASS: ' + $frames + ' frames across '+$visual.Scenes.Length+' scenes, eighteen actors, three viewport sizes.')
$captionChecks=0
foreach($line in $visual.Lyrics.lines) {
    $moment=$line.start-$config.CueOffset+[Math]::Min(.03,($line.end-$line.start)*.5)
    $caption=$visual.Draw(7,120,12,$moment,$false)
    $visible=(-join ($caption.Cells|ForEach-Object {$_.Char})) -replace '\s',''
    $expected=$line.text -replace '\s',''
    if(-not $visible.Contains($expected)){throw ('Lyric missing from caption window at '+$line.start)}
    $captionChecks++
}
$impacts=0
foreach($event in $visual.Rhythm.events) {
    if($event.strength -lt .65){continue}
    $channel=@{kick=0;snare=1;hat=2}[$event.kind]
    $before=$visual.Pulse($event.time-.04,$channel)
    $after=$visual.Pulse($event.time+.022,$channel)
    if($after-$before -gt .25){$impacts++}
}
if($impacts -lt 20){throw 'Audio impacts are not arriving at their measured timestamps.'}
Write-Host ('PASS: '+$captionChecks+' complete sung lines visible; '+$impacts+' measured attacks produce correctly timed pulse rises.')
$graphicActors=@(0,1,2,3,4,5,6,9,10,11,12,13,14,15,16,17)
$poses=@()
foreach($index in $graphicActors) {
    $t=67.25-$config.CueOffset
    if(-not $visual.FormationVisible($index,$t)){throw 'Heart tile unexpectedly hidden.'}
    $a=$visual.Position($index,$t);$b=$visual.Position($index,$t+.1)
    if([Math]::Abs($a.x-$b.x)+[Math]::Abs($a.y-$b.y) -gt .00001){throw 'Heart formation drifts while it should hold.'}
    $poses+=[pscustomobject]@{Row=[Math]::Round($a.y,3);X=$a.x}
}
$rowCounts=($poses|Group-Object Row|Sort-Object {[double]$_.Name}|ForEach-Object {$_.Count}) -join ','
if($rowCounts -ne '2,5,5,3,1'){throw ('Invalid desktop heart silhouette: '+$rowCounts)}
Write-Host 'PASS: sixteen real-window positions form a stable heart with rows 2/5/5/3/1.'
if ($Live) {
    $memory = [System.IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting('Local\Worldex.' + $settings.Run)
    $map = $memory.CreateViewAccessor()
    try {
        foreach ($index in 0..17) { if ($map.ReadInt32(64+$index*4) -ne 1) { throw ('Panel not ready: ' + $index) } }
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
        Write-Host ('PASS: eighteen workers ready; pause drift={0:0.000}s, seek={1:0.000}s; resume advances.' -f ($b-$a),($c-$b))
    } finally { $map.Dispose();$memory.Dispose() }
}
