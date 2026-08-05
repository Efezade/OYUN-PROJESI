# =====================================================================
#  AL - karsi PC'de yapilan her seyi bu bilgisayara indirir
#
#  Kullanim:  .\al.ps1
#
#  ONEMLI: Calistirmadan once Unity'yi KAPAT.
#  Sebep: Unity acikken sahne/prefab dosyalari degisirse Unity
#         hafizasindaki eski hali diske geri yazar ve is kaybolur.
# =====================================================================

$ErrorActionPreference = 'Stop'
$Proje = Split-Path -Parent $MyInvocation.MyCommand.Path

function Basari($m) { Write-Host "  [OK] $m" -ForegroundColor Green }
function Bilgi($m)  { Write-Host "  [..] $m" -ForegroundColor Gray }
function Hata($m)   { Write-Host "  [!!] $m" -ForegroundColor Red }
function Uyari($m)  { Write-Host "  [!] $m"  -ForegroundColor Yellow }

Write-Host "`n=== AL ===" -ForegroundColor Cyan

# --- Unity kapali olmali -------------------------------------------
if (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue) {
    Hata "Unity ACIK. Once Unity'yi kapat, sonra tekrar calistir."
    Write-Host "  (Acikken alirsan sahne degisiklikleri kaybolabilir.)" -ForegroundColor Yellow
    exit 1
}

# --- Kendi kaydedilmemis isin var mi? ------------------------------
$durum = & git -C $Proje status --porcelain
if ($durum) {
    $adet = ($durum | Measure-Object).Count
    Uyari "Sende gonderilmemis $adet dosya var."
    Write-Host "  Once '.\gonder.ps1' calistirman onerilir - yoksa cakisabilir." -ForegroundColor Yellow
    $c = Read-Host "  Yine de devam edeyim mi? (e/h)"
    if ($c -notmatch '^[eE]') { Write-Host "  Iptal edildi."; exit 0 }
}

# --- Yeni bir sey var mi? ------------------------------------------
Bilgi "Sunucu kontrol ediliyor..."
& git -C $Proje fetch origin 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Hata "Sunucuya ulasilamadi. Watson PC acik mi? Tailscale bagli mi?"
    Write-Host "  Kontrol: tailscale status" -ForegroundColor Yellow
    exit 1
}

$yeni = & git -C $Proje rev-list --count "HEAD..origin/main"
if ([int]$yeni -eq 0) {
    Basari "Zaten guncelsin - yeni bir sey yok"
    exit 0
}

Write-Host "`n  $yeni yeni commit geliyor:" -ForegroundColor White
& git -C $Proje log --oneline --no-decorate "HEAD..origin/main" | ForEach-Object { "     $_" }

# --- Indir ----------------------------------------------------------
Write-Host ""
Bilgi "Indiriliyor..."
& git -C $Proje pull --rebase origin main 2>&1 | Out-String | Write-Host

if ($LASTEXITCODE -ne 0) {
    Hata "CAKISMA - ayni dosyaya iki taraf da dokunmus."
    Write-Host "  Vazgecip eski haline donmek icin:  git rebase --abort" -ForegroundColor Yellow
    Write-Host "  Hangi dosya cakisti gormek icin :  git status" -ForegroundColor Yellow
    exit 1
}

Basari "Guncel! Unity'yi acabilirsin."
Write-Host "  (Unity acilirken yeni asset'leri isleyecegi icin biraz bekleyebilir.)`n" -ForegroundColor Gray
