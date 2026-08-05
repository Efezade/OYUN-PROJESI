# =====================================================================
#  OYUN PROJESI - OTOMATIK YEDEKLEME KURULUMU
#  YONETICI olarak calistir. 1_sunucu_kur.ps1'den SONRA.
#
#  Ne yapar: her Pazar 03:00'te Gitea'nin tam yedegini alir
#            (depolar + kullanicilar + ayarlar), son 4 yedegi tutar.
# =====================================================================

$ErrorActionPreference = 'Stop'

$GiteaDir  = 'C:\Gitea'
$YedekDir  = 'C:\Gitea_Yedek'
$YedekAdet = 4          # kac yedek saklansin

$pr = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $pr.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "HATA: Yonetici olarak calistir." -ForegroundColor Red; exit 1
}

New-Item -ItemType Directory -Force -Path $YedekDir | Out-Null

# --- Yedek alma scriptini olustur ----------------------------------
$yedekScript = "$GiteaDir\yedek_al.ps1"
@"
`$ErrorActionPreference = 'Continue'
`$tarih  = Get-Date -Format 'yyyy-MM-dd_HHmm'
`$hedef  = '$YedekDir'
`$gitea  = '$GiteaDir\gitea.exe'
`$config = '$GiteaDir\custom\conf\app.ini'

Set-Location `$hedef
& `$gitea dump --config `$config --work-path '$GiteaDir' --type zip --file "gitea_yedek_`$tarih.zip"

# Eski yedekleri temizle - sadece son $YedekAdet tanesi kalsin
Get-ChildItem `$hedef -Filter 'gitea_yedek_*.zip' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip $YedekAdet |
    Remove-Item -Force

"`$(Get-Date -Format 'yyyy-MM-dd HH:mm') - yedek alindi" |
    Add-Content "`$hedef\yedek_gunlugu.txt"
"@ | Set-Content -Path $yedekScript -Encoding utf8

Write-Host "  [OK] Yedek scripti: $yedekScript" -ForegroundColor Green

# --- Haftalik zamanlanmis gorev ------------------------------------
$gorevAd = 'OYUN-Gitea-Yedek'
Get-ScheduledTask -TaskName $gorevAd -ErrorAction SilentlyContinue |
    Unregister-ScheduledTask -Confirm:$false

$eylem   = New-ScheduledTaskAction -Execute 'powershell.exe' `
             -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$yedekScript`""
$tetik   = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At 3am
$ayar    = New-ScheduledTaskSettingsSet -StartWhenAvailable `
             -DontStopOnIdleEnd -ExecutionTimeLimit (New-TimeSpan -Hours 3)
$asKimlik = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest

Register-ScheduledTask -TaskName $gorevAd -Action $eylem -Trigger $tetik `
    -Settings $ayar -Principal $asKimlik `
    -Description 'OYUN projesi Gitea sunucusunun haftalik yedegi' | Out-Null

Write-Host "  [OK] Haftalik gorev kuruldu: her Pazar 03:00" -ForegroundColor Green

# --- Ilk yedegi hemen al -------------------------------------------
Write-Host "  [..] Ilk yedek aliniyor (biraz surebilir)..." -ForegroundColor Gray
Start-ScheduledTask -TaskName $gorevAd

Write-Host @"

=====================================================
  YEDEKLEME KURULDU
=====================================================
  Konum : $YedekDir
  Zaman : Her Pazar 03:00
  Sayi  : Son $YedekAdet yedek saklanir

  ONEMLI: Bu yedek AYNI DISKTE. Disk arizasina karsi
  korumaz. Asil korumaniz zaten su uc kopya:
    1) Gitea sunucusu (bu PC)
    2) GitHub yedek deposu (bulut)
    3) Sherlock'un tam klonu (diger PC)

  Ayda bir bu klasoru harici diske kopyalarsan tam
  guvende olursun.
=====================================================

"@ -ForegroundColor Cyan
