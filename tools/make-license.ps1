# Выпуск лицензионного ключа EmberKeeper (для владельца программы)
$ErrorActionPreference = "Stop"
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$tool = Join-Path $root "tools\license-tool\EmberKeeperLicenseGen.exe"
$keys = Join-Path $root "keys"
$outDir = Join-Path $root "licenses"

Write-Host ""
Write-Host "  ============================================" -ForegroundColor DarkYellow
Write-Host "   EMBERKEEPER — выпуск лицензии" -ForegroundColor Yellow
Write-Host "  ============================================" -ForegroundColor DarkYellow
Write-Host ""

if (-not (Test-Path $tool)) { throw "Не найден генератор: $tool" }
if (-not (Test-Path (Join-Path $keys "private.pem"))) { throw "Не найден приватный ключ: $keys\private.pem" }

$name = Read-Host "Имя или название (например: Иван / Гильдия «Стальной клинок»)"
if ([string]::IsNullOrWhiteSpace($name)) { Write-Host "Отменено."; Read-Host "Enter — закрыть"; exit }

Write-Host ""
Write-Host "Код компьютера клиент видит в окне активации EmberKeeper." -ForegroundColor DarkGray
Write-Host "Оставьте пустым, чтобы лицензия работала на любом ПК (*)." -ForegroundColor DarkGray
$machine = Read-Host "Код компьютера (Enter = любой)"
if ([string]::IsNullOrWhiteSpace($machine)) { $machine = "*" }

Write-Host ""
$daysText = Read-Host "Срок в днях (Enter или 0 = бессрочно)"
$days = 0
if (-not [string]::IsNullOrWhiteSpace($daysText)) {
    if (-not [int]::TryParse($daysText, [ref]$days) -or $days -lt 0) { $days = 0 }
}

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
$safe = ($name -replace '[^\p{L}\p{N}\-_ ]', '').Trim() -replace '\s+', '-'
if ([string]::IsNullOrWhiteSpace($safe)) { $safe = "license-" + (Get-Date -Format "yyyyMMdd-HHmmss") }
$out = Join-Path $outDir ($safe + ".key")

Write-Host ""
Write-Host "Выпускаю ключ..." -ForegroundColor Cyan
& $tool --issue --name $name --machine $machine --days $days --keys $keys --out $out
if ($LASTEXITCODE -ne 0) { throw "Генератор вернул ошибку" }

$key = (Get-Content $out -Raw).Trim()
try { Set-Clipboard -Value $key; $copied = $true } catch { $copied = $false }

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host " Ключ готов и сохранён: $out" -ForegroundColor Green
if ($copied) { Write-Host " Ключ скопирован в буфер обмена — можно вставлять в письмо/мессенджер." -ForegroundColor Green }
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host $key -ForegroundColor Gray
Write-Host ""
Write-Host "Передайте клиенту этот текст: он вставляет его в окно активации EmberKeeper." -ForegroundColor DarkGray
Read-Host "Enter — закрыть"
