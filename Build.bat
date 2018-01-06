@echo off

powershell -ExecutionPolicy RemoteSigned -File .\Build.ps1 -configuration "Release" -Target Build

echo.
echo.
pause