[Preprocessor]
#ifndef ReleaseDir
  #define ReleaseDir "release"
#endif

[Setup]
AppName=EME Diagnostics
AppVersion=1.0.1.0
AppPublisher=E.M.E
DefaultDirName={autopf}\EMEDiagnostics
DefaultGroupName=EME Diagnostics
OutputDir=installer
OutputBaseFilename=EMEDiagnostics_v1.0.1.0_Setup
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

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na area de trabalho"; GroupDescription: "Atalhos:"
Name: "startmenuicon"; Description: "Criar atalho no Menu Iniciar"; GroupDescription: "Atalhos:"; Flags: checkedonce

[Files]
Source: "{#ReleaseDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\EME Diagnostics"; Filename: "{app}\EME.Diagnostics.App.exe"; Comment: "Abrir E.M.E Diagnostics"
Name: "{group}\Desinstalar EME Diagnostics"; Filename: "{uninstallexe}"
Name: "{autodesktop}\EME Diagnostics"; Filename: "{app}\EME.Diagnostics.App.exe"; Tasks: desktopicon

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
