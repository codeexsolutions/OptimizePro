# Publica Optimize.App + VetorGpl juntos em publish\Optimize.App\ (VetorGpl numa subpasta,
# do jeito que PotraceProcessoService.cs espera encontrar). Depois disso, roda:
#   & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" instalador.iss
# pra gerar o instalador em publish\instalador\.

$ErrorActionPreference = "Stop"

Write-Host "Publicando Optimize.App..."
dotnet publish Optimize.App -c Release -r win-x64 --self-contained true -o publish\Optimize.App

Write-Host "Publicando VetorGpl..."
dotnet publish Ferramentas\VetorGpl -c Release -r win-x64 --self-contained true -o publish\Optimize.App\VetorGpl

Write-Host "Pronto. Binarios em publish\Optimize.App\"
