; Inno Setup Script para Calcpad-Lab
; Genera un instalador setup.exe

#define MyAppName "Calcpad-Lab"
#define MyAppVersion "1.0.0"
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

[Files]
; Application files — self-contained .NET 10 publish
Source: "{#MyAppPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

; Examples — in Documents folder
Source: "Examples\Algebra Lineal\*"; DestDir: "{userdocs}\Calcpad-Lab\Examples\Algebra Lineal"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist; Excludes: "*.html"
Source: "Examples\Cálculo\*"; DestDir: "{userdocs}\Calcpad-Lab\Examples\Cálculo"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist; Excludes: "*.html"
Source: "Examples\Mecánica\*"; DestDir: "{userdocs}\Calcpad-Lab\Examples\Mecánica"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist; Excludes: "*.html"
Source: "Examples\Finite Elements\*"; DestDir: "{userdocs}\Calcpad-Lab\Examples\Finite Elements"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist; Excludes: "*.html"
Source: "Examples\Demos\*"; DestDir: "{userdocs}\Calcpad-Lab\Examples\Demos"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist; Excludes: "*.html"

; Documentation
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{userdocs}\Calcpad-Lab"; Flags: uninsalwaysuninstall
Name: "{userdocs}\Calcpad-Lab\Examples"; Flags: uninsalwaysuninstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Examples"; Filename: "{userdocs}\Calcpad-Lab\Examples"
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
