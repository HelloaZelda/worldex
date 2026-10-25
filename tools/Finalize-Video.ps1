param([string]$Job)
$ErrorActionPreference='Stop'
$settings=Get-Content -LiteralPath $Job -Raw -Encoding UTF8|ConvertFrom-Json
$report=Join-Path $settings.Directory 'finalization.json'
try {
    @{stage='waiting-for-render'}|ConvertTo-Json|Set-Content $report -Encoding UTF8
    $timer=[Diagnostics.Stopwatch]::StartNew()
    while($timer.Elapsed.TotalHours -lt 4) {
        $state=$null
        try{$state=Get-Content (Join-Path $settings.Directory 'progress.json') -Raw -Encoding UTF8|ConvertFrom-Json}catch{}
        if($state.stage -eq 'error'){throw ('Rendering failed: '+$state.message)}
        if($state.stage -eq 'complete'){break}
        Start-Sleep -Seconds 2
    }
    if($state.stage -ne 'complete'){throw 'Render wait timed out.'}
    @{stage='validating'}|ConvertTo-Json|Set-Content $report -Encoding UTF8
    & (Join-Path $PSScriptRoot 'Validate-Video.ps1') -JobPath $Job -Decode | Out-File (Join-Path $settings.Directory 'validation-output.log') -Encoding UTF8
    $desktop=[Environment]::GetFolderPath('Desktop')
    if(-not(Test-Path $desktop)){New-Item -ItemType Directory -Path $desktop -Force|Out-Null}
    $copy=Join-Path $desktop 'world.execute-me-4k.mp4'
    if(Test-Path $copy){$copy=Join-Path $desktop ('world.execute-me-4k-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss')+'.mp4')}
    Copy-Item -LiteralPath $settings.Output -Destination $copy
    @{stage='complete';output=$settings.Output;desktop=$copy;bytes=(Get-Item $settings.Output).Length;validation=(Join-Path $settings.Directory 'validation.json')}|ConvertTo-Json|Set-Content $report -Encoding UTF8
}catch {
    $_|Out-String|Set-Content (Join-Path $settings.Directory 'finalization-error.log') -Encoding UTF8
    @{stage='error';message=($_|Out-String)}|ConvertTo-Json|Set-Content $report -Encoding UTF8
}
