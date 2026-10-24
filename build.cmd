@echo off
setlocal
cd /d "%~dp0"
dotnet build AppManagerForJava.sln -c Release
if errorlevel 1 exit /b 1
dotnet run --project tests\AppManagerForJava.Tests -c Release --no-build
if errorlevel 1 exit /b 1
dotnet run --project tests\AppManagerForJava.Windows.Tests -c Release --no-build
exit /b %errorlevel%
