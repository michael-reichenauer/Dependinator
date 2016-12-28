@echo off
echo Building setup ...
echo.

del DependiatorSetup.exe >nul 2>&1
del version.txt >nul 2>&1

call nuget restore Dependiator.sln
echo.

"%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe" Dependiator.sln /t:rebuild /v:m /nologo

echo.
copy Dependiator\bin\Debug\Dependiator.exe DependiatorSetup.exe /Y 

PowerShell -Command "& {(Get-Item DependiatorSetup.exe).VersionInfo.FILEVERSION }" > version.txt
echo.
echo DependiatorSetup.exe version:
type version.txt 

echo.
echo.
pause