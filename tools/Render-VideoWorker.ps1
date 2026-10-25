param([string]$Job,[switch]$Stills)
$ErrorActionPreference='Stop'
$settings=Get-Content -LiteralPath $Job -Raw -Encoding UTF8|ConvertFrom-Json
try {
    Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
    [Reflection.Assembly]::LoadFrom($settings.Assembly)|Out-Null
    $source=Get-Content -LiteralPath (Join-Path $settings.Root 'tools\video\Worldex.Video.cs') -Raw -Encoding UTF8
    $compiler=New-Object System.CodeDom.Compiler.CompilerParameters
    $compiler.GenerateInMemory=$true
    $compiler.CompilerOptions='/optimize+'
    $compiler.ReferencedAssemblies.AddRange(@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll',$settings.Assembly))
    Add-Type -TypeDefinition $source -CompilerParameters $compiler
    if((-not $Stills) -and $settings.Parallelism -gt 1) {
        & (Join-Path $settings.Root 'tools\Render-VideoParallel.ps1') -Job $Job
    }elseif($Stills) {
        [Worldex.Video.Renderer]::Stills($Job)
        @{stage='stills-ready';directory=$settings.Directory}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $settings.Directory 'progress.json') -Encoding UTF8
    }else{[Worldex.Video.Renderer]::Export($Job)}
}catch {
    $_|Out-String|Set-Content -LiteralPath (Join-Path $settings.Directory 'worker-error.log') -Encoding UTF8
    @{stage='error';message=($_|Out-String)}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $settings.Directory 'progress.json') -Encoding UTF8
}
