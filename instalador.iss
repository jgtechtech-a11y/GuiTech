; Instalador do Gui_Tech (Inno Setup 6)
;
; Gera ..\GuiTech-dist\GuiTech-Setup-<versao>.exe a partir da versao em pasta
; (..\GuiTech-dist\GuiTech), que e a recomendada: nada e extraido em %TEMP%.
;
; Compilar:  ISCC.exe instalador.iss
; Teste:     ISCC.exe /DTESTE instalador.iss   (instala sem administrador, em pasta propria)

#define AppName "Gui_Tech"
; A versao e lida do proprio GuiTech.exe publicado (fonte unica: GuiTech.csproj).
; Se a pasta ainda nao existir, cai no valor padrao abaixo.
#ifdef AppVersion
#else
  #if FileExists("..\GuiTech-dist\GuiTech\GuiTech.exe")
    #define AppVersion GetStringFileInfo("..\GuiTech-dist\GuiTech\GuiTech.exe", "ProductVersion")
  #else
    #define AppVersion "0.25.13"
  #endif
#endif
#define AppExe "GuiTech.exe"
#define AppPublisher "Gui_Tech"
#define SourceDir "..\GuiTech-dist\GuiTech"

[Setup]
AppId={{8F3C1A64-5B27-4E9D-9B41-2C6D7A0E5F13}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
#ifndef TESTE
DefaultDirName={autopf}\GuiTech
#endif
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\GuiTech-dist
#ifdef TESTE
OutputBaseFilename=GuiTech-Setup-TESTE
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\GuiTechTeste
Compression=lzma/fast
#else
OutputBaseFilename=GuiTech-Setup-{#AppVersion}
PrivilegesRequired=admin
Compression=lzma2/ultra64
#endif
SetupIconFile=Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName} {#AppVersion}
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
LicenseFile=LICENSE
; Fecha o Gui_Tech sozinho antes de atualizar (o app usa este mutex).
AppMutex=Local\GuiTech.SingleInstance
CloseApplications=yes
RestartApplications=no
AllowNoIcons=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\LEIA-ME"; Filename: "{app}\LEIA-ME.txt"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir o {#AppName} agora"; Flags: nowait postinstall skipifsilent
Filename: "{app}\LEIA-ME.txt"; Description: "Abrir o LEIA-ME"; Flags: shellexec postinstall unchecked skipifsilent

[UninstallRun]
; Remove a tarefa que o app cria para abrir junto com o Windows (se o usuario ligou).
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN GuiTech /F"; Flags: runhidden; RunOnceId: "DelAutoStartTask"

[UninstallDelete]
; Nada de %AppData%: as configuracoes e o historico do usuario ficam preservados.
Type: dirifempty; Name: "{app}"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
