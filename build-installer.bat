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

echo ==== LAM SACH ARTIFACT RUNTIME (tranh ship trang thai may build) ====
rem config.json trong publish phai la ban goc (seed) — khong dinh cache
rem BarTenderXmlScriptSupported / LastFolder... cua may build.
copy /Y "config\config.json" "publish\config\config.json" >nul
if exist "publish\logs" rmdir /s /q "publish\logs"
if exist "publish\debug_xml" rmdir /s /q "publish\debug_xml"
if exist "publish\print_logs" rmdir /s /q "publish\print_logs"

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

rem Lay <Version> tu .csproj de setup cung so phien ban voi app (app so
rem phien ban nay voi tag GitHub Release khi tu cap nhat).
set "APP_VER="
for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "([xml](Get-Content -Raw 'KiotViet Label Printer Pro V2.csproj')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1"`) do set "APP_VER=%%V"
if not defined APP_VER (
    echo LOI: Khong doc duoc ^<Version^> trong .csproj.
    pause
    exit /b 1
)

echo Dung ISCC tai: %ISCC%  ^(phien ban %APP_VER%^)
"%ISCC%" /DAppVer=%APP_VER% installer.iss
if errorlevel 1 (
    echo Installer build failed.
    pause
    exit /b 1
)

echo.
echo DONE. File setup nam trong installer_output
echo.
echo DE CAC MAY KHAC TU CAP NHAT: vao https://github.com/Dung02052002/KiotViet_Printer/releases/new
echo   - Tag: v%APP_VER%
echo   - Keo tha file installer_output\InTemKiotViet_V%APP_VER%_Setup.exe vao muc dinh kem
echo   - Ghi noi dung thay doi ^(hien cho nguoi dung khi hoi cap nhat^) roi bam Publish release
pause