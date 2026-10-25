param([int]$Index, [string]$Config, [string]$Assembly)
$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName System.Windows.Forms, System.Drawing, System.Web.Extensions
    [Reflection.Assembly]::LoadFrom($Assembly) | Out-Null
    [Worldex.Engine]::Panel($Config, $Index)
} catch {
    $settings = Get-Content -LiteralPath $Config -Raw -Encoding UTF8 | ConvertFrom-Json
    $_ | Out-String | Set-Content -LiteralPath (Join-Path (Join-Path $settings.Root '.runtime') ($settings.Run + '-' + $Index + '.log')) -Encoding UTF8
}
