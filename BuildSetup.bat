@echo off
echo Building setup ...
echo.

del DependinatorSetup.exe >nul 2>&1
del version.txt >nul 2>&1


if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" (
  set MSBUILD="%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe"
)
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe" (
  set MSBUILD="%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe"
)
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe" (
  set MSBUILD="%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"
)

if exist %MSBUILD% (
  echo Using MSBuild: %MSBUILD%
  echo.


  echo Restore nuget packets ...
  rem call %MSBUILD% /nologo /t:restore /v:m Dependinator.sln
  .\Binaries\nuget.exe restore Dependinator.sln
  echo.

  echo Building ...
  %MSBUILD% /t:rebuild /v:m /nologo Dependinator.sln 
  echo.

  echo Copy to Setup file ...
  copy Dependinator\bin\Debug\Dependinator.exe DependinatorSetup.exe /Y 

  echo Get built version...
  PowerShell -Command "& {(Get-Item DependinatorSetup.exe).VersionInfo.FILEVERSION }" > version.txt
  echo.

  echo DependinatorSetup.exe version:
  type version.txt 
  
) else (
  echo.
  echo Error: Failed to locate compatible msbuild.exe
)

echo.
echo.
pause