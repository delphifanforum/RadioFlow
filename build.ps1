<#
.SYNOPSIS
  RadioFlow: ortam kontrolü, restore, Release derleme, test ve win-x64 publish.
.EXAMPLE
  .\build.ps1              # tam akış
  .\build.ps1 -SkipPublish # yalnızca derleme + test
#>
param([switch]$SkipPublish, [switch]$SkipTests)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

function Step($name) { Write-Host "`n=== $name ===" -ForegroundColor Cyan }

Step "Ortam kontrolü"
$sdks = dotnet --list-sdks
$sdks | Write-Host
if (-not ($sdks | Where-Object { $_ -match '^10\.' })) {
    throw ".NET 10 SDK bulunamadı. https://dotnet.microsoft.com/download/dotnet/10.0 adresinden kurun."
}
Write-Host ("Windows: " + [Environment]::OSVersion.VersionString)
dotnet --info | Select-String "OS Version|RID|Base Path" | Write-Host

Step "dotnet restore"
dotnet restore RadioFlow.slnx
if ($LASTEXITCODE -ne 0) { throw "restore başarısız" }

Step "dotnet build -c Release"
dotnet build RadioFlow.slnx -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "build başarısız" }

if (-not $SkipTests) {
    Step "dotnet test -c Release"
    dotnet test --solution RadioFlow.slnx -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "testler başarısız" }
}

if (-not $SkipPublish) {
    Step "dotnet publish (win-x64, self-contained)"
    $out = Join-Path $PSScriptRoot "artifacts\publish\win-x64"
    dotnet publish .\src\RadioFlow.App\RadioFlow.App.csproj -c Release -r win-x64 --self-contained true -o $out
    if ($LASTEXITCODE -ne 0) { throw "publish başarısız" }

    Step "LibVLC yerel dosya doğrulaması"
    $required = @("RadioFlow.exe", "libvlc\win-x64\libvlc.dll", "libvlc\win-x64\libvlccore.dll", "libvlc\win-x64\plugins")
    $missing = $required | Where-Object { -not (Test-Path (Join-Path $out $_)) }
    if ($missing) { throw ("Publish çıktısında eksik: " + ($missing -join ", ")) }
    $size = (Get-ChildItem $out -Recurse | Measure-Object Length -Sum).Sum / 1MB
    Write-Host ("Publish tamam: {0} ({1:N0} MB)" -f $out, $size) -ForegroundColor Green
}
