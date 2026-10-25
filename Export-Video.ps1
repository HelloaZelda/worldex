param(
    [int]$Width=3840,[int]$Height=2160,[int]$Fps=30,
    [string]$OutputPath='',
    [double]$Start=0,[double]$Length=0,
    [ValidateRange(1,6)][int]$Parallelism=4,
    [switch]$Stills
)
$ErrorActionPreference='Stop'
if($Width -lt 640 -or $Height -lt 360 -or $Width%2 -or $Height%2 -or $Fps -lt 1 -or $Fps -gt 60){throw 'Use even video dimensions >= 640x360, FPS 1..60.'}
$runtime=Join-Path $PSScriptRoot '.runtime'
$playback=Join-Path $runtime 'run.json'
if(-not(Test-Path $playback)){throw 'Run start.cmd once to prepare the local audio and lyrics.'}
$current=Get-Content -LiteralPath $playback -Raw -Encoding UTF8|ConvertFrom-Json
if(-not $current.Duration -or -not(Test-Path $current.Audio)){throw 'Audio preparation is incomplete.'}
if($Start -lt 0 -or $Start -ge $current.Duration -or $Length -lt 0){throw 'Invalid export range.'}
if($Length -gt 0){$Length=[Math]::Min($Length,$current.Duration-$Start)}
if(-not $OutputPath){$OutputPath=Join-Path $PSScriptRoot 'exports\world.execute-me-4k.mp4'}
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
$sources=Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs'|Sort-Object Name
$text=($sources|ForEach-Object {Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8}) -join "`n"
$sha=[Security.Cryptography.SHA256]::Create();$hash=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)))).Replace('-','').Substring(0,16);$sha.Dispose()
$assembly=Join-Path $runtime ('Worldex.'+$hash+'.dll')
if(-not(Test-Path $assembly)){throw 'Renderer build missing. Run Start-Worldex.ps1 -RenderOnly first.'}
$id=[Guid]::NewGuid().ToString('N');$directory=Join-Path $runtime ('video\'+$id)
New-Item -ItemType Directory -Path $directory -Force|Out-Null
Copy-Item -LiteralPath $playback -Destination (Join-Path $directory 'playback.json')
$encoder='h264_nvenc'
if(-not $Stills) {
    $ErrorActionPreference='Continue'
    & $current.Ffmpeg -hide_banner -loglevel error -f lavfi -i 'color=s=64x64:d=0.1' -c:v h264_nvenc -f null - 2>&1|Out-File (Join-Path $directory 'encoder-probe.log') -Encoding UTF8
    $ErrorActionPreference='Stop'
    if($LASTEXITCODE -ne 0){$encoder='libx264'}
}
$job=@{Root=$PSScriptRoot;Assembly=$assembly;Playback=(Join-Path $directory 'playback.json');Output=$OutputPath;Ffmpeg=$current.Ffmpeg;Directory=$directory;Encoder=$encoder;Width=$Width;Height=$Height;Fps=$Fps;Start=$Start;Length=$Length;Parallelism=$Parallelism}
$jobPath=Join-Path $directory 'job.json';$job|ConvertTo-Json|Set-Content -LiteralPath $jobPath -Encoding UTF8
@{stage='starting';output=$OutputPath}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $directory 'progress.json') -Encoding UTF8
$command='"{0}" -NoProfile -STA -ExecutionPolicy Bypass -File "{1}" -Job "{2}"{3}' -f "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe",(Join-Path $PSScriptRoot 'tools\Render-VideoWorker.ps1'),$jobPath,$(if($Stills){' -Stills'}else{''})
$startup=New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]0}
$process=Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine=$command;CurrentDirectory=$PSScriptRoot;ProcessStartupInformation=$startup}
if($process.ReturnValue -ne 0){throw 'Unable to launch detached video renderer.'}
$job['PID']=$process.ProcessId
$job|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $runtime 'latest-video.json') -Encoding UTF8
Write-Host ('Video job: '+$id+' / PID '+$process.ProcessId)
Write-Host ('Progress: '+(Join-Path $directory 'progress.json'))
Write-Host ('Output: '+$OutputPath)
