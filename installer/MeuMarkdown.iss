; Inno Setup Script - Meu Markdown
; Para compilar: baixe Inno Setup em https://jrsoftware.org/isinfo.php

#define AppName "Meu Markdown"
; Versão vem do build-installer.bat via ISCC /DAppVersion=X.Y.Z (lê de Directory.Build.props).
; Default só pra IDE/abertura direta no Inno Setup Studio (build real sobrescreve).
#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif
#define AppPublisher "Adriano Caldeira"
#define AppURL "https://github.com/adrianocaldeira/meu-markdown"
#define AppExeName "MeuMarkdown.exe"
#define AppDescription "Editor e Visualizador Markdown"

[Setup]
AppId={{8F3A2B1C-4D5E-4F6A-8B7C-9D0E1F2A3B4C}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
; Splash/wizard image (164x314 pixels, bmp)
; WizardImageFile=installer\wizard-image.bmp
; WizardSmallImageFile=installer\wizard-small.bmp
LicenseFile=
OutputDir=dist
OutputBaseFilename=MeuMarkdown-Setup-v{#AppVersion}
SetupIconFile=..\src\MeuMarkdown\Resources\app-icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar ícone na área de trabalho"; GroupDescription: "Ícones adicionais:"; Flags: unchecked
Name: "fileassoc"; Description: "Associar arquivos .md e .markdown com {#AppName}"; GroupDescription: "Associações de arquivo:"

[Files]
; Main executable (published single-file)
Source: "..\src\MeuMarkdown\bin\Release\net10.0-windows\win-x64\publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; File association for .md
Root: HKA; Subkey: "Software\Classes\.md"; ValueType: string; ValueName: ""; ValueData: "MeuMarkdown.Document"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.markdown"; ValueType: string; ValueName: ""; ValueData: "MeuMarkdown.Document"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\MeuMarkdown.Document"; ValueType: string; ValueName: ""; ValueData: "Arquivo Markdown"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\MeuMarkdown.Document\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\MeuMarkdown.Document\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: fileassoc

; App registration
Root: HKA; Subkey: "Software\MeuMarkdown"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\MeuMarkdown"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Iniciar {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// Refresh file associations after install/uninstall
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.md', 'UserChoice');
end;
