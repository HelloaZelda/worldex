param([string]$Job)
$ErrorActionPreference='Stop'
$settings=Get-Content -LiteralPath $Job -Raw -Encoding UTF8|ConvertFrom-Json
$playback=Get-Content -LiteralPath $settings.Playback -Raw -Encoding UTF8|ConvertFrom-Json
$duration=if($settings.Length -gt 0){$settings.Length}else{$playback.Duration-$settings.Start}
$total=[int][Math]::Ceiling($duration*$settings.Fps)
$chunkSize=[int][Math]::Ceiling($total/$settings.Parallelism)
$chunks=@()
$clock=[Diagnostics.Stopwatch]::StartNew()
try {
    foreach($index in 0..($settings.Parallelism-1)) {
        $first=$index*$chunkSize;$count=[Math]::Min($chunkSize,$total-$first)
        if($count -le 0){continue}
        $directory=Join-Path $settings.Directory ('chunk-'+$index)
        New-Item -ItemType Directory -Path $directory -Force|Out-Null
        $child=Get-Content -LiteralPath $Job -Raw -Encoding UTF8|ConvertFrom-Json
        $child.Directory=$directory;$child.Start=$settings.Start+$first/[double]$settings.Fps
        $child.Length=$count/[double]$settings.Fps;$child.Parallelism=1
        $child|Add-Member -NotePropertyName FrameCount -NotePropertyValue $count -Force
        $child|Add-Member -NotePropertyName VideoOnly -NotePropertyValue $true -Force
        $child.Output=Join-Path $settings.Directory ('part-'+$index+'.mp4')
        $path=Join-Path $directory 'job.json'
        $child|ConvertTo-Json|Set-Content -LiteralPath $path -Encoding UTF8
        $command='"{0}" -NoProfile -STA -ExecutionPolicy Bypass -File "{1}" -Job "{2}"' -f "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe",(Join-Path $settings.Root 'tools\Render-VideoWorker.ps1'),$path
        $startup=New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]0}
        $process=Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=$command;CurrentDirectory=$settings.Root;ProcessStartupInformation=$startup}
        if($process.ReturnValue -ne 0){throw ('Unable to start video chunk '+$index)}
        $chunks+=@{directory=$directory;output=$child.Output;pid=$process.ProcessId;frames=$count}
    }
    while($true) {
        $done=0;$frames=0
        foreach($chunk in $chunks) {
            $progressPath=Join-Path $chunk.directory 'progress.json'
            if(Test-Path $progressPath) {
                $progress=$null
                try{$progress=Get-Content -LiteralPath $progressPath -Raw -Encoding UTF8|ConvertFrom-Json}catch{}
                if($progress.stage -eq 'error'){throw ('Chunk failed: '+$progress.message)}
                if($progress.stage -eq 'complete'){$done++;$frames+=$chunk.frames}else{$frames+=[int]$progress.frame}
            }
            if(-not(Get-Process -Id $chunk.pid -ErrorAction SilentlyContinue)) {
                if(-not(Test-Path $chunk.output)){throw ('Video worker exited before completion. '+$chunk.directory)}
            }
        }
        $fps=$frames/[Math]::Max(1,$clock.Elapsed.TotalSeconds)
        @{stage='rendering';frame=$frames;total=$total;percent=100.0*$frames/$total;fps=$fps;etaSeconds=if($fps -gt 0){($total-$frames)/$fps}else{0};output=$settings.Output;workers=$chunks.Count;completeChunks=$done}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $settings.Directory 'progress.json') -Encoding UTF8
        if($done -eq $chunks.Count){break}
        if(Test-Path (Join-Path $settings.Directory 'cancel')){throw 'Video export cancelled'}
        Start-Sleep -Seconds 2
    }
    @{stage='finalizing';percent=100;output=$settings.Output}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $settings.Directory 'progress.json') -Encoding UTF8
    $lines=@($chunks|ForEach-Object {"file '"+$_.output.Replace('\','/')+"'"}) -join "`n"
    $list=Join-Path $settings.Directory 'concat.txt'
    [IO.File]::WriteAllText($list,$lines,(New-Object Text.UTF8Encoding($false)))
    $temporary=Join-Path $settings.Directory 'final.mp4'
    $ErrorActionPreference='Continue'
    & $settings.Ffmpeg -hide_banner -loglevel warning -y -f concat -safe 0 -i $list -ss $settings.Start -t $duration -i $playback.Audio -map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 192k -ar 48000 -movflags +faststart $temporary 2>&1|Out-File -LiteralPath (Join-Path $settings.Directory 'mux.log') -Encoding UTF8
    $ErrorActionPreference='Stop'
    if($LASTEXITCODE -ne 0){throw 'Video assembly failed; see mux.log'}
    New-Item -ItemType Directory -Path (Split-Path $settings.Output -Parent) -Force|Out-Null
    Copy-Item -LiteralPath $temporary -Destination $settings.Output -Force
    @{stage='complete';frame=$total;total=$total;percent=100;output=$settings.Output;fps=$total/$clock.Elapsed.TotalSeconds;elapsedSeconds=$clock.Elapsed.TotalSeconds}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $settings.Directory 'progress.json') -Encoding UTF8
}catch {
    foreach($chunk in $chunks){New-Item -ItemType File -Path (Join-Path $chunk.directory 'cancel') -Force|Out-Null}
    $_|Out-String|Set-Content -LiteralPath (Join-Path $settings.Directory 'parallel-error.log') -Encoding UTF8
    @{stage='error';message=($_|Out-String)}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $settings.Directory 'progress.json') -Encoding UTF8
}
