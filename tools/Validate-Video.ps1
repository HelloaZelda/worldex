param([string]$Path='', [double]$ExpectedDuration=0, [switch]$Decode, [string]$ReportPath='', [string]$JobPath='')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $JobPath){$JobPath=Join-Path $root '.runtime\latest-video.json'}
$job=Get-Content -LiteralPath $JobPath -Raw -Encoding UTF8|ConvertFrom-Json
if(-not $Path){$Path=$job.Output}
if(-not(Test-Path -LiteralPath $Path -PathType Leaf)){throw 'Video output does not exist yet.'}
$probe=Join-Path (Split-Path $job.Ffmpeg -Parent) 'ffprobe.exe'
$raw=& $probe -v error -show_entries 'stream=codec_type,codec_name,width,height,r_frame_rate,nb_frames,duration,sample_rate,channels:format=duration,size' -of json $Path
if($LASTEXITCODE){throw 'ffprobe failed.'}
$info=$raw|ConvertFrom-Json
$video=@($info.streams|Where-Object codec_type -eq 'video')[0]
$audio=@($info.streams|Where-Object codec_type -eq 'audio')[0]
if($video.width -ne $job.Width -or $video.height -ne $job.Height){throw 'Unexpected video dimensions.'}
if($video.r_frame_rate -ne ($job.Fps.ToString()+'/1')){throw 'Unexpected frame rate.'}
if($video.codec_name -ne 'h264' -or $audio.codec_name -ne 'aac' -or $audio.channels -ne 2){throw 'Expected H.264 video and stereo AAC audio.'}
if($ExpectedDuration -le 0){$playback=Get-Content -LiteralPath $job.Playback -Raw -Encoding UTF8|ConvertFrom-Json;$ExpectedDuration=if($job.Length -gt 0){$job.Length}else{$playback.Duration-$job.Start}}
$duration=[double]::Parse($info.format.duration,[Globalization.CultureInfo]::InvariantCulture)
if([Math]::Abs($duration-$ExpectedDuration) -gt .15){throw 'Video duration does not match the audio range.'}
$expectedFrames=[int][Math]::Ceiling($ExpectedDuration*$job.Fps)
if([int]$video.nb_frames -ne $expectedFrames){throw 'Unexpected video frame count.'}
$timestamps=@(& $probe -v error -select_streams v:0 -show_entries packet=dts_time -of csv=p=0 $Path|Where-Object {$_ -match '^-?\d'})
if($LASTEXITCODE){throw 'Packet timestamp inspection failed.'}
$previous=$null;$maxGap=0.0
foreach($stamp in $timestamps) {
    $current=[double]::Parse($stamp.Trim().TrimEnd(','),[Globalization.CultureInfo]::InvariantCulture)
    if($null -ne $previous){$gap=$current-$previous;if($gap -le 0){throw 'Non-monotonic video timestamps.'};$maxGap=[Math]::Max($maxGap,$gap)}
    $previous=$current
}
if($maxGap -gt 2.0/$job.Fps+.005){throw 'A timestamp gap was found at a chunk boundary.'}
if($Decode) {
    $ErrorActionPreference='Continue'
    & $job.Ffmpeg -hide_banner -loglevel error -xerror -hwaccel cuda -hwaccel_output_format cuda -i $Path -map 0:v:0 -map 0:a:0 -f null - 2>&1|Out-File -LiteralPath (Join-Path $job.Directory 'decode.log') -Encoding UTF8
    $ErrorActionPreference='Stop'
    if($LASTEXITCODE){throw 'Full decode failed; see decode.log'}
}
$result=@{stage='validated';width=$video.width;height=$video.height;fps=$job.Fps;frames=$expectedFrames;duration=$duration;videoCodec=$video.codec_name;audioCodec=$audio.codec_name;channels=$audio.channels;bytes=[long]$info.format.size;maxPacketGap=$maxGap;fullDecode=[bool]$Decode;output=$Path}
if(-not $ReportPath){$ReportPath=Join-Path $job.Directory 'validation.json'}
$result|ConvertTo-Json|Set-Content -LiteralPath $ReportPath -Encoding UTF8
$result|ConvertTo-Json
