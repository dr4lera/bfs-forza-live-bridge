param([string]$FFmpeg,[string]$Python)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
if(!$Python){$taskCommand=Get-Command python -ErrorAction SilentlyContinue;if($taskCommand){$Python=$taskCommand.Source}}
if(!$Python){throw 'Python 3 is required. Install from https://www.python.org/downloads/windows/ and enable its PATH option.'}
if(!$FFmpeg){
    $taskCandidates=@((Join-Path $taskRoot 'runtime\ffmpeg.exe'),(Join-Path $env:LOCALAPPDATA 'universal-modder\ffmpeg\bin\ffmpeg.exe'))
    foreach($taskCandidate in $taskCandidates){if(Test-Path -LiteralPath $taskCandidate){$FFmpeg=$taskCandidate;break}}
    if(!$FFmpeg){$taskCommand=Get-Command ffmpeg -ErrorAction SilentlyContinue;if($taskCommand){$FFmpeg=$taskCommand.Source}}
}
if(!$FFmpeg){
    Write-Host 'Downloading FFmpeg from the BtbN build project.'
    $taskRuntime=Join-Path $taskRoot 'runtime';New-Item -ItemType Directory -Path $taskRuntime -Force | Out-Null
    $taskArchive=Join-Path $taskRuntime 'ffmpeg-download.zip'
    Invoke-WebRequest -Uri 'https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip' -OutFile $taskArchive
    Expand-Archive -LiteralPath $taskArchive -DestinationPath $taskRuntime -Force
    $taskBuild=Get-ChildItem -LiteralPath $taskRuntime -Directory | Where-Object Name -Like 'ffmpeg-*-win64-gpl' | Select-Object -First 1
    if(!$taskBuild){throw 'FFmpeg archive layout was not recognized.'}
    $FFmpeg=Join-Path $taskBuild.FullName 'bin\ffmpeg.exe'
}
$taskEvidence=Join-Path $taskRoot 'evidence';New-Item -ItemType Directory -Path $taskEvidence -Force | Out-Null
$taskPidFile=Join-Path $taskEvidence 'capture.pid'
if(Test-Path -LiteralPath $taskPidFile){
    $taskSavedPid=[int](Get-Content -LiteralPath $taskPidFile)
    $taskOwner=Get-CimInstance Win32_Process -Filter "ProcessId=$taskSavedPid" -ErrorAction SilentlyContinue
    if($taskOwner -and $taskOwner.CommandLine -like "*$(Join-Path $taskRoot 'tools\capture.py')*"){Write-Host "Bridge already running (PID $taskSavedPid).";exit 0}
}
$taskStop=Join-Path $taskRoot 'capture.stop';if(Test-Path -LiteralPath $taskStop){Remove-Item -LiteralPath $taskStop}
$taskScript=Join-Path $taskRoot 'tools\capture.py'
$taskProcess=Start-Process -FilePath $Python -ArgumentList @('-u',('"'+$taskScript+'"'),'--ffmpeg',('"'+$FFmpeg+'"')) -WorkingDirectory $taskRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskEvidence 'capture-continuous.out.log') -RedirectStandardError (Join-Path $taskEvidence 'capture-continuous.err.log') -PassThru
$taskProcess.Id | Set-Content -LiteralPath $taskPidFile
Start-Sleep -Seconds 2
if($taskProcess.HasExited){Get-Content -LiteralPath (Join-Path $taskEvidence 'capture-continuous.err.log');throw 'Capture did not start. Open and restore Forza first.'}
Write-Host "Bridge running continuously (PID $($taskProcess.Id)). No time limit. Use Stop-Bridge.ps1 to stop."
