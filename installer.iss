[Setup]
AppId={{8E1A7C3D-2D91-4E37-8B54-123456789ABC}
AppName=In Tem KiotViet
AppVersion=1.0.0
DefaultDirName={userdesktop}\KiotViet Label Printer
DefaultGroupName=In Tem KiotViet
DisableProgramGroupPage=yes
OutputDir=installer_output
OutputBaseFilename=InTemKiotViet_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "publish\*"; DestDir: "{userdesktop}\KiotViet Label Printer"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userdesktop}\In Tem KiotViet"; Filename: "{userdesktop}\KiotViet Label Printer\KiotViet Label Printer Pro V2.exe"; WorkingDir: "{userdesktop}\KiotViet Label Printer"

[Run]
Filename: "{userdesktop}\KiotViet Label Printer\KiotViet Label Printer Pro V2.exe"; Description: "Mở In Tem KiotViet"; Flags: nowait postinstall skipifsilent