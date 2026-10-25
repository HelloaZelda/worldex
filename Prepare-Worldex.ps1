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
    $source = Join-Path $settings.Root 'src\Worldex.Engine.cs'
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.Substring(0,16)
    $assembly = Join-Path $runtime ('Worldex.' + $hash + '.dll')
    Add-Type -AssemblyName System.Windows.Forms, System.Drawing, System.Web.Extensions
    if (-not (Test-Path -LiteralPath $assembly)) {
        Add-Type -TypeDefinition (Get-Content -LiteralPath $source -Raw -Encoding UTF8) -OutputAssembly $assembly -ReferencedAssemblies System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Web.Extensions.dll
    }
    [Reflection.Assembly]::LoadFrom($assembly) | Out-Null
    if ($settings.RenderOnly) {
        Set-Stage 'rendering'
        [Worldex.Engine]::Preview($Config, (Join-Path $runtime 'previews'))
        Set-Stage 'rendered'
        exit
    }
    if (Test-Path -LiteralPath $stop) { Set-Stage 'stopped'; exit }
    Set-Stage 'opening-panels'
    foreach ($index in 0..6) {
        $arguments = '"{0}" -NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File "{1}" -Index {2} -Config "{3}" -Assembly "{4}"' -f "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe", (Join-Path $settings.Root 'Terminal-Panel.ps1'), $index, $Config, $assembly
        if ($index % 2 -eq 1) { $arguments = '"{0}" /d /s /c "{1}"' -f "$env:SystemRoot\System32\cmd.exe", $arguments }
        $command = '"{0}" {1}' -f "$env:SystemRoot\System32\conhost.exe", $arguments
        $result = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=$command;CurrentDirectory=$settings.Root}
        if ($result.ReturnValue -ne 0) { throw ('Unable to create panel ' + $index) }
    }
    Set-Stage 'playing'
    [Worldex.Engine]::Director($Config)
    if (Test-Path -LiteralPath (Join-Path $runtime ($settings.Run + '-director.log'))) { throw 'Director failed; see director log.' }
    Set-Stage 'finished'
} catch {
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $runtime ($settings.Run + '-prepare.log')) -Encoding UTF8
    New-Item -ItemType File -Path $stop -Force | Out-Null
    Set-Stage 'error'
}
