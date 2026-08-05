# =====================================================================
#  GONDER - yaptigin her seyi karsi PC'ye gonderir
#
#  Kullanim:
#     .\gonder.ps1 "yaptigim isin kisa aciklamasi"
#     .\gonder.ps1                  (aciklama otomatik olusur)
#
#  Ne yapar: degisiklikleri commit'ler -> Gitea'ya push'lar
#            -> yedek olarak GitHub'a da push'lar
# =====================================================================

param([string]$Mesaj)

$ErrorActionPreference = 'Stop'
$Proje = Split-Path -Parent $MyInvocation.MyCommand.Path

function Basari($m) { Write-Host "  [OK] $m" -ForegroundColor Green }
function Bilgi($m)  { Write-Host "  [..] $m" -ForegroundColor Gray }
function Hata($m)   { Write-Host "  [!!] $m" -ForegroundColor Red }
function Uyari($m)  { Write-Host "  [!] $m"  -ForegroundColor Yellow }

Write-Host "`n=== GONDER ===" -ForegroundColor Cyan

# --- Unity acik mi? Sahne kaydedilmemis olabilir --------------------
if (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue) {
    Uyari "Unity acik. Gondermeden once Ctrl+S ile sahneyi KAYDET."
    $c = Read-Host "  Kaydettin mi? (e/h)"
    if ($c -notmatch '^[eE]') { Write-Host "  Iptal edildi."; exit 0 }
}

# --- Degisiklik var mi? --------------------------------------------
$durum = & git -C $Proje status --porcelain
if (-not $durum) {
    Basari "Degisiklik yok - gonderilecek bir sey yok"
} else {
    $adet = ($durum | Measure-Object).Count
    Bilgi "$adet dosya degismis"

    if (-not $Mesaj) { $Mesaj = "Calisma kaydi - $(Get-Date -Format 'yyyy-MM-dd HH:mm')" }

    & git -C $Proje add -A
    & git -C $Proje commit -m $Mesaj | Out-Null
    Basari "Commit'lendi: $Mesaj"
}

# --- Once karsidan gelenleri al (cakisma olmasin) -------------------
Bilgi "Karsi taraftan gelen var mi bakiliyor..."
& git -C $Proje fetch origin 2>&1 | Out-Null
$geride = & git -C $Proje rev-list --count "HEAD..origin/main"
if ([int]$geride -gt 0) {
    Bilgi "$geride yeni commit var, once onlar aliniyor..."
    & git -C $Proje pull --rebase origin main 2>&1 | Out-String | Write-Host
    if ($LASTEXITCODE -ne 0) {
        Hata "CAKISMA VAR - otomatik birlestirilemedi."
        Write-Host "  Ayni dosyaya iki taraf da dokunmus. Cozmek icin:" -ForegroundColor Yellow
        Write-Host "    git status          -> hangi dosya cakisti?" -ForegroundColor Yellow
        Write-Host "    git rebase --abort  -> vazgec, hicbir sey kaybolmaz" -ForegroundColor Yellow
        exit 1
    }
    Basari "Karsi tarafin isi alindi"
}

# --- Gitea'ya gonder (ana sunucu) ----------------------------------
Bilgi "Gitea'ya gonderiliyor..."
& git -C $Proje push origin main 2>&1 | Out-String | Write-Host
if ($LASTEXITCODE -eq 0) { Basari "Gitea'ya gonderildi" }
else { Hata "Gitea'ya gonderilemedi - sunucu kapali olabilir"; exit 1 }

# --- GitHub'a yedek gonder (basarisiz olursa sorun degil) ----------
$github = & git -C $Proje remote 2>$null | Where-Object { $_ -eq 'github' }
if ($github) {
    Bilgi "GitHub'a yedekleniyor..."
    & git -C $Proje push github main 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { Basari "GitHub yedegi guncel" }
    else { Uyari "GitHub yedegi basarisiz (onemli degil, Gitea'da guvende)" }
}

Write-Host "`nBitti. Karsi taraf artik '.\al.ps1' calistirip senin isini alabilir.`n" -ForegroundColor Green
