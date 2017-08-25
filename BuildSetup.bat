@echo off
echo Building setup ...
echo.

del DependinatorSetup.exe >nul 2>&1
del version.txt >nul 2>&1

if exist DependinatorSetup.exe (
  echo.
  echo Error: Failed to clean DependinatorSetup.exe
  pause
  exit
)

if exist version.txt (
  echo.
  echo Error: Failed to clean version.txt
  pause
  exit
)

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
  rem echo Using MSBuild: %MSBUILD%
  rem echo.


  echo Restore nuget packets ...
  rem call %MSBUILD% /nologo /t:restore /v:m Dependinator.sln
  .\Binaries\nuget.exe restore -Verbosity quiet Dependinator.sln
  echo.

  echo Building ...
  %MSBUILD% /t:rebuild /v:m /nologo /p:Configuration=Release Dependinator.sln 
  echo.

  echo Copy Setup file ...
  copy Dependinator\bin\Release\Dependinator.exe DependinatorSetup.exe /Y >NUL

  PowerShell -Command "& {(Get-Item DependinatorSetup.exe).VersionInfo.FILEVERSION }" > version.txt
  echo.

  echo Version:
  type version.txt 
  
) else (
  echo.
  echo Error: Failed to locate compatible msbuild.exe
)

echo.
echo.
pause