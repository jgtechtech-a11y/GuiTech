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
    #define AppVersion "0.25.16"
  #endif
#endif
#define AppExe "GuiTech.exe"
#define AppPublisher "Gui_Tech"
; SourceDir/OutputDir podem vir do CI (/DSourceDir=dist /DOutputDir=dist), onde o
; repositorio e a propria pasta do projeto. Localmente, publicar.bat usa ..\GuiTech-dist.
#ifndef SourceDir
  #define SourceDir "..\GuiTech-dist\GuiTech"
#endif
#ifndef OutputDir
  #define OutputDir "..\GuiTech-dist"
#endif

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
OutputDir={#OutputDir}
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
; Sem LicenseFile: a pagina de aceite da MIT nao e necessaria a cada instalacao.
; Em vez dela, o assistente mostra INSTALACAO.txt (o que o programa faz + licenca MIT),
; e a LICENSE completa acompanha a instalacao em {app}\LICENSE (exigencia da MIT).
InfoBeforeFile=INSTALACAO.txt
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
; runascurrentuser: o GuiTech.exe exige administrador no manifesto; sem esse flag o
; Inno lanca com os privilegios originais e falha com CreateProcess codigo 740.
; Na variante TESTE (instalador sem admin) o shellexec deixa o Windows pedir elevacao.
#ifdef TESTE
Filename: "{app}\{#AppExe}"; Description: "Executar o programa agora"; Flags: nowait postinstall skipifsilent shellexec
#else
Filename: "{app}\{#AppExe}"; Description: "Executar o programa agora"; Flags: nowait postinstall skipifsilent runascurrentuser
#endif
Filename: "{app}\LEIA-ME.txt"; Description: "Abrir o LEIA-ME"; Flags: shellexec postinstall unchecked skipifsilent

; Atualização em um clique: o app baixa este Setup, confere o SHA-256 e o executa com
; /SILENT ... /AUTO. A entrada abaixo (fora do postinstall, pois /SILENT pula a pagina
; final) reabre o Gui_Tech como o usuario quando /AUTO foi usado.
#ifdef TESTE
Filename: "{app}\{#AppExe}"; Flags: nowait shellexec; Check: IsAutoUpdate
#else
Filename: "{app}\{#AppExe}"; Flags: nowait runascurrentuser; Check: IsAutoUpdate
#endif

[UninstallRun]
; Remove a tarefa que o app cria para abrir junto com o Windows (se o usuario ligou).
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN GuiTech /F"; Flags: runhidden; RunOnceId: "DelAutoStartTask"

[UninstallDelete]
; Nada de %AppData%: as configuracoes e o historico do usuario ficam preservados.
Type: dirifempty; Name: "{app}"

[Code]
function HasParam(const Name: string): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Name) = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

{ Verdadeiro quando o proprio Gui_Tech iniciou o Setup para se atualizar (/AUTO).
  Nesse modo o instalador e silencioso, entao quem reabre o programa e a entrada [Run]. }
function IsAutoUpdate: Boolean;
begin
  Result := HasParam('/AUTO');
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
end;
