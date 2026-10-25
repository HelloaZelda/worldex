param([int]$DurationSeconds = 600)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if ($DurationSeconds -lt 10) { throw 'DurationSeconds must be at least 10.' }
& (Join-Path $root 'Stop-Worldex.ps1')
$runtime = Join-Path $root '.runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$run = [Guid]::NewGuid().ToString('N')
$epoch = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() + 1500
$config = @{ Run = $run; Epoch = $epoch; Duration = $DurationSeconds; Root = $root }
$configPath = Join-Path $runtime 'run.json'
$config | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
# Each conhost is independent of the launching terminal and survives its exit.
0..6 | ForEach-Object {
    $arguments = '"{0}" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "{1}" -Index {2} -Config "{3}"' -f "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe", (Join-Path $root 'Terminal-Panel.ps1'), $_, $configPath
    if ($_ % 2 -eq 1) { $arguments = '"{0}" /d /s /c "{1}"' -f "$env:SystemRoot\System32\cmd.exe", $arguments }
    Start-Process -FilePath "$env:SystemRoot\System32\conhost.exe" -ArgumentList $arguments -WindowStyle Normal | Out-Null
}
Write-Host 'WORLD EX / 7 terminal windows launched.'
Write-Host ('Duration: {0}s. Stop: .\Stop-Worldex.ps1' -f $DurationSeconds)
