; DeskQuadra — instalador 1.0 (Inno Setup 6)
; Release 1.0.0 — framework-dependent (.NET 8 Desktop Runtime)
; Fonte empacotada: ..\release\installer-payload\ (publish framework-dependent leve, ~20 arquivos)
;   gerada com: dotnet publish src/DeskQuadra.UI.Wpf -c Release -r win-x64 --self-contained false -o release/installer-payload/
;   (NÃO usar o portátil self-contained aqui — são ~270 arquivos / ~170 MB.)
; Saída: ..\release\DeskQuadra-1.0.0-setup.exe (pasta release/ é ignorada pelo git via [Rr]elease/)
;
; URL direta do runtime .NET 8: ver const DotNetDesktopRuntimeUrl no [Code] abaixo (troca fácil).

#define MyAppName "DeskQuadra"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "DeskQuadra"
#define MyAppExeName "DeskQuadra.UI.Wpf.exe"
#define MyGuardianExeName "DeskQuadra.Guardian.exe"

[Setup]
; GUID novo gerado para o AppId do release 1.0 (identidade única no Windows para install/uninstall)
AppId={{67FCF5C7-F016-4E0E-9AC1-8270CC92ED7F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
OutputDir=..\release
OutputBaseFilename=DeskQuadra-1.0.0-setup
SetupIconFile=..\src\DeskQuadra.UI.Wpf\Assets\DeskQuadra.ico
UninstallDisplayIcon={app}\DeskQuadra.ico
Compression=lzma2/max
SolidCompression=yes
VersionInfoVersion=1.0.0
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppName} Instalador
; Fecha UI/Guardian abertos durante a instalação (evita "arquivo em uso")
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName},{#MyGuardianExeName}
RestartApplications=no

[Languages]
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Tasks]
; Atalho da área de trabalho: opcional e NÃO marcado por padrão (instalação limpa)
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Payload framework-dependent (leve) — recursivo para cobrir subpastas futuras
Source: "..\release\installer-payload\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Ícone oficial (Assets/DeskQuadra.ico) copiado para o {app}: alimenta o UninstallDisplayIcon
Source: "..\src\DeskQuadra.UI.Wpf\Assets\DeskQuadra.ico"; DestDir: "{app}"; DestName: "DeskQuadra.ico"; Flags: ignoreversion

[Icons]
; Menu Iniciar: sempre criado
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
; Área de trabalho: somente se a Task opcional for marcada
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[UninstallDelete]
; Desinstalador completo: remove resíduos gerados em execução (logs etc.) para a pasta sumir
Type: filesandordirs; Name: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent unchecked

[Code]
const
  { URL DIRETA do instalador do .NET 8 Desktop Runtime (x64).
    TROCA FÁCIL: para outra versão/branch (ex. .NET 9), substitua só esta const
    pelo link aka.ms correspondente (sempre o .exe direto, nunca página HTML). }
  DotNetDesktopRuntimeUrl = 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe';
  DotNetDesktopRuntimeFileName = 'windowsdesktop-runtime-win-x64.exe';

{ Detecta o runtime .NET 8 via chave de registro (instalação framework-dependent
  exige Microsoft.NETCore.App 8.x + Microsoft.WindowsDesktop.App 8.x). }
function IsDotNet8RuntimeInRegistry(): Boolean;
var
  NetCoreVer, DesktopVer: String;
