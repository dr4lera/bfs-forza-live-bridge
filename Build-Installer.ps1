$ErrorActionPreference='Stop'
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path -LiteralPath $taskCompiler)){throw '.NET Framework 4 compiler not found.'}
& $taskCompiler /nologo /target:exe /optimize+ /warnaserror+ /out:"$PSScriptRoot\Install-Bridge.exe" /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "$PSScriptRoot\installer\Installer.cs"
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed.'}
