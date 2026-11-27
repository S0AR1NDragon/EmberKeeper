# Сборка EmberKeeper: self-contained папка, portable zip и установщик EmberKeeperSetup.exe
# Запуск:  pwsh -File build-installer.ps1

param(
    [string]$Version = "1.0.0",
    [string]$Dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    if (-not (Test-Path $Dotnet)) { $Dotnet = "dotnet" }

    $publish = Join-Path $root "EmberKeeper\publish"
    $installerDir = Join-Path $root "installer"
    $dist = Join-Path $root "dist"
    $payload = Join-Path $installerDir "payload.zip"

    Write-Host "== 1/5 Публикация приложения (self-contained win-x64) ==" -ForegroundColor Cyan
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    & $Dotnet publish (Join-Path $root "EmberKeeper\EmberKeeper.csproj") -c Release -r win-x64 `
        --self-contained true -p:DebugType=none -o $publish
    if ($LASTEXITCODE -ne 0) { throw "публикация не удалась" }

    $dataOut = Join-Path $publish "data"
    $dataSrc = Join-Path $root "EmberKeeper\bin\Release\net8.0\data"
    if (-not (Test-Path $dataOut) -and (Test-Path $dataSrc)) {
        Copy-Item $dataSrc $dataOut -Recurse -Force
    }
    if (-not (Test-Path (Join-Path $dataOut "2014-spells.json"))) {
        Write-Warning "В папке data нет библиотеки — проверьте CopyData"
    }

    Write-Host "== 2/5 Упаковка payload.zip ==" -ForegroundColor Cyan
    if (Test-Path $payload) { Remove-Item $payload -Force }
    Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $payload -CompressionLevel Optimal
    $payloadMb = "{0:N1}" -f ((Get-Item $payload).Length / 1MB)
    Write-Host "   payload.zip: $payloadMb МБ"

    Write-Host "== 3/5 Сборка установщика ==" -ForegroundColor Cyan
    if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
    New-Item -ItemType Directory -Path $dist | Out-Null
    & $Dotnet publish (Join-Path $installerDir "EmberKeeperSetup.csproj") -c Release -r win-x64 `
        --self-contained true -p:DebugType=none -o $dist
    if ($LASTEXITCODE -ne 0) { throw "сборка установщика не удалась" }

    Write-Host "== 4/5 Имена файлов ==" -ForegroundColor Cyan
    $setup = Join-Path $dist "EmberKeeperSetup.exe"
    $setupNamed = Join-Path $dist "EmberKeeperSetup-$Version.exe"
    if (Test-Path $setup) { Move-Item $setup $setupNamed -Force }

    Write-Host "== 5/5 Portable-архив ==" -ForegroundColor Cyan
    $portable = Join-Path $dist "EmberKeeper-$Version-portable.zip"
    Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $portable -CompressionLevel Optimal

    # памятка и ключ для вашего ПК — кладём рядом с установщиком
    $readme = Join-Path $root "packaging\README.txt"
    if (Test-Path $readme) { Copy-Item $readme (Join-Path $dist "README.txt") -Force }
    $myKey = Join-Path $root "lic-machine.txt"
    if (Test-Path $myKey) { Copy-Item $myKey (Join-Path $dist "license.key") -Force }

    Write-Host ""
    Write-Host "Готово:" -ForegroundColor Green
    Get-ChildItem $dist | ForEach-Object {
        "{0,-42} {1,8:N1} МБ" -f $_.Name, ($_.Length / 1MB)
    }
}
finally {
    Pop-Location
}
