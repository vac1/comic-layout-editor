; Script de Inno Setup (opcional) para crear un instalador de ComicLayout Editor.
;
; Requisitos: Inno Setup 6+  (https://jrsoftware.org/isinfo.php)
; Pasos:
;   1) Publica el ejecutable single-file:
;        dotnet publish src/ComicLayoutEditor.App -c Release -r win-x64
;   2) Compila este script con Inno Setup (ISCC.exe):
;        ISCC installer\ComicLayoutEditor.iss
;   3) El instalador se genera en  installer\Output\ComicLayoutEditorSetup.exe
;
; El instalador NO requiere .NET en la máquina destino: el .exe publicado es
; self-contained (incluye el runtime).

#define AppName "ComicLayout Editor"
#define AppVersion "1.0.0"
#define AppExe "ComicLayoutEditor.App.exe"
#define PublishDir "..\src\ComicLayoutEditor.App\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=ComicLayoutEditorSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

; --- Asociación del tipo de archivo .comicproj ---------------------------------
; Registra un ProgID propio y el comando de apertura. El "%1" es imprescindible:
; es lo que hace que Windows pase la RUTA del archivo como argumento al .exe.
; Sin "%1" la app arranca pero nunca recibe el fichero (doble clic no abre nada).
#define ProgId "ComicLayoutEditor.Project"

[Registry]
; ProgID: descripción y icono del tipo de archivo
Root: HKA; Subkey: "Software\Classes\{#ProgId}"; ValueType: string; ValueData: "Proyecto ComicLayout"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#ProgId}\DefaultIcon"; ValueType: string; ValueData: "{app}\{#AppExe},0"
; Comando de apertura — nótese el "%1" entre comillas
Root: HKA; Subkey: "Software\Classes\{#ProgId}\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExe}"" ""%1"""
; Vincula la extensión .comicproj con el ProgID
Root: HKA; Subkey: "Software\Classes\.comicproj"; ValueType: string; ValueData: "{#ProgId}"; Flags: uninsdeletevalue

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Iniciar {#AppName}"; Flags: nowait postinstall skipifsilent
