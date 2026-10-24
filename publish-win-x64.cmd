@echo off
setlocal
cd /d "%~dp0"
dotnet publish src\AppManagerForJava -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64
exit /b %errorlevel%
