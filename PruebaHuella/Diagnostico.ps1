# Diagnostico del lector DigitalPersona U.are.U
# Ejecutar:  powershell -ExecutionPolicy Bypass -File Diagnostico.ps1
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  DIAGNOSTICO LECTOR DIGITALPERSONA U.are.U" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# 1) Dispositivo presente?
$dev = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -match "VID_05BA" }
if ($dev) {
    Write-Host "[OK] Lector CONECTADO y presente:" -ForegroundColor Green
    $dev | ForEach-Object { Write-Host "     $($_.FriendlyName)  [$($_.Status)]" }
} else {
    Write-Host "[X] No hay ningun lector DigitalPersona presente." -ForegroundColor Red
    Write-Host "    -> Desenchufa y vuelve a enchufar el lector en un puerto USB directo."
    Write-Host "    -> Revisa que el LED rojo del lector este encendido."
}
Write-Host ""

# 2) Servicios necesarios
Write-Host "Servicios:" -ForegroundColor Yellow
foreach ($s in "DpHost","usbdpfp") {
    $svc = Get-Service $s -ErrorAction SilentlyContinue
    if ($svc) { Write-Host "   $($s.PadRight(10)) -> $($svc.Status)" }
    else { Write-Host "   $($s.PadRight(10)) -> NO INSTALADO" -ForegroundColor Red }
}
Write-Host ""

# 3) Prueba de enumeracion via SDK (si el exe esta compilado)
$exe = Join-Path $PSScriptRoot "PruebaHuella.exe"
if ((Test-Path $exe) -and $dev) {
    Write-Host "Iniciando prueba de captura del SDK..." -ForegroundColor Yellow
    Write-Host "(Apoya un dedo en el lector cuando lo pida)" -ForegroundColor Yellow
    Write-Host ""
    & $exe
} elseif (-not $dev) {
    Write-Host "Conecta el lector y vuelve a correr este script." -ForegroundColor Yellow
}
