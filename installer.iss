[Preprocessor]
#ifndef ReleaseDir
  #define ReleaseDir "release"
#endif

[Setup]
AppName=EME Diagnostics
AppVersion=1.5.2.0
AppPublisher=E.M.E
DefaultDirName={autopf}\EMEDiagnostics
DefaultGroupName=EME Diagnostics
OutputDir=installer
OutputBaseFilename=EMEDiagnostics_v1.5.2.0_Setup
Compression=lzma2/ultra64
SolidCompression=yes
SetupIconFile=docs\logo.ico
UninstallDisplayIcon={app}\EME.Diagnostics.App.exe
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Languages]
Name: "portuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[InstallDelete]
; Remove banco antigo (vazado) se existir em {app}\database
Type: files; Name: "{app}\database\eme-hardware.db"

[Dirs]
Name: "{commonappdata}\EME\HardwareDatabase"; Permissions: users-modify
Name: "{commonappdata}\EME\Diagnostics\network_reports"; Permissions: users-modify

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na area de trabalho"; GroupDescription: "Atalhos:"
Name: "startmenuicon"; Description: "Criar atalho no Menu Iniciar"; GroupDescription: "Atalhos:"; Flags: checkedonce

[Files]
Source: "{#ReleaseDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "database\*"
#ifexist "release\database\eme-hardware.db"
Source: "{#ReleaseDir}\database\eme-hardware.db"; DestDir: "{commonappdata}\EME\HardwareDatabase"; Flags: ignoreversion uninsneveruninstall
#endif
#ifexist "release\tools\PawnIO_setup.exe"
Source: "{#ReleaseDir}\tools\PawnIO_setup.exe"; DestDir: "{app}\tools"; Flags: ignoreversion
#endif

[Icons]
Name: "{group}\EME Diagnostics"; Filename: "{app}\EME.Diagnostics.App.exe"; Comment: "Abrir E.M.E Diagnostics"
Name: "{group}\Desinstalar EME Diagnostics"; Filename: "{uninstallexe}"
Name: "{autodesktop}\EME Diagnostics"; Filename: "{app}\EME.Diagnostics.App.exe"; Tasks: desktopicon

[Registry]
; Reforça a elevação para todos os atalhos, mesmo se o Windows ignorar o manifesto embutido.
Root: HKLM; Subkey: "Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; ValueType: string; ValueName: "{app}\EME.Diagnostics.App.exe"; ValueData: "RUNASADMIN"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\EME.Diagnostics.App.exe"; Description: "Abrir E.M.E Diagnostics agora"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNet8Installed: Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if not IsDotNet8Installed then
  begin
    if MsgBox('.NET 8 Desktop Runtime nao foi encontrado. Deseja baixar agora?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0/runtime', '', '', SW_SHOW, ewNoWait, ResultCode);
    end;
  end;
end;

procedure InstallPawnIO;
var
  ResultCode: Integer;
  PawnIOExe: String;
begin
  PawnIOExe := ExpandConstant('{app}\tools\PawnIO_setup.exe');
  if not FileExists(PawnIOExe) then
  begin
    Log('PawnIO_setup.exe nao encontrado em: ' + PawnIOExe + ' — pulando instalacao do driver.');
    Exit;
  end;

  Log('Instalando PawnIO driver...');
  if Exec(PawnIOExe, '-install -silent', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
      Log('PawnIO instalado com sucesso.')
    else if ResultCode = 3010 then
    begin
      Log('PawnIO: instalado, reinicializacao necessaria (3010).');
      SuppressibleMsgBox('O driver PawnIO foi instalado. Pode ser necessario reiniciar o computador para que todos os sensores funcionem.', mbInformation, MB_OK, IDOK);
    end
    else
      Log('PawnIO: codigo de retorno inesperado: ' + IntToStr(ResultCode));
  end
  else
    Log('Falha ao executar PawnIO_setup.exe. Codigo: ' + IntToStr(ResultCode));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    InstallPawnIO;
end;
