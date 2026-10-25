param(
    [string]$AudioPath = '',
    [double]$SeekSeconds = 0,
    [double]$CueOffsetSeconds = 0,
    [int]$PreviewSeconds = 0,
    [switch]$Mute,
    [switch]$Paused,
    [switch]$RenderOnly,
    [switch]$NoWait
)
$ErrorActionPreference = 'Stop'
$runtime = Join-Path $PSScriptRoot '.runtime'
$saved = $null
foreach ($remembered in @((Join-Path $runtime 'preferences.json'), (Join-Path $runtime 'run.json'))) {
    if (Test-Path -LiteralPath $remembered) {
        try { $saved = Get-Content -LiteralPath $remembered -Raw -Encoding UTF8 | ConvertFrom-Json; break } catch { }
    }
}
function Resolve-WorldexTool([string]$name, [string]$remembered) {
    if ($remembered -and (Test-Path -LiteralPath $remembered -PathType Leaf)) { return $remembered }
    $found = Get-Command $name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { return $found.Source }
    $searchPath = [Environment]::GetEnvironmentVariable('Path','User') + ';' + [Environment]::GetEnvironmentVariable('Path','Machine')
    foreach ($directory in ($searchPath -split ';')) {
        if (-not $directory) { continue }
        $candidate = Join-Path $directory ($name + '.exe')
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    throw ('Required tool missing: ' + $name + '. See README.md for setup.')
}
if (-not $AudioPath) {
    if ($saved -and $saved.Source -and (Test-Path -LiteralPath $saved.Source)) { $AudioPath = $saved.Source }
    if (-not $AudioPath) {
        foreach ($desktop in @([Environment]::GetFolderPath('Desktop'), (Join-Path $env:USERPROFILE 'Desktop'))) {
            $candidate = Join-Path $desktop 'world.execute (me) ; - Mili.flac'
            if (Test-Path -LiteralPath $candidate) { $AudioPath = $candidate; break }
        }
    }
}
if (-not (Test-Path -LiteralPath $AudioPath -PathType Leaf)) { throw ('Audio file not found: ' + $AudioPath) }
if ($SeekSeconds -lt 0 -or $PreviewSeconds -lt 0) { throw 'SeekSeconds and PreviewSeconds must be non-negative.' }
$python = Resolve-WorldexTool 'python' $saved.Python
$ffmpeg = Resolve-WorldexTool 'ffmpeg' $saved.Ffmpeg
& (Join-Path $PSScriptRoot 'Stop-Worldex.ps1')
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
@{Source=(Resolve-Path -LiteralPath $AudioPath).Path;Python=$python;Ffmpeg=$ffmpeg} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runtime 'preferences.json') -Encoding UTF8
$run = [Guid]::NewGuid().ToString('N')
$audioHash = (Get-FileHash -LiteralPath $AudioPath -Algorithm SHA256).Hash.ToLowerInvariant()
$cache = Join-Path $runtime ('cache\' + $audioHash)
$settings = @{
    Run=$run; Root=$PSScriptRoot; Source=(Resolve-Path -LiteralPath $AudioPath).Path
    Cache=$cache; Audio=(Join-Path $cache 'music.wav'); Features=(Join-Path $cache 'features.bin')
    Score=(Join-Path $cache 'performance-score.json'); Tokens=(Join-Path $PSScriptRoot 'assets\design-tokens.json')
    Lyrics=(Join-Path $cache 'lyrics-aligned.json'); Rhythm=(Join-Path $cache 'rhythm.json')
    Seek=$SeekSeconds; CueOffset=$CueOffsetSeconds; Preview=$PreviewSeconds; Mute=[bool]$Mute; Paused=[bool]$Paused; RenderOnly=[bool]$RenderOnly
    Python=$python; Ffmpeg=$ffmpeg
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
if (-not $NoWait) {
    $statusPath = Join-Path $runtime ($run + '-status.json')
    $playbackPath = Join-Path $runtime ($run + '-playback.json')
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $lastMessage = ''
    while ($timer.Elapsed.TotalSeconds -lt 150) {
        $state = $null
        if (Test-Path -LiteralPath $statusPath) {
            try { $state = Get-Content -LiteralPath $statusPath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { }
        }
        $panels = @(Get-ChildItem -LiteralPath $runtime -Filter ($run + '-*.json') | Where-Object Name -Match '-\d+\.json$').Count
        $stage = if ($state) { $state.Stage } else { 'starting' }
        $message = '{0}s  {1}  terminals {2}/18' -f [int]$timer.Elapsed.TotalSeconds,$stage,$panels
        if ($message -ne $lastMessage) { Write-Host ('  ' + $message); $lastMessage=$message }
        if ((-not $RenderOnly) -and (Test-Path -LiteralPath $playbackPath)) {
            Write-Host 'READY - all 18 terminals and audio clock are running.' -ForegroundColor Green
            return
        }
        if ($RenderOnly -and $stage -eq 'rendered') { Write-Host 'READY - scene previews rendered.' -ForegroundColor Green; return }
        if ($stage -eq 'error') {
            $details = @(Get-ChildItem -LiteralPath $runtime -Filter ($run + '*.log') | Where-Object Name -Match '-(prepare|director|lyrics|analysis)\.log$' | ForEach-Object {Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8}) -join "`n"
            throw ('Startup failed. ' + $details + "`nLogs: " + $runtime)
        }
        if ($stage -in @('stopped','superseded','finished')) { throw ('Startup ended: ' + $stage + '. Logs: ' + $runtime) }
        if (-not (Get-Process -Id $process.ProcessId -ErrorAction SilentlyContinue)) { throw ('Background preparation exited before readiness. Logs: ' + $runtime) }
        Start-Sleep -Milliseconds 500
    }
    throw ('Startup is taking longer than expected. Background preparation remains independent. Logs: ' + $runtime)
}
