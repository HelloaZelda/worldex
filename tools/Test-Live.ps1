param([switch]$KeepOpen)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$status=Join-Path $root '.runtime\live-final-qa.json'
$log=Join-Path $root '.runtime\live-final-qa.log'
$memory=$null;$map=$null
function Save-Result([string]$stage,[object]$details) {
    @{stage=$stage;details=$details;updated=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $status -Encoding UTF8
}
function Send-Command([int]$command) {
    $map.Write(16,$command)
    for($attempt=0;$attempt -lt 30;$attempt++) {
        Start-Sleep -Milliseconds 30
        if($map.ReadInt32(16) -eq 0){return}
    }
    throw 'Director did not acknowledge command.'
}
function Set-Time([double]$seconds) {
    $map.Write(48,$seconds);Send-Command 5;Start-Sleep -Milliseconds 650
}
try {
    Save-Result 'starting' $null
    & (Join-Path $root 'Start-Worldex.ps1') -SeekSeconds 67.25 -Paused -Mute -NoWait | Out-File $log -Encoding UTF8
    $run=Get-Content -LiteralPath (Join-Path $root '.runtime\run.json') -Raw -Encoding UTF8|ConvertFrom-Json
    $clock=Join-Path $root ('.runtime\'+$run.Run+'-playback.json')
    for($attempt=0;$attempt -lt 120;$attempt++) {
        if(Test-Path -LiteralPath $clock){break}
        $statePath=Join-Path $root ('.runtime\'+$run.Run+'-status.json')
        if(Test-Path $statePath) {
            $state=Get-Content -LiteralPath $statePath -Raw -Encoding UTF8|ConvertFrom-Json
            if($state.Stage -eq 'error'){throw 'Preparation failed; inspect run logs.'}
        }
        Start-Sleep -Milliseconds 500
    }
    if(-not(Test-Path $clock)){throw 'Timed out waiting for audio clock.'}
    $memory=[IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting('Local\Worldex.'+$run.Run)
    $map=$memory.CreateViewAccessor()
    Save-Result 'controls' @{run=$run.Run}
    Send-Command 1
    & (Join-Path $PSScriptRoot 'Verify-Worldex.ps1') -Live | Out-File $log -Append -Encoding UTF8
    Send-Command 1
    Set-Time 67.25
    & (Join-Path $PSScriptRoot 'Capture-Stage.ps1') -Output (Join-Path $root '.runtime\heart-native.png') | Out-File $log -Append -Encoding UTF8
    $actors=Get-ChildItem -LiteralPath (Join-Path $root '.runtime') -Filter ($run.Run+'-*.json')|Where-Object Name -Match '-\d+\.json$'
    $rows=@();$positions=@()
    foreach($file in $actors) {
        $actor=Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8|ConvertFrom-Json
        if($actor.Index -in @(7,8)){continue}
        $handle=[IntPtr]$actor.Handle
        if(-not[WorldexCapture]::IsWindowVisible($handle)){throw ('Heart actor hidden: '+$actor.Index)}
        $rect=New-Object WorldexCapture+Rect
        [WorldexCapture]::GetWindowRect($handle,[ref]$rect)|Out-Null
        $rows+=$rect.T;$positions+=@{handle=$handle;left=$rect.L;top=$rect.T}
    }
    $counts=($rows|Group-Object|Sort-Object {[int]$_.Name}|ForEach-Object {$_.Count}) -join ','
    if($counts -ne '2,5,5,3,1'){throw ('Native heart silhouette mismatch: '+$counts)}
    Start-Sleep -Milliseconds 500
    foreach($p in $positions) {
        $rect=New-Object WorldexCapture+Rect;[WorldexCapture]::GetWindowRect($p.handle,[ref]$rect)|Out-Null
        if($rect.L -ne $p.left -or $rect.T -ne $p.top){throw 'Native heart windows drift while held.'}
    }
    Save-Result 'formations' @{heartRows=$counts;actors=$positions.Count}
    $score=Get-Content -LiteralPath $run.Score -Raw -Encoding UTF8|ConvertFrom-Json
    $broken=@($score.scenes|Where-Object kind -eq 'broken')[0]
    Set-Time ($broken.time+.72)
    & (Join-Path $PSScriptRoot 'Capture-Stage.ps1') -Output (Join-Path $root '.runtime\broken-native.png') | Out-File $log -Append -Encoding UTF8
    $count=@($score.scenes|Where-Object kind -eq 'countdown')[0]
    Set-Time ($count.time+.30)
    & (Join-Path $PSScriptRoot 'Capture-Stage.ps1') -Output (Join-Path $root '.runtime\count-native.png') | Out-File $log -Append -Encoding UTF8
    Set-Time 67.25
    Save-Result 'passed' @{heartRows=$counts;actors=$positions.Count;captions=128;scenes=$score.scenes.Count;liveControls='passed';nativeStability='passed'}
}catch {
    $_|Out-String|Out-File $log -Append -Encoding UTF8
    Save-Result 'error' ($_|Out-String)
}finally {
    if($map){$map.Dispose()};if($memory){$memory.Dispose()}
    if(-not $KeepOpen){& (Join-Path $root 'Stop-Worldex.ps1')}
}
