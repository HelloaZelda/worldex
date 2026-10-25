$ErrorActionPreference = 'Stop'
$runtime = Join-Path $PSScriptRoot '.runtime'
if (Test-Path -LiteralPath $runtime) {
    # Keep previous markers: an older detached preparation may still be exiting.
    $config = Join-Path $runtime 'run.json'
    if (Test-Path -LiteralPath $config) {
        $run = Get-Content -LiteralPath $config -Raw -Encoding UTF8 | ConvertFrom-Json
        New-Item -ItemType File -Path (Join-Path $runtime ($run.Run + '.stop')) -Force | Out-Null
        Start-Sleep -Milliseconds 350
    }
}
