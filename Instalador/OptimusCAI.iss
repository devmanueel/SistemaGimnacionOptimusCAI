; Instalador OptimusCAI - Inno Setup
; Requisitos en la PC cliente:
; - .NET Framework 4.7.2 o superior
; - SQL Server LocalDB
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
