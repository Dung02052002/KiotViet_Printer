@echo off
cd /d "%~dp0"

echo ==== CLEAN OLD BUILD ====
if exist publish rmdir /s /q publish
if exist installer_output rmdir /s /q installer_output

echo ==== PUBLISH APP ====
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish
if errorlevel 1 (
    echo Publish failed.
    pause
    exit /b 1
)

echo ==== BUILD INSTALLER ====
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
if errorlevel 1 (
    echo Installer build failed.
    pause
    exit /b 1
)

echo.
echo DONE. File setup nam trong installer_output
pause