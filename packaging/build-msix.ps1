<#
.SYNOPSIS
  RadioFlow için MSIX paketi oluşturur (Faz 4 altyapısı — henüz uçtan uca test edilmedi).
.DESCRIPTION
  1) win-x64 self-contained publish alır
  2) Manifest ve görselleri publish klasörüne kopyalar
  3) Windows SDK'daki makeappx.exe ile .msix üretir
  4) (İsteğe bağlı) signtool ile imzalar
  Gereksinim: Windows 10/11 SDK (makeappx.exe, signtool.exe).
.EXAMPLE
  .\packaging\build-msix.ps1 -CertificatePath .\cert.pfx -CertificatePassword (Read-Host -AsSecureString)
#>
param(
    [string]$Configuration = "Release",
    [string]$CertificatePath,
    [SecureString]$CertificatePassword
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "packaging\out\msix-layout"
$output = Join-Path $root "packaging\out\RadioFlow_0.1.0.0_x64.msix"

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish (Join-Path $root "src\RadioFlow.App\RadioFlow.App.csproj") -c $Configuration -r win-x64 --self-contained true -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız." }

Copy-Item (Join-Path $PSScriptRoot "msix\Package.appxmanifest") (Join-Path $publish "AppxManifest.xml") -Force
Copy-Item (Join-Path $PSScriptRoot "msix\Assets") (Join-Path $publish "Assets") -Recurse -Force

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "\\x64\\" } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw "makeappx.exe bulunamadı. Windows SDK kurun." }

& $makeappx.FullName pack /d $publish /p $output /o
if ($LASTEXITCODE -ne 0) { throw "makeappx başarısız." }

if ($CertificatePath) {
    $signtool = Join-Path $makeappx.DirectoryName "signtool.exe"
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($CertificatePassword))
    & $signtool sign /fd SHA256 /a /f $CertificatePath /p $plain $output
    if ($LASTEXITCODE -ne 0) { throw "İmzalama başarısız." }
}
Write-Host "MSIX oluşturuldu: $output" -ForegroundColor Green
