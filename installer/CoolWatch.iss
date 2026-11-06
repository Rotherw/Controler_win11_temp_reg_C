; Wcześniej: dotnet publish src/CoolWatch.App -c Release -r win-x64 --self-contained -o publish
#define AppName "CoolWatch"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputBaseFilename=CoolWatch-Setup
OutputDir=..\dist
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\CoolWatch.App.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\CoolWatch.App.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Utwórz skrót na pulpicie"

[Run]
Filename: "{app}\CoolWatch.App.exe"; Description: "Uruchom CoolWatch"; Flags: postinstall nowait skipifsilent
