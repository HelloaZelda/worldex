$ErrorActionPreference = 'Stop'
$runtime = Join-Path $PSScriptRoot '.runtime'
if (Test-Path -LiteralPath $runtime) {
    # Workers check this marker every frame, then close their own console.
    Get-ChildItem -LiteralPath $runtime -Filter '*.stop' | Remove-Item -Force
    $config = Join-Path $runtime 'run.json'
    if (Test-Path -LiteralPath $config) {
        $run = Get-Content -LiteralPath $config -Raw -Encoding UTF8 | ConvertFrom-Json
        New-Item -ItemType File -Path (Join-Path $runtime ($run.Run + '.stop')) -Force | Out-Null
        Start-Sleep -Milliseconds 350
    }
}
