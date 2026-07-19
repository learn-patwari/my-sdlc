; SprintForge Beta — Inno Setup 6 installer script
; Produces: Setup.exe (single-file, self-contained)

#define AppName      "SprintForge Beta"
#define AppPublisher "SprintForge"
#define AppVersion   "1.0.0"
#define AppExeName   "SprintForge.exe"
#define AppIcon      "..\src\SprintForge.Wpf\Resources\sprintforge.ico"
#define PublishDir   "..\publish\SprintForge-win-x64"

[Setup]
AppId={{A3F2C1D4-8B5E-4F7A-9C3D-2E6B0A1F4D8C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/learn-patwari/my-sdlc
AppSupportURL=https://github.com/learn-patwari/my-sdlc/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=output
OutputBaseFilename=Setup
SetupIconFile={#AppIcon}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; Uninstaller
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";    Description: "Create a &desktop shortcut";          GroupDescription: "Additional icons:"; Flags: unchecked
Name: "startmenuicon";  Description: "Create a &Start Menu shortcut";       GroupDescription: "Additional icons:"; Flags: checkedonce

[Files]
; Self-contained single-file EXE — no runtime required
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";         Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";   Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove any app-generated files in the install dir on uninstall
Type: filesandordirs; Name: "{app}"
