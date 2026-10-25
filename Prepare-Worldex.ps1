param([string]$Config)
$ErrorActionPreference = 'Stop'
$settings = Get-Content -LiteralPath $Config -Raw -Encoding UTF8 | ConvertFrom-Json
$runtime = Join-Path $settings.Root '.runtime'
$stop = Join-Path $runtime ($settings.Run + '.stop')
$status = Join-Path $runtime ($settings.Run + '-status.json')
function Set-Stage([string]$stage) { @{Stage=$stage;PID=$PID;Updated=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath $status -Encoding UTF8 }
try {
    Set-Stage 'analyzing'
    $env:PATH = (Split-Path -Parent $settings.Ffmpeg) + ';' + $env:PATH
    & $settings.Python (Join-Path $settings.Root 'tools\analyze_audio.py') $settings.Source --output $settings.Cache 2>&1 | Out-File -LiteralPath (Join-Path $runtime ($settings.Run + '-analysis.log')) -Encoding UTF8
    if ($LASTEXITCODE -ne 0) { throw 'Audio analysis failed; see analysis log.' }
    Set-Stage 'aligning-lyrics'
    $lyricReady = $false
    if (Test-Path -LiteralPath $settings.Lyrics) {
        $existing = Get-Content -LiteralPath $settings.Lyrics -Raw -Encoding UTF8 | ConvertFrom-Json
        $lyricReady = $existing.version -eq 3
    }
    if (-not $lyricReady) {
        & $settings.Python (Join-Path $settings.Root 'tools\align_lyrics.py') --cache $settings.Cache 2>&1 | Out-File -LiteralPath (Join-Path $runtime ($settings.Run + '-lyrics.log')) -Encoding UTF8
        if ($LASTEXITCODE -ne 0) { throw 'Lyric cue processing failed; see lyrics log.' }
    }
    & $settings.Python (Join-Path $settings.Root 'tools\build_performance.py') --cache $settings.Cache --root $settings.Root 2>&1 | Out-File -LiteralPath (Join-Path $runtime ($settings.Run + '-score.log')) -Encoding UTF8
    if ($LASTEXITCODE -ne 0) { throw 'Performance score build failed.' }
    if (Test-Path -LiteralPath $stop) { Set-Stage 'stopped'; exit }
    $analysis = Get-Content -LiteralPath (Join-Path $settings.Cache 'audio-analysis.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $settings | Add-Member Duration $analysis.duration -Force
    $settings | Add-Member Bpm $analysis.bpm -Force
    $settings | Add-Member BeatOffset $analysis.beatOffset -Force
    if ($settings.Seek -ge $settings.Duration) { throw 'SeekSeconds is beyond the track duration.' }
    $settings | ConvertTo-Json | Set-Content -LiteralPath $Config -Encoding UTF8
    $current = Get-Content -LiteralPath (Join-Path $runtime 'run.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($current.Run -ne $settings.Run) { Set-Stage 'superseded'; exit }
    Copy-Item -LiteralPath $Config -Destination (Join-Path $runtime 'run.json') -Force
    Set-Stage 'compiling'
    $sources = Get-ChildItem -LiteralPath (Join-Path $settings.Root 'src') -Filter '*.cs' | Sort-Object Name
    $sourceText = ($sources | ForEach-Object {Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8}) -join "`n"
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $hash = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($sourceText)))).Replace('-','').Substring(0,16)
    $sha.Dispose()
    $assembly = Join-Path $runtime ('Worldex.' + $hash + '.dll')
    Add-Type -AssemblyName System.Windows.Forms, System.Drawing, System.Web.Extensions
    if (-not (Test-Path -LiteralPath $assembly)) {
        Add-Type -TypeDefinition $sourceText -OutputAssembly $assembly -ReferencedAssemblies System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Web.Extensions.dll
    }
    # Allow Windows to complete its inspection of a newly emitted assembly.
    $loaded=$false
    foreach($attempt in 1..12) {
        try { [Reflection.Assembly]::LoadFrom($assembly) | Out-Null;$loaded=$true;break }
        catch [System.IO.FileLoadException] { if($attempt -eq 12){throw};Start-Sleep -Milliseconds 500 }
    }
    if(-not $loaded){throw 'Renderer assembly could not be loaded.'}
    if ($settings.RenderOnly) {
        Set-Stage 'rendering'
        [Worldex.Engine]::Preview($Config, (Join-Path $runtime 'previews'))
        Set-Stage 'rendered'
        exit
    }
    if (Test-Path -LiteralPath $stop) { Set-Stage 'stopped'; exit }
    Set-Stage 'opening-panels'
    $panelStartup=New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]0}
    foreach ($index in 0..17) {
        $arguments = '"{0}" -NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File "{1}" -Index {2} -Config "{3}" -Assembly "{4}"' -f "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe", (Join-Path $settings.Root 'Terminal-Panel.ps1'), $index, $Config, $assembly
        if ($index % 2 -eq 1) { $arguments = '"{0}" /d /s /c "{1}"' -f "$env:SystemRoot\System32\cmd.exe", $arguments }
        $command = '"{0}" {1}' -f "$env:SystemRoot\System32\conhost.exe", $arguments
        $result = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=$command;CurrentDirectory=$settings.Root;ProcessStartupInformation=$panelStartup}
        if ($result.ReturnValue -ne 0) { throw ('Unable to create panel ' + $index) }
    }
    Set-Stage 'waiting-for-terminals'
    [Worldex.Engine]::Director($Config)
    if (Test-Path -LiteralPath (Join-Path $runtime ($settings.Run + '-director.log'))) { throw 'Director failed; see director log.' }
    Set-Stage 'finished'
} catch {
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $runtime ($settings.Run + '-prepare.log')) -Encoding UTF8
    New-Item -ItemType File -Path $stop -Force | Out-Null
    Set-Stage 'error'
}
