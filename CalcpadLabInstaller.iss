; Inno Setup Script para Calcpad-Lab
; Genera un instalador setup.exe

#define MyAppName "Calcpad-Lab"
#define MyAppVersion "1.0.23"
#define MyAppPublisher "Jorge Burbano"
#define MyAppURL "https://github.com/GiorgioBurbanelli89/Calcpad-Lab"
#define MyAppExeName "CalcpadLab.exe"
#define MyAppPublishDir "C:\Users\j-b-j\Desktop\CalcpadLab-Installer\CalcpadLab"

[Setup]
AppId={{A7B8C9D0-1E2F-3A4B-5C6D-7E8F9A0B1C2D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Calcpad-Lab
DefaultGroupName=Calcpad-Lab
AllowNoIcons=yes
LicenseFile=LICENSE
OutputDir=.\Installer
OutputBaseFilename=Calcpad-Lab-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "fileassoc_m"; Description: "Asociar archivos .m (MATLAB) con Calcpad-Lab"; GroupDescription: "Asociaciones de archivo:"
Name: "fileassoc_cpd"; Description: "Asociar archivos .cpd con Calcpad-Lab"; GroupDescription: "Asociaciones de archivo:"; Flags: unchecked

[InstallDelete]
; Limpiar Examples viejos antes de copiar — evita que queden .m huérfanos de
; versiones anteriores (ej. fem_demo.m híbrido roto).
Type: filesandordirs; Name: "{app}\Examples"

[Files]
; Application files — self-contained .NET 10 publish
Source: "{#MyAppPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

; Examples — scripts .m (MATLAB) bundleados en {app}\Examples.
; La app al primer arranque los copia a {userdocs}\Calcpad-Lab\Examples del usuario real
; (evita el problema de install elevado donde {userdocs} apunta al admin).
Source: "Examples-Lab\*"; DestDir: "{app}\Examples"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist

; Documentation
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
; (La app crea {userdocs}\Calcpad-Lab\Examples en el primer arranque del usuario real.)

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Examples (bundleados)"; Filename: "{app}\Examples"
Name: "{group}\{cm:ProgramOnTheWeb,{#MyAppName}}"; Filename: "{#MyAppURL}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; .m file association
Root: HKA; Subkey: "Software\Classes\.m\OpenWithProgids"; ValueType: string; ValueName: "CalcpadLab.MFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc_m
Root: HKA; Subkey: "Software\Classes\CalcpadLab.MFile"; ValueType: string; ValueName: ""; ValueData: "Calcpad-Lab MATLAB Document"; Flags: uninsdeletekey; Tasks: fileassoc_m
Root: HKA; Subkey: "Software\Classes\CalcpadLab.MFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc_m
Root: HKA; Subkey: "Software\Classes\CalcpadLab.MFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc_m

; .cpd file association (optional)
Root: HKA; Subkey: "Software\Classes\.cpd\OpenWithProgids"; ValueType: string; ValueName: "CalcpadLab.CpdFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc_cpd
Root: HKA; Subkey: "Software\Classes\CalcpadLab.CpdFile"; ValueType: string; ValueName: ""; ValueData: "Calcpad-Lab Document"; Flags: uninsdeletekey; Tasks: fileassoc_cpd
Root: HKA; Subkey: "Software\Classes\CalcpadLab.CpdFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc_cpd
Root: HKA; Subkey: "Software\Classes\CalcpadLab.CpdFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc_cpd

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar Calcpad-Lab"; Flags: nowait postinstall skipifsilent