begin
  Result := False;
  { Via 64-bit do registro (app x64). Tenta HKLM 64 + 32 bits. }
  if RegQueryStringValue(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App', 'Version', NetCoreVer) then
  begin
    if Copy(NetCoreVer, 1, 2) = '8.' then
    begin
      if RegQueryStringValue(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', 'Version', DesktopVer) then
      begin
        if Copy(DesktopVer, 1, 2) = '8.' then
        begin
          Result := True;
          Exit;
        end;
      end;
    end;
  end;
  { Fallback: nós 32-bit / chave legada de detecção do SDK/host. }
  if RegQueryStringValue(HKLM32, 'SOFTWARE\dotnet\Setup\InstalledVersions\x86\sharedfx\Microsoft.NETCore.App', 'Version', NetCoreVer) then
  begin
    if Copy(NetCoreVer, 1, 2) = '8.' then
    begin
      if RegQueryStringValue(HKLM32, 'SOFTWARE\dotnet\Setup\InstalledVersions\x86\sharedfx\Microsoft.WindowsDesktop.App', 'Version', DesktopVer) then
      begin
        if Copy(DesktopVer, 1, 2) = '8.' then
        begin
          Result := True;
          Exit;
        end;
      end;
    end;
  end;
end;

{ Detecta o runtime .NET 8 executando `dotnet --list-runtimes` e procurando
  "Microsoft.NETCore.App 8." e "Microsoft.WindowsDesktop.App 8.". }
function IsDotNet8RuntimeViaDotnetCmd(): Boolean;
var
  ResultCode: Integer;
  TempFile, Cmd: String;
  Output: AnsiString;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\dotnet_runtimes.txt');
  { Exec não passa por shell: rotear via cmd.exe para o redirecionamento ">" funcionar. }
  Cmd := '/C dotnet --list-runtimes > "' + TempFile + '" 2>&1';
  if Exec(ExpandConstant('{cmd}'), Cmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if LoadStringFromFile(TempFile, Output) then
    begin
      if (Pos('Microsoft.NETCore.App 8.', Output) > 0) and
         (Pos('Microsoft.WindowsDesktop.App 8.', Output) > 0) then
        Result := True;
    end;
    DeleteFile(TempFile);
  end;
end;

function IsDotNet8Installed(): Boolean;
begin
  Result := IsDotNet8RuntimeInRegistry() or IsDotNet8RuntimeViaDotnetCmd();
end;

{ Baixa o instalador do runtime para DestPath.
  1) curl.exe (in-box no Windows 10 1803+); 2) fallback PowerShell.
  Timeouts limitados para nunca travar o setup. }
function DownloadDotNetRuntime(const DestPath: String): Boolean;
var
  ResultCode: Integer;
  PsCmd: String;
begin
  Result := False;
  if Exec('curl.exe',
      '--fail -L --connect-timeout 30 --max-time 600 -o "' + DestPath + '" ' + DotNetDesktopRuntimeUrl,
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if (ResultCode = 0) and FileExists(DestPath) then
    begin
      Result := True;
      Exit;
    end;
  end;
  PsCmd := '-NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest -Uri ''' +
    DotNetDesktopRuntimeUrl + ''' -OutFile ''' + DestPath +
    ''' -UseBasicParsing -TimeoutSec 600; exit 0 } catch { exit 1 }"';
  if Exec('powershell.exe', PsCmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if (ResultCode = 0) and FileExists(DestPath) then
      Result := True;
  end;
end;

{ Instala o runtime baixado em modo silencioso.
  Retorna True se o instalador reportou sucesso (0 = ok, 1641/3010 = ok com reboot). }
function InstallDotNetRuntime(const InstallerPath: String; var NeedsRestart: Boolean): Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if Exec(InstallerPath, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    if (ResultCode = 0) or (ResultCode = 1641) or (ResultCode = 3010) then
    begin
      Result := True;
      if (ResultCode = 1641) or (ResultCode = 3010) then
        NeedsRestart := True;
    end;
  end;
end;

{ Bootstrap do .NET 8.
  FASE CORRETA: PrepareToInstall roda depois do wizard e antes da cópia dos arquivos —
  a pasta {tmp} já existe e ainda é possível abortar com mensagem (InitializeSetup seria
  cedo demais: sem {tmp} e antes mesmo do usuário confirmar o destino).
  Falha graciosa: qualquer problema AVISA e oferece continuar/cancelar; nunca trava. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  TmpExe: String;
  Choice: Integer;
begin
  Result := '';
  NeedsRestart := False;

  if IsDotNet8Installed() then
    Exit;

  { Modo silencioso: tenta o bootstrap sem perguntar; falha nunca aborta. }
  if WizardSilent() then
  begin
    TmpExe := ExpandConstant('{tmp}\' + DotNetDesktopRuntimeFileName);
    if DownloadDotNetRuntime(TmpExe) then
    begin
      InstallDotNetRuntime(TmpExe, NeedsRestart);
      DeleteFile(TmpExe);
    end;
    Exit;
  end;

  Choice := MsgBox(
    'O .NET 8 Desktop Runtime (x64) não foi detectado neste computador.' + #13#10 + #13#10 +
    'O DeskQuadra 1.0.0 é framework-dependent e precisa dele para executar.' + #13#10 + #13#10 +
    'Clique SIM para baixar e instalar automaticamente (~60 MB).' + #13#10 +
    'Clique NÃO para continuar SEM instalar (o aplicativo não vai abrir).' + #13#10 +
    'Clique CANCELAR para sair do assistente.',
    mbConfirmation, MB_YESNOCANCEL);

  if Choice = IDCANCEL then
  begin
    Result := 'Instalação cancelada pelo usuário (pré-requisito .NET 8 Desktop Runtime ausente).';
    Exit;
  end;
  if Choice = IDNO then
    Exit; { continua sem o runtime — decisão explícita do usuário }

  { SIM: baixa para {tmp} e executa /install /quiet /norestart. }
  TmpExe := ExpandConstant('{tmp}\' + DotNetDesktopRuntimeFileName);
  if not DownloadDotNetRuntime(TmpExe) then
  begin
    Choice := MsgBox(
      'Não foi possível baixar o .NET 8 Desktop Runtime.' + #13#10 +
      'Verifique sua conexão com a internet.' + #13#10 + #13#10 +
      'Deseja CONTINUAR a instalação mesmo assim?' + #13#10 +
      '(O DeskQuadra não vai abrir sem o runtime. Baixe depois em:' + #13#10 +
      DotNetDesktopRuntimeUrl + ')',
      mbError, MB_YESNO);
    if Choice = IDYES then
      Exit;
    Result := 'Instalação cancelada (falha ao baixar o .NET 8 Desktop Runtime).';
    Exit;
  end;

  if InstallDotNetRuntime(TmpExe, NeedsRestart) then
  begin
    DeleteFile(TmpExe);
    Exit;
  end;

  DeleteFile(TmpExe);
  Choice := MsgBox(
    'A instalação automática do .NET 8 Desktop Runtime falhou.' + #13#10 + #13#10 +
    'Deseja CONTINUAR mesmo assim?' + #13#10 +
    '(Instale manualmente depois a partir de:' + #13#10 +
    DotNetDesktopRuntimeUrl + ')',
    mbError, MB_YESNO);
  if Choice <> IDYES then
    Result := 'Instalação cancelada (falha ao instalar o .NET 8 Desktop Runtime).';
end;
