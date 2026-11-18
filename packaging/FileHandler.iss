#ifndef AppVersion
  #define AppVersion "2.0.0"
#endif

#define AppName "FileHandler"
#define AppPublisher "jpmDevelope"

[Setup]
AppId={{77F28E9C-CA8B-4A9D-A1F3-BD947CFE4374}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts
OutputBaseFilename=FileHandler-{#AppVersion}-Setup-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppName} {#AppVersion}
UninstallDisplayIcon={app}\FileHandler.exe
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\FileHandler"; Filename: "{app}\FileHandler.exe"
Name: "{userdesktop}\FileHandler"; Filename: "{app}\FileHandler.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\FileHandler.exe"; Description: "Iniciar FileHandler"; Flags: postinstall nowait skipifsilent
