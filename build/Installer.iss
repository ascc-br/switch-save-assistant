; AppId e diretório mantêm a continuidade com o instalador emergencial.
#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef AppBinaryVersion
  #error AppBinaryVersion is required
#endif
#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef ProjectDir
  #error ProjectDir is required
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif

[Setup]
AppId={{4DFD14CF-0D06-4B90-A795-598D698B9824}
AppName=Switch Save Assistant
AppVersion={#AppVersion}
AppVerName=Switch Save Assistant {#AppVersion}
AppPublisher=Alfran
AppPublisherURL=https://github.com/ascc-br/switch-save-assistant
AppSupportURL=https://github.com/ascc-br/switch-save-assistant/issues
DefaultDirName={localappdata}\Programs\Switch Save Assistant
DefaultGroupName=Switch Save Assistant
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=SwitchSaveAssistant-Setup-{#AppVersion}-win-x64
SetupIconFile={#ProjectDir}\SwitchSaveAssistant.App\Assets\App.ico
UninstallDisplayIcon={app}\SwitchSaveAssistant.exe
LicenseFile={#PayloadDir}\LICENSES.txt
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppBinaryVersion}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Switch Save Assistant"; Filename: "{app}\SwitchSaveAssistant.exe"
Name: "{autodesktop}\Switch Save Assistant"; Filename: "{app}\SwitchSaveAssistant.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SwitchSaveAssistant.exe"; Description: "{cm:LaunchProgram,Switch Save Assistant}"; Flags: nowait postinstall skipifsilent
