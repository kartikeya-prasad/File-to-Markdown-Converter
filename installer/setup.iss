; Inno Setup script for File to Markdown Converter.
; The publish directory is passed via the /DPublishDir=<path> command-line define
; (see tools\build-exe-installer.ps1).

#ifndef PublishDir
  #error PublishDir must be defined: ISCC /DPublishDir=...
#endif
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#define MyAppName "File to Markdown Converter"
#define MyAppPublisher "Kartikeya Prasad"
#define MyAppExeName "FileToMarkdown.App.exe"

[Setup]
AppId={{A2B5C8D1-6E4F-4A7B-9C3D-1F2E3A4B5C6D}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\File to Markdown Converter
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#SourcePath}\..\dist
OutputBaseFilename=FileToMarkdownConverter-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
