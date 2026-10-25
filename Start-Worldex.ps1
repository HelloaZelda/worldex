param(
    [string]$AudioPath = '',
    [double]$SeekSeconds = 0,
    [double]$CueOffsetSeconds = 0,
    [int]$PreviewSeconds = 0,
    [switch]$Mute,
    [switch]$Paused,
    [switch]$RenderOnly
)
$ErrorActionPreference = 'Stop'
if (-not $AudioPath) {
    $previous = Join-Path $PSScriptRoot '.runtime\run.json'
    if (Test-Path -LiteralPath $previous) {
        $saved = Get-Content -LiteralPath $previous -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($saved.Source -and (Test-Path -LiteralPath $saved.Source)) { $AudioPath = $saved.Source }
    }
    if (-not $AudioPath) {
        foreach ($desktop in @([Environment]::GetFolderPath('Desktop'), (Join-Path $env:USERPROFILE 'Desktop'))) {
            $candidate = Join-Path $desktop 'world.execute (me) ; - Mili.flac'
            if (Test-Path -LiteralPath $candidate) { $AudioPath = $candidate; break }
        }
    }
}
if (-not (Test-Path -LiteralPath $AudioPath -PathType Leaf)) { throw ('Audio file not found: ' + $AudioPath) }
if ($SeekSeconds -lt 0 -or $PreviewSeconds -lt 0) { throw 'SeekSeconds and PreviewSeconds must be non-negative.' }
foreach ($tool in @('python', 'ffmpeg')) { if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw ('Required tool missing: ' + $tool) } }
& (Join-Path $PSScriptRoot 'Stop-Worldex.ps1')
$runtime = Join-Path $PSScriptRoot '.runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$run = [Guid]::NewGuid().ToString('N')
$audioHash = (Get-FileHash -LiteralPath $AudioPath -Algorithm SHA256).Hash.ToLowerInvariant()
$cache = Join-Path $runtime ('cache\' + $audioHash)
$settings = @{
    Run=$run; Root=$PSScriptRoot; Source=(Resolve-Path -LiteralPath $AudioPath).Path
    Cache=$cache; Audio=(Join-Path $cache 'music.wav'); Features=(Join-Path $cache 'features.bin')
    Score=(Join-Path $cache 'performance-score.json'); Tokens=(Join-Path $PSScriptRoot 'assets\design-tokens.json')
    Lyrics=(Join-Path $cache 'lyrics-aligned.json'); Rhythm=(Join-Path $cache 'rhythm.json')
    Seek=$SeekSeconds; CueOffset=$CueOffsetSeconds; Preview=$PreviewSeconds; Mute=[bool]$Mute; Paused=[bool]$Paused; RenderOnly=[bool]$RenderOnly
    Python=(Get-Command python).Source; Ffmpeg=(Get-Command ffmpeg).Source
}
$configPath = Join-Path $runtime ($run + '-config.json')
$settings | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
Copy-Item -LiteralPath $configPath -Destination (Join-Path $runtime 'run.json') -Force
# Launch the entire preparation/playback tree through WMI, outside the terminal job.
$command = '"{0}" -NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File "{1}" -Config "{2}"' -f "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe", (Join-Path $PSScriptRoot 'Prepare-Worldex.ps1'), $configPath
$startup = New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]0}
$process = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=$command;CurrentDirectory=$PSScriptRoot;ProcessStartupInformation=$startup}
if ($process.ReturnValue -ne 0) { throw ('Unable to start director: WMI ' + $process.ReturnValue) }
Write-Host ('WORLDEX preparing in background. Director PID: ' + $process.ProcessId)
Write-Host 'SPACE pause / LEFT RIGHT seek 10s / HOME restart / ESC stop'
Write-Host ('Status and logs: ' + $runtime)
