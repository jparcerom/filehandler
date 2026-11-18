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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
english.DesktopIconTask=Create a desktop shortcut
spanish.DesktopIconTask=Crear un acceso directo en el escritorio
english.ShortcutsGroup=Shortcuts:
spanish.ShortcutsGroup=Accesos directos:
english.LaunchFileHandler=Launch FileHandler
spanish.LaunchFileHandler=Iniciar FileHandler
english.CannotSaveLanguage=Could not save the selected application language.
spanish.CannotSaveLanguage=No se pudo guardar el idioma seleccionado de la aplicación.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIconTask}"; GroupDescription: "{cm:ShortcutsGroup}"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\FileHandler"; Filename: "{app}\FileHandler.exe"
Name: "{userdesktop}\FileHandler"; Filename: "{app}\FileHandler.exe"; Tasks: desktopicon

[UninstallDelete]
Type: files; Name: "{app}\app-language.txt"

[Run]
Filename: "{app}\FileHandler.exe"; Description: "{cm:LaunchFileHandler}"; Flags: postinstall nowait skipifsilent

[Code]
function GetAppLanguage(Param: String): String;
begin
  if ActiveLanguage = 'english' then
    Result := 'en'
  else
    Result := 'es';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    if not SaveStringToFile(
      ExpandConstant('{app}\app-language.txt'),
      GetAppLanguage(''),
      False) then
      RaiseException(ExpandConstant('{cm:CannotSaveLanguage}'));
end;
