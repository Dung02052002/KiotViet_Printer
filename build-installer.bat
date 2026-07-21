@echo off
setlocal
cd /d "%~dp0"

set "EXE_NAME=KiotViet Label Printer Pro V2.exe"

echo ==== DONG APP NEU DANG CHAY (tranh bi khoa file khi build) ====
taskkill /IM "%EXE_NAME%" /F >nul 2>&1

echo ==== CLEAN OLD BUILD (publish, installer_output, bin, obj) ====
if exist publish rmdir /s /q publish
if exist installer_output rmdir /s /q installer_output
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

if exist publish (
    echo LOI: Khong xoa duoc thu muc publish cu ^(dang bi khoa boi tien trinh khac^).
    echo Hay dong het cua so app/Explorer dang mo trong thu muc do roi chay lai.
    pause
    exit /b 1
)

echo ==== RESTORE ====
dotnet restore
if errorlevel 1 (
    echo Restore failed.
    pause
    exit /b 1
)

echo ==== PUBLISH APP (Release build moi hoan toan) ====
dotnet publish "KiotViet Label Printer Pro V2.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish
if errorlevel 1 (
    echo Publish failed. Co the do file .exe dang bi chay/khoa hoac loi compile ben tren.
    pause
    exit /b 1
)

if not exist "publish\%EXE_NAME%" (
    echo LOI: Khong tim thay "%EXE_NAME%" trong thu muc publish. Build khong hoan tat, DUNG build installer voi ban cu.
    pause
    exit /b 1
)

echo ==== BUILD INSTALLER ====
set "ISCC="
if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "C:\Program Files\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
if not defined ISCC for %%I in (ISCC.exe) do if exist "%%~$PATH:I" set "ISCC=%%~$PATH:I"

if not defined ISCC (
    echo LOI: Khong tim thay ISCC.exe ^(Inno Setup 6^). Hay cai Inno Setup 6 hoac sua duong dan trong build-installer.bat.
    pause
    exit /b 1
)

echo Dung ISCC tai: %ISCC%
"%ISCC%" installer.iss
if errorlevel 1 (
    echo Installer build failed.
    pause
    exit /b 1
)

echo.
echo DONE. File setup nam trong installer_output
pause