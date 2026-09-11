; Script do Inno Setup pro instalador do Optimize (OptimizePro.App + VetorGpl juntos).
; Compilar: ISCC.exe instalador.iss  (depois de rodar publicar.ps1)
; Baixar Inno Setup: https://jrsoftware.org/isdl.php

#define MyAppName "Optimize Pro"
#define MyAppVersion "1.0.3"
#define MyAppPublisher "Codeex Solutions"
#define MyAppExeName "OptimizePro.App.exe"

[Setup]
AppId={{A3F1B2C4-7D5E-4F8A-9C3B-1E2D3F4A5B6C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=© {#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=publish\instalador
OutputBaseFilename=Optimize-Setup-{#MyAppVersion}
SetupIconFile=Optimize.App\Assets\optimize.ico
; Propriedades do arquivo Optimize-Setup-*.exe em si (Explorer → Propriedades → Detalhes) —
; não só do app instalado.
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Instalador do {#MyAppName}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
; AppId fixo + pasta de instalação sem versão no caminho (linha 16) é o que faz rodar este
; instalador de novo ATUALIZAR em vez de instalar do zero: o Inno reconhece pelo AppId que é
; o mesmo app já instalado e sobrescreve os arquivos na mesma pasta, sem duplicar atalho nem
; entrada em "Programas e Recursos". CloseApplications fecha o Optimize Pro sozinho se ele
; estiver aberto na hora (senão o .exe travado faria a atualização falhar).
CloseApplications=force
RestartApplications=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"

[Files]
; Publica tudo que está em publish\Optimize.App (inclui a subpasta VetorGpl\ inteira,
; que precisa estar ao lado do executável principal — ver PotraceProcessoService.cs).
Source: "publish\Optimize.App\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir o {#MyAppName} agora"; Flags: nowait postinstall skipifsilent
