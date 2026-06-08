; Instalador OptimusCAI - Inno Setup
; Requisitos en la PC cliente:
; - .NET Framework 4.7.2 o superior (se descarga si falta)
; - SQL Server LocalDB (se descarga si falta)
; - Driver / Runtime DigitalPersona One Touch

#define AppName "OptimusCAI"
#define AppVersion "1.0.0"
#define ProjectDir "D:\Joaquin\Proyectos C#\SistemaGimnacionOptimusCAI"
#define ReleaseDir ProjectDir + "\bin\x86\Release"

[Setup]
AppId={{6A40C39E-5048-44EA-8B72-89986812D03D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=OptimusCAI
DefaultDirName=C:\OptimusCAI
DefaultGroupName={#AppName}
OutputDir={#ProjectDir}\Instalador\Output
OutputBaseFilename=Instalador_OptimusCAI
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
DisableProgramGroupPage=yes
WizardStyle=modern

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: checkedonce

[Files]
Source: "{#ReleaseDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ProjectDir}\DataBase\*"; DestDir: "{app}\DataBase"; Flags: ignoreversion recursesubdirs createallsubdirs onlyifdoesntexist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\SistemaGimnacionOptimusCAI.exe"; WorkingDir: "{app}"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\SistemaGimnacionOptimusCAI.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\SistemaGimnacionOptimusCAI.exe"; Description: "Abrir {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\error_inicio.txt"

[Code]
const
  NET_472_RELEASE = 461808;
  NET_472_WEB_INSTALLER_URL = 'https://go.microsoft.com/fwlink/?LinkId=863262';
  LOCALDB_INSTALLER_URL = 'https://download.microsoft.com/download/3/8/d/38de7036-2433-4207-8eae-06e247e17b25/SqlLocalDB.msi';

function IsNet472OrNewerInstalled(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release);

  if not Result then
    Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release);

  if Result then
    Result := Release >= NET_472_RELEASE;
end;

function IsLocalDbInstalled(): Boolean;
begin
  Result :=
    RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions\16.0') or
    RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions\15.0') or
    RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions\14.0') or
    RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions\13.0') or
    RegKeyExists(HKLM64, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions\16.0') or
    RegKeyExists(HKLM64, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions\15.0') or
    RegKeyExists(HKLM64, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions\14.0') or
    RegKeyExists(HKLM64, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions\13.0');
end;

function IsSuccessOrRestartCode(ResultCode: Integer): Boolean;
begin
  Result := (ResultCode = 0) or (ResultCode = 3010) or (ResultCode = 1641);
end;

function InstallNet472(): String;
var
  InstallerPath: String;
  ResultCode: Integer;
begin
  Result := '';
  InstallerPath := ExpandConstant('{tmp}\NDP472-KB4054531-Web.exe');

  WizardForm.StatusLabel.Caption := 'Descargando .NET Framework 4.7.2...';
  DownloadTemporaryFile(NET_472_WEB_INSTALLER_URL, 'NDP472-KB4054531-Web.exe', '', nil);

  WizardForm.StatusLabel.Caption := 'Instalando .NET Framework 4.7.2...';
  if not Exec(InstallerPath, '/q /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'No se pudo ejecutar el instalador de .NET Framework 4.7.2.';
    Exit;
  end;

  if not IsSuccessOrRestartCode(ResultCode) then
    Result := 'No se pudo instalar .NET Framework 4.7.2. Codigo: ' + IntToStr(ResultCode);
end;

function InstallLocalDb(): String;
var
  InstallerPath: String;
  ResultCode: Integer;
begin
  Result := '';
  InstallerPath := ExpandConstant('{tmp}\SqlLocalDB.msi');

  WizardForm.StatusLabel.Caption := 'Descargando SQL Server LocalDB...';
  DownloadTemporaryFile(LOCALDB_INSTALLER_URL, 'SqlLocalDB.msi', '', nil);

  WizardForm.StatusLabel.Caption := 'Instalando SQL Server LocalDB...';
  if not Exec('msiexec.exe', '/i "' + InstallerPath + '" /qn IACCEPTSQLLOCALDBLICENSETERMS=YES', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'No se pudo ejecutar el instalador de SQL Server LocalDB.';
    Exit;
  end;

  if not IsSuccessOrRestartCode(ResultCode) then
    Result := 'No se pudo instalar SQL Server LocalDB. Codigo: ' + IntToStr(ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';

  if not IsNet472OrNewerInstalled() then
  begin
    Result := InstallNet472();
    if Result <> '' then
      Exit;
  end;

  if not IsLocalDbInstalled() then
  begin
    Result := InstallLocalDb();
    if Result <> '' then
      Exit;
  end;
end;
