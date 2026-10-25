$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$latest=Join-Path $root '.runtime\latest-video.json'
if(-not(Test-Path $latest)){throw 'No video export has been started.'}
$job=Get-Content -LiteralPath $latest -Raw -Encoding UTF8|ConvertFrom-Json
$progress=Get-Content -LiteralPath (Join-Path $job.Directory 'progress.json') -Raw -Encoding UTF8|ConvertFrom-Json
[pscustomobject]@{
    Stage=$progress.stage
    Percent=if($null -ne $progress.percent){[Math]::Round($progress.percent,1)}else{0}
    Frames=$progress.frame
    TotalFrames=$progress.total
    EstimatedMinutes=if($progress.etaSeconds){[Math]::Round($progress.etaSeconds/60,1)}else{0}
    Output=$job.Output
    ProgressFile=(Join-Path $job.Directory 'progress.json')
    Error=$progress.message
}|Format-List
