[Setup]
AppId={{8E1A7C3D-2D91-4E37-8B54-123456789ABC}
AppName=In Tem KiotViet
AppVersion=2.1.0
AppPublisher=Dung Store

; Người dùng tự chọn thư mục cài. App ghi config/data/logs cạnh file .exe nên
; thư mục cài được cấp quyền ghi cho Users ở [Dirs] (kể cả khi chọn Program Files).
DefaultDirName={autopf}\KiotViet Label Printer
DisableDirPage=no
DefaultGroupName=In Tem KiotViet_V1
DisableProgramGroupPage=yes
; Bản publish là win-x64 → cài vào Program Files 64-bit, không phải (x86).
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

OutputDir=installer_output
OutputBaseFilename=InTemKiotViet_V2.1.0_Setup

Compression=lzma
SolidCompression=yes
WizardStyle=modern

; Cho phép cài đè / update
AppMutex=KiotVietLabelPrinterProV2Mutex
CloseApplications=force
RestartApplications=no

; Gỡ bản cũ theo cùng AppId trước khi cài bản mới
UninstallDisplayIcon={app}\KiotViet Label Printer Pro V2.exe

[Tasks]
Name: "desktopicon"; Description: "Tạo biểu tượng In Tem KiotViet trên màn hình Desktop"; GroupDescription: "Biểu tượng:"

[Dirs]
Name: "{app}"; Permissions: users-modify

[InstallDelete]
; Xoá shortcut cũ (các bản trước luôn tạo) — nếu người dùng vẫn chọn tạo
; biểu tượng thì [Icons] sẽ tạo lại ngay sau đó.
Type: files; Name: "{userdesktop}\In Tem KiotViet.lnk"

[Files]
; config.json / history.json là DỮ LIỆU của người dùng (đường dẫn template,
; máy in, lịch sử in) — chỉ chép bản gốc khi máy chưa có, KHÔNG ghi đè khi
; cài bản cập nhật, và không xoá khi gỡ cài đặt.
Source: "publish\*"; DestDir: "{app}"; Excludes: "config.json,history.json"; Flags: ignoreversion overwritereadonly recursesubdirs createallsubdirs
Source: "publish\config\config.json"; DestDir: "{app}\Config"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "publish\Data\history.json"; DestDir: "{app}\Data"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{userdesktop}\In Tem KiotViet"; Filename: "{app}\KiotViet Label Printer Pro V2.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\KiotViet Label Printer Pro V2.exe"; Description: "Mở In Tem KiotViet"; Flags: nowait postinstall skipifsilent

[Code]
const
  AppExeName = 'KiotViet Label Printer Pro V2.exe';
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8E1A7C3D-2D91-4E37-8B54-123456789ABC}_is1';

// Bản cũ có thể đã cài ở chế độ 32-bit (WOW6432Node) hoặc 64-bit, theo máy
// hoặc theo user — thử lần lượt từng nhánh registry.
function GetInstalledVersion(): String;
begin
  Result := '';
  if not RegQueryStringValue(HKLM64, UninstallKey, 'DisplayVersion', Result) then
    if not RegQueryStringValue(HKLM32, UninstallKey, 'DisplayVersion', Result) then
      RegQueryStringValue(HKCU, UninstallKey, 'DisplayVersion', Result);
end;

// Hỏi MỘT lần khi thư mục đã chọn có sẵn bản cũ. Đồng ý thì mọi file được
// ghi đè thẳng (ignoreversion + overwritereadonly), không hỏi lại từng file.
function NextButtonClick(CurPageID: Integer): Boolean;
var
  Dir, Version, Msg: String;
begin
  Result := True;
  if (CurPageID <> wpSelectDir) or WizardSilent then
    Exit;

  Dir := WizardDirValue;
  if not FileExists(AddBackslash(Dir) + AppExeName) then
    Exit;

  Version := GetInstalledVersion();
  Msg := 'Thư mục này đã có In Tem KiotViet';
  if Version <> '' then
    Msg := Msg + ' (phiên bản ' + Version + ')';
  Msg := Msg + ':' + #13#10 + Dir + #13#10#13#10 +
    'Ghi đè bằng phiên bản {#SetupSetting("AppVersion")}?' + #13#10 +
    'Cấu hình và lịch sử in hiện có sẽ được giữ nguyên.' + #13#10#13#10 +
    'Chọn "No" để chọn thư mục khác.';

  Result := MsgBox(Msg, mbConfirmation, MB_YESNO) = IDYES;
end;
