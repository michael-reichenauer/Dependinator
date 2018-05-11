@echo off

powershell -ExecutionPolicy RemoteSigned -File .\Build.ps1 -configuration "Release"

echo.
echo.
pause