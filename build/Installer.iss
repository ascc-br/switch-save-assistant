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

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{4DFD14CF-0D06-4B90-A795-598D698B9824}_is1';
var
  MaintenancePage: TInputOptionWizardPage;
  DeleteSavedDataCheckBox: TNewCheckBox;
  ExistingInstallation, LeavingAfterUninstall: Boolean;
  ExistingDir, ExistingVersion, ExistingUninstaller: String;

function LocalText(Portuguese, English, Spanish: String): String;
begin
  if ActiveLanguage = 'brazilianportuguese' then Result := Portuguese
  else if ActiveLanguage = 'spanish' then Result := Spanish
  else Result := English;
end;

procedure MaintenanceSelectionChanged(Sender: TObject);
begin
  DeleteSavedDataCheckBox.Enabled := MaintenancePage.SelectedValueIndex = 1;
  if not DeleteSavedDataCheckBox.Enabled then DeleteSavedDataCheckBox.Checked := False;
end;

procedure DeleteSavedPreferences;
var
  DataDir: String;
  Failed: Boolean;
begin
  DataDir := AddBackslash(ExpandConstant('{localappdata}')) + 'SwitchSaveAssistant\';
  Failed := False;
  { Explicit files only: never delete save folders, safety backups or the data tree. }
  if FileExists(DataDir + 'settings.json') then
    if not DeleteFile(DataDir + 'settings.json') then Failed := True;
  if FileExists(DataDir + 'settings.json.tmp') then
    if not DeleteFile(DataDir + 'settings.json.tmp') then Failed := True;
  if FileExists(DataDir + 'profiles.json') then
    if not DeleteFile(DataDir + 'profiles.json') then Failed := True;
  if FileExists(DataDir + 'profiles.json.tmp') then
    if not DeleteFile(DataDir + 'profiles.json.tmp') then Failed := True;
  if Failed then
    MsgBox(LocalText('O aplicativo foi desinstalado, mas alguns arquivos de perfis ou configurações não puderam ser apagados.',
      'The application was uninstalled, but some profile or settings files could not be deleted.',
      'La aplicación se desinstaló, pero no se pudieron borrar algunos archivos de perfiles o configuración.'), mbError, MB_OK);
end;

procedure InitializeWizard;
var
  UninstallCommand: String;
begin
  ExistingInstallation := RegQueryStringValue(HKCU, UninstallKey, 'InstallLocation', ExistingDir);
  if ExistingInstallation then begin
    RegQueryStringValue(HKCU, UninstallKey, 'DisplayVersion', ExistingVersion);
    RegQueryStringValue(HKCU, UninstallKey, 'UninstallString', UninstallCommand);
    ExistingUninstaller := RemoveQuotes(UninstallCommand);
    WizardForm.DirEdit.Text := ExistingDir;
  end;
  MaintenancePage := CreateInputOptionPage(wpWelcome,
    LocalText('Manutenção do Switch Save Assistant', 'Switch Save Assistant maintenance', 'Mantenimiento de Switch Save Assistant'),
    LocalText('Instalação existente: ', 'Existing installation: ', 'Instalación existente: ') + ExistingVersion,
    LocalText('Escolha uma opção. A exclusão dos perfis e configurações salvas é opcional.',
      'Choose an option. Deleting saved profiles and settings is optional.',
      'Elige una opción. Borrar los perfiles y la configuración guardados es opcional.'), True, False);
  MaintenancePage.Add(LocalText('Atualizar/reparar os arquivos do aplicativo', 'Update/repair application files', 'Actualizar/reparar los archivos de la aplicación'));
  MaintenancePage.Add(LocalText('Desinstalar o aplicativo', 'Uninstall the application', 'Desinstalar la aplicación'));
  MaintenancePage.SelectedValueIndex := 0;
  MaintenancePage.CheckListBox.Height := ScaleY(70);
  DeleteSavedDataCheckBox := TNewCheckBox.Create(MaintenancePage);
  DeleteSavedDataCheckBox.Parent := MaintenancePage.Surface;
  DeleteSavedDataCheckBox.Left := MaintenancePage.CheckListBox.Left + ScaleX(24);
  DeleteSavedDataCheckBox.Top := MaintenancePage.CheckListBox.Top + MaintenancePage.CheckListBox.Height + ScaleY(8);
  DeleteSavedDataCheckBox.Width := MaintenancePage.SurfaceWidth - DeleteSavedDataCheckBox.Left;
  DeleteSavedDataCheckBox.Height := ScaleY(40);
  DeleteSavedDataCheckBox.Caption := LocalText('Apagar perfis e configurações salvas',
    'Delete saved profiles and settings', 'Borrar perfiles y configuración guardados');
  DeleteSavedDataCheckBox.Checked := False;
  DeleteSavedDataCheckBox.Enabled := False;
  MaintenancePage.CheckListBox.OnClickCheck := @MaintenanceSelectionChanged;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = MaintenancePage.ID then Result := not ExistingInstallation
  else if ExistingInstallation then
    Result := (PageID = wpWelcome) or
      (PageID = wpSelectDir) or (PageID = wpSelectProgramGroup) or (PageID = wpSelectTasks);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ExitCode: Integer;
begin
  Result := True;
  if (CurPageID <> MaintenancePage.ID) or (MaintenancePage.SelectedValueIndex <> 1) then Exit;
  Result := False;
  if (ExistingUninstaller = '') or not FileExists(ExistingUninstaller) then begin
    MsgBox(LocalText('Desinstalador ausente. Use Atualizar/reparar para restaurá-lo.',
      'Uninstaller missing. Use Update/repair to restore it.',
      'Falta el desinstalador. Usa Actualizar/reparar para restaurarlo.'), mbError, MB_OK);
    Exit;
  end;
  if not Exec(ExistingUninstaller, '/NORESTART', '', SW_SHOWNORMAL, ewWaitUntilTerminated, ExitCode) then begin
    MsgBox(LocalText('Não foi possível abrir o desinstalador.', 'Could not open the uninstaller.',
      'No se pudo abrir el desinstalador.'), mbError, MB_OK);
    Exit;
  end;
  if (ExitCode = 0) and not RegKeyExists(HKCU, UninstallKey) then begin
    if DeleteSavedDataCheckBox.Checked then DeleteSavedPreferences;
    LeavingAfterUninstall := True;
    WizardForm.Close;
  end;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if LeavingAfterUninstall then begin
    Cancel := True;
    Confirm := False;
  end;
end;
