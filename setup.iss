[Setup]
AppName=BaoTools
AppVersion=1.1.3
DefaultDirName={autopf}\BaoTools
DefaultGroupName=BaoTools
OutputDir=out_setup
OutputBaseFilename=BaoTools_Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=src\BaoToolsGui\icon.ico
UninstallDisplayIcon={app}\BaoTools.exe

[Files]
Source: "out_portable\BaoTools.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "out_portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\BaoTools"; Filename: "{app}\BaoTools.exe"
Name: "{autodesktop}\BaoTools"; Filename: "{app}\BaoTools.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\BaoTools.exe"; Description: "Launch BaoTools"; Flags: nowait postinstall skipifsilent
