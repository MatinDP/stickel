#define AppVersion "1.0.1"
[Setup]
AppId={{BB36D4ED-02FA-4A6C-AE40-02DD260A45A3}
AppName=Stickel
AppVersion={#AppVersion}
AppPublisher=Stickel
DefaultDirName={localappdata}\Programs\Stickel
DefaultGroupName=Stickel
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=Stickel-Setup-{#AppVersion}-x64
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\Stickel.Desktop\Assets\stickel.ico
UninstallDisplayIcon={app}\Stickel.exe
ChangesAssociations=yes
CloseApplications=yes
SetupLogging=yes
[Tasks]
Name: "associate"; Description: "Open .stickel project files with Stickel"; Flags: unchecked
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
[Files]
Source: "..\dist\Stickel-1.0.1-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\Stickel"; Filename: "{app}\Stickel.exe"
Name: "{autodesktop}\Stickel"; Filename: "{app}\Stickel.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Classes\.stickel"; ValueType: string; ValueName: ""; ValueData: "Stickel.Project"; Tasks: associate; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\Stickel.Project"; ValueType: string; ValueName: ""; ValueData: "Stickel project"; Tasks: associate; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Stickel.Project\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Stickel.exe,0"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\Stickel.Project\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Stickel.exe"" ""%1"""; Tasks: associate
[Run]
Filename: "{app}\Stickel.exe"; Description: "Launch Stickel"; Flags: nowait postinstall skipifsilent
