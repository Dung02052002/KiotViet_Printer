; Phiên bản do build-installer.bat truyền vào (/DAppVer=...) từ <Version>
; trong .csproj — chỉ cần sửa một chỗ. Giá trị dưới chỉ dùng khi chạy ISCC tay.
#ifndef AppVer
  #define AppVer "2.2.1"
#endif

[Setup]
AppId={{8E1A7C3D-2D91-4E37-8B54-123456789ABC}
AppName=In Tem KiotViet
AppVersion={#AppVer}
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
OutputBaseFilename=InTemKiotViet_V{#AppVer}_Setup

Compression=lzma
SolidCompression=yes
WizardStyle=modern

; Cho phép cài đè / update. KHÔNG dùng AppMutex ở đây: nó bắt người dùng tự
; đóng app trước khi cài, mà tiến trình bị treo (nằm trong Background
; processes, không có cửa sổ) thì người dùng không đóng được. Bản cũ đang
; chạy được tắt hẳn ở PrepareToInstall sau khi người dùng đồng ý.
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
Type: files; Name: "{commondesktop}\In Tem KiotViet.lnk"

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
Filename: "{app}\KiotViet Label Printer Pro V2.exe"; Description: "Mở In Tem KiotViet"; Flags: nowait postinstall skipifsilent runasoriginaluser
; Cập nhật từ trong app (UpdateService chạy setup /SILENT): mở lại app sau
; khi cài xong, bằng quyền của người dùng chứ không phải quyền admin của setup.
Filename: "{app}\KiotViet Label Printer Pro V2.exe"; Flags: nowait postinstall skipifnotsilent runasoriginaluser

[Code]
// ===== Gỡ sạch bản cũ trước khi cài =====
// Bản cũ có thể nằm ở nhiều nơi (Desktop của các bản trước, Program Files,
// ổ D:\...) và một tiến trình cũ bị treo không cửa sổ sẽ giữ mutex chặn bản
// mới mở lên. Sau khi chọn thư mục cài, setup liệt kê các bản cũ + tiến trình
// đang chạy và HỎI người dùng một lần; đồng ý thì PrepareToInstall tắt hẳn
// mọi tiến trình app và xoá các thư mục bản cũ. Dữ liệu người dùng (Config,
// Data, Output) được chuyển sang thư mục cài mới trước khi xoá.

const
  AppExeName = 'KiotViet Label Printer Pro V2.exe';
  AppFolderName = 'KiotViet Label Printer';
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8E1A7C3D-2D91-4E37-8B54-123456789ABC}_is1';
  DRIVE_FIXED = 3;

var
  OldDirs: TArrayOfString;       // thư mục bản cũ sẽ bị xoá
  RunningPaths: TArrayOfString;  // tiến trình app đang chạy (đường dẫn .exe)
  CleanupConfirmed: Boolean;

function GetDriveType(lpRootPathName: String): Cardinal;
  external 'GetDriveTypeW@kernel32.dll stdcall';

function SameDir(A, B: String): Boolean;
begin
  Result := CompareText(RemoveBackslashUnlessRoot(A), RemoveBackslashUnlessRoot(B)) = 0;
end;

procedure AddUnique(var List: TArrayOfString; Value: String);
var
  I, N: Integer;
begin
  Value := RemoveBackslashUnlessRoot(Trim(Value));
  if Value = '' then
    Exit;
  N := GetArrayLength(List);
  for I := 0 to N - 1 do
    if SameDir(List[I], Value) then
      Exit;
  SetArrayLength(List, N + 1);
  List[N] := Value;
end;

// Chỉ xoá thư mục chắc chắn là bản cài của app: có file .exe của app VÀ
// (đúng tên thư mục cài mặc định HOẶC có uninstaller của Inno). Bản app
// chạy từ chỗ khác (vd. bin\Debug của project) chỉ bị tắt, không bị xoá.
function IsOldInstallDir(Dir: String): Boolean;
begin
  Dir := AddBackslash(Dir);
  Result := FileExists(Dir + AppExeName) and
    ((CompareText(ExtractFileName(RemoveBackslash(Dir)), AppFolderName) = 0) or
     FileExists(Dir + 'unins000.exe'));
end;

// Liệt kê tiến trình app đang chạy qua WMI — gồm cả tiến trình treo không
// cửa sổ. Trả về số tiến trình; Paths nhận đường dẫn .exe (hoặc PID).
function QueryRunning(var Paths: TArrayOfString): Integer;
var
  Locator, Service, Procs, P: Variant;
  I: Integer;
  ExePath: String;
begin
  Result := 0;
  SetArrayLength(Paths, 0);
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Procs := Service.ExecQuery('SELECT ProcessId, ExecutablePath FROM Win32_Process WHERE Name = ''' + AppExeName + '''');
    Result := Procs.Count;
    for I := 0 to Procs.Count - 1 do
    begin
      P := Procs.ItemIndex(I);
      if VarIsNull(P.ExecutablePath) then
        ExePath := 'PID ' + IntToStr(P.ProcessId)
      else
        ExePath := P.ExecutablePath;
      SetArrayLength(Paths, I + 1);
      Paths[I] := ExePath;
    end;
  except
    Log('Không truy vấn được tiến trình qua WMI: ' + GetExceptionMessage);
  end;
end;

procedure AddCandidate(Dir: String);
begin
  if (Dir <> '') and IsOldInstallDir(Dir) then
    AddUnique(OldDirs, Dir);
end;

procedure AddRegistryInstallLocation(RootKey: Integer);
var
  Location: String;
begin
  if RegQueryStringValue(RootKey, UninstallKey, 'InstallLocation', Location) then
    AddCandidate(Location);
end;

procedure CollectOldVersions();
var
  I: Integer;
  Drive: String;
begin
  SetArrayLength(OldDirs, 0);
  QueryRunning(RunningPaths);

  // Bản đang cài theo registry đứng đầu: ưu tiên lấy cấu hình từ đây.
  AddRegistryInstallLocation(HKLM64);
  AddRegistryInstallLocation(HKLM32);
  AddRegistryInstallLocation(HKCU);
  AddCandidate(WizardDirValue);
  AddCandidate(ExpandConstant('{userdesktop}\' + AppFolderName));
  AddCandidate(ExpandConstant('{commondesktop}\' + AppFolderName));
  AddCandidate(ExpandConstant('{commonpf64}\' + AppFolderName));
  AddCandidate(ExpandConstant('{commonpf32}\' + AppFolderName));
  AddCandidate(ExpandConstant('{localappdata}\Programs\' + AppFolderName));

  // Gốc mọi ổ cứng cố định (C:\, D:\...) — không đụng ổ USB / CD để không
  // gây hộp thoại "chưa có đĩa".
  for I := Ord('C') to Ord('Z') do
  begin
    Drive := Chr(I) + ':\';
    if GetDriveType(Drive) = DRIVE_FIXED then
      AddCandidate(Drive + AppFolderName);
  end;

  // Thư mục của các tiến trình đang chạy (chỉ thêm nếu đúng là bản cài).
  for I := 0 to GetArrayLength(RunningPaths) - 1 do
    if Pos('PID ', RunningPaths[I]) <> 1 then
      AddCandidate(ExtractFileDir(RunningPaths[I]));
end;

function BuildCleanupMessage(): String;
var
  I: Integer;
begin
  Result := 'Phát hiện In Tem KiotViet cũ trên máy. Để bản mới chạy ổn định, setup sẽ:' + #13#10;
  if GetArrayLength(RunningPaths) > 0 then
  begin
    Result := Result + #13#10 + 'TẮT các tiến trình đang chạy (kể cả tiến trình bị treo):' + #13#10;
    for I := 0 to GetArrayLength(RunningPaths) - 1 do
      Result := Result + '   • ' + RunningPaths[I] + #13#10;
  end;
  if GetArrayLength(OldDirs) > 0 then
  begin
    Result := Result + #13#10 + 'XOÁ toàn bộ các bản cũ:' + #13#10;
    for I := 0 to GetArrayLength(OldDirs) - 1 do
      Result := Result + '   • ' + OldDirs[I] + #13#10;
  end;
  Result := Result + #13#10 +
    'Cấu hình, lịch sử in và thư mục Output sẽ được chuyển sang bản mới tại:' + #13#10 +
    '   ' + WizardDirValue + #13#10#13#10 +
    'Đồng ý gỡ bản cũ và cài phiên bản {#SetupSetting("AppVersion")}?' + #13#10 +
    'Chọn "No" để quay lại (bấm Cancel nếu muốn thoát cài đặt).';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID <> wpSelectDir then
    Exit;

  CollectOldVersions();
  if (GetArrayLength(OldDirs) = 0) and (GetArrayLength(RunningPaths) = 0) then
  begin
    CleanupConfirmed := False;
    Exit;
  end;

  if WizardSilent then
    CleanupConfirmed := True
  else
  begin
    CleanupConfirmed := MsgBox(BuildCleanupMessage(), mbConfirmation, MB_YESNO) = IDYES;
    Result := CleanupConfirmed;
  end;
end;

// Tắt hẳn mọi tiến trình app (/T: cả tiến trình con), đợi tối đa ~15 giây.
function KillRunningApp(): Boolean;
var
  ResultCode, Tries: Integer;
  Remaining: TArrayOfString;
begin
  Result := QueryRunning(Remaining) = 0;
  Tries := 0;
  while (not Result) and (Tries < 30) do
  begin
    if Tries mod 10 = 0 then
      Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /T /IM "' + AppExeName + '"', '',
        SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(500);
    Tries := Tries + 1;
    Result := QueryRunning(Remaining) = 0;
  end;
end;

// Chép cả cây thư mục, KHÔNG ghi đè file đã có ở đích.
procedure CopyDirNoOverwrite(Src, Dst: String);
var
  FindRec: TFindRec;
  S, D: String;
begin
  if not DirExists(Src) then
    Exit;
  ForceDirectories(Dst);
  if FindFirst(Src + '\*', FindRec) then
  try
    repeat
      if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
      begin
        S := Src + '\' + FindRec.Name;
        D := Dst + '\' + FindRec.Name;
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
          CopyDirNoOverwrite(S, D)
        else if not FileExists(D) then
          if not FileCopy(S, D, True) then
            Log('Không chép được ' + S);
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function IsKeptName(Name: String): Boolean;
begin
  Result := (CompareText(Name, 'Config') = 0) or (CompareText(Name, 'Data') = 0) or
    (CompareText(Name, 'Output') = 0) or (CompareText(Name, 'logs') = 0);
end;

// Thư mục cài mới trùng một bản cũ: xoá file chương trình cũ, giữ dữ liệu.
function CleanDirKeepingData(Dir: String): Boolean;
var
  FindRec: TFindRec;
  Path: String;
begin
  Result := True;
  if FindFirst(Dir + '\*', FindRec) then
  try
    repeat
      if (FindRec.Name <> '.') and (FindRec.Name <> '..') and not IsKeptName(FindRec.Name) then
      begin
        Path := Dir + '\' + FindRec.Name;
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          if not DelTree(Path, True, True, True) then
            Result := False;
        end
        else if not DeleteFile(Path) then
          Result := False;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  I: Integer;
  Target, Failed: String;
  ConfigCopied: Boolean;
begin
  Result := '';
  // Cài im lặng (/VERYSILENT): không có ai để hỏi — vẫn dọn bản cũ.
  if WizardSilent and not CleanupConfirmed then
  begin
    CollectOldVersions();
    CleanupConfirmed := True;
  end;
  if not CleanupConfirmed then
    Exit;

  if not KillRunningApp() then
  begin
    Result := 'Không tắt được In Tem KiotViet đang chạy. Hãy khởi động lại máy rồi chạy lại bộ cài.';
    Exit;
  end;

  Target := RemoveBackslashUnlessRoot(ExpandConstant('{app}'));
  ForceDirectories(Target);

  // Cấu hình + lịch sử: thư mục đích chưa có thì lấy từ bản cũ đầu tiên có
  // (bản theo registry được xếp trước).
  ConfigCopied := FileExists(Target + '\Config\config.json');
  for I := 0 to GetArrayLength(OldDirs) - 1 do
    if (not ConfigCopied) and (not SameDir(OldDirs[I], Target)) and
       FileExists(OldDirs[I] + '\Config\config.json') then
    begin
      CopyDirNoOverwrite(OldDirs[I] + '\Config', Target + '\Config');
      CopyDirNoOverwrite(OldDirs[I] + '\Data', Target + '\Data');
      ConfigCopied := True;
      Log('Đã chuyển cấu hình từ ' + OldDirs[I]);
    end;

  Failed := '';
  for I := 0 to GetArrayLength(OldDirs) - 1 do
  begin
    if SameDir(OldDirs[I], Target) then
    begin
      if not CleanDirKeepingData(Target) then
        Failed := Failed + '   • ' + Target + #13#10;
    end
    else
    begin
      CopyDirNoOverwrite(OldDirs[I] + '\Output', Target + '\Output');
      if DelTree(OldDirs[I], True, True, True) then
        Log('Đã xoá bản cũ ' + OldDirs[I])
      else
        Failed := Failed + '   • ' + OldDirs[I] + #13#10;
    end;
  end;

  // Không chặn cài đặt vì một vài file bị khoá — chỉ báo để người dùng tự xoá.
  if (Failed <> '') and not WizardSilent then
    MsgBox('Không xoá hết được các thư mục sau (có file đang bị khoá):' + #13#10 + Failed + #13#10 +
      'Bản mới vẫn được cài bình thường. Bạn có thể tự xoá các thư mục này sau.',
      mbInformation, MB_OK);
end;

// Gỡ cài đặt: tiến trình app đang chạy (kể cả bị treo) giữ file → hỏi rồi tắt.
function InitializeUninstall(): Boolean;
var
  Paths: TArrayOfString;
begin
  Result := True;
  if QueryRunning(Paths) = 0 then
    Exit;

  if MsgBox('In Tem KiotViet đang chạy. Tắt phần mềm để tiếp tục gỡ cài đặt?',
    mbConfirmation, MB_YESNO) <> IDYES then
  begin
    Result := False;
    Exit;
  end;

  if not KillRunningApp() then
  begin
    MsgBox('Không tắt được In Tem KiotViet. Hãy khởi động lại máy rồi gỡ lại.', mbError, MB_OK);
    Result := False;
  end;
end;
