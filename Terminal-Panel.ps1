param([int]$Index, [string]$Config, [string]$Assembly)
$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName System.Windows.Forms, System.Drawing, System.Web.Extensions
    foreach($attempt in 1..12) {
        try { [Reflection.Assembly]::LoadFrom($Assembly) | Out-Null;break }
        catch [System.IO.FileLoadException] {if($attempt -eq 12){throw};Start-Sleep -Milliseconds 500}
    }
    [Worldex.Engine]::Panel($Config, $Index)
} catch {
    $settings = Get-Content -LiteralPath $Config -Raw -Encoding UTF8 | ConvertFrom-Json
    $_ | Out-String | Set-Content -LiteralPath (Join-Path (Join-Path $settings.Root '.runtime') ($settings.Run + '-' + $Index + '.log')) -Encoding UTF8
}
