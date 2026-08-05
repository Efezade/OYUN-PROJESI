# =====================================================================
#  OYUN PROJESI - KURULUMU BITIR + SHERLOCK PROMPTU URET
#  YONETICI olarak calistir.
#
#  ONKOSUL: 1_sunucu_kur.ps1 calisti VE "tailscale up" ile giris yapildi.
#
#  Ne yapar:
#    - Tailscale IP'yi bulur, Gitea adresini ona gore duzeltir
#    - Sherlock icin Gitea kullanicisi acar
#    - OYUN deposunu Gitea'da olusturur
#    - Projeyi Gitea'ya push'lar (GitHub yedek olarak kalir)
#    - Karsi PC'ye yapistirilacak hazir promptu uretir
# =====================================================================

# DIKKAT - 'Stop' bilerek KULLANILMIYOR:
# PowerShell 5.1'de native programlarin (git, gitea, tailscale) stderr'e
# yazdigi NORMAL ilerleme ve uyari mesajlari 'Stop' modunda olumcul hata
# sayilir ve script ortasinda durur. Ornegin "git push" ilerlemeyi stderr'e
# yazar. Bunun yerine kritik adimlarda $LASTEXITCODE acikca kontrol edilir.
$ErrorActionPreference = 'Continue'

$GiteaDir  = 'C:\Gitea'
$GiteaPort = 3000
$Proje     = 'C:\3D OYUN\OYUN'
$AdminUser = 'watson'
$SherlockUser = 'sherlock'
$DepoAdi   = 'OYUN'
$UnitySurum = '6000.2.14f1'

$giteaExe = "$GiteaDir\gitea.exe"
$appIni   = "$GiteaDir\custom\conf\app.ini"

function Adim($n,$t){ Write-Host "`n=== $n. $t ===" -ForegroundColor Cyan }
function Tamam($m){ Write-Host "  [OK] $m" -ForegroundColor Green }
function Bilgi($m){ Write-Host "  [..] $m" -ForegroundColor Gray }
function Uyari($m){ Write-Host "  [!] $m" -ForegroundColor Yellow }
function Dur($m){ Write-Host "HATA: $m" -ForegroundColor Red; exit 1 }

# Gitea'yi calistirip ciktisini metin olarak dondurur (stderr dahil).
function Gitea {
    param([Parameter(ValueFromRemainingArguments = $true)]$Arg)
    return (& $giteaExe @Arg 2>&1 | Out-String)
}

$pr = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $pr.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "HATA: Yonetici olarak calistir." -ForegroundColor Red; exit 1
}

# ===================================================================
Adim 1 "Tailscale baglantisi kontrolu"
# ===================================================================
# tailscale.exe PATH'te olmayabilir: bu pencere Tailscale kurulmadan once
# acildiysa PATH bayat kalir. Once dogrudan kurulum yolunu deneriz.
$tsExe = @(
    'C:\Program Files\Tailscale\tailscale.exe',
    'C:\Program Files (x86)\Tailscale\tailscale.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $tsExe) {
    $k = Get-Command tailscale -ErrorAction SilentlyContinue
    if ($k) { $tsExe = $k.Source }
}
if (-not $tsExe) { Dur "Tailscale kurulu degil. Once 1_sunucu_kur.ps1 calistir." }

$tsIp = (& $tsExe ip -4 2>$null | Select-Object -First 1)
if (-not $tsIp -or $tsIp -notmatch '^100\.') {
    Write-Host "HATA: Tailscale'e giris yapilmamis." -ForegroundColor Red
    Write-Host "  Once su komutu calistir ve tarayicidan giris yap:" -ForegroundColor Yellow
    Write-Host "      & '$tsExe' up" -ForegroundColor Cyan
    exit 1
}
$tsIp = $tsIp.Trim()
Tamam "Bu PC'nin Tailscale adresi: $tsIp"

$tsAd = (& $tsExe status --self --json 2>$null | ConvertFrom-Json).Self.DNSName
if ($tsAd) { $tsAd = $tsAd.TrimEnd('.'); Tamam "Tailscale adi: $tsAd" }

# ===================================================================
Adim 2 "Gitea adresini Tailscale'e gore ayarla"
# ===================================================================
$icerik = Get-Content $appIni -Raw
$yeniKok = "http://${tsIp}:$GiteaPort/"
if ($icerik -notmatch [regex]::Escape($yeniKok)) {
    $icerik = $icerik -replace '(?m)^ROOT_URL\s*=.*$', "ROOT_URL         = $yeniKok"
    $icerik = $icerik -replace '(?m)^DOMAIN\s*=.*$',   "DOMAIN           = $tsIp"
    Set-Content -Path $appIni -Value $icerik -Encoding utf8
    Restart-Service gitea
    Start-Sleep -Seconds 5
    Tamam "Gitea adresi guncellendi: $yeniKok"
} else {
    Tamam "Gitea adresi zaten dogru"
}

if ((Get-Service gitea).Status -ne 'Running') {
    Write-Host "HATA: Gitea calismiyor. Log:" -ForegroundColor Red
    Get-ChildItem "$GiteaDir\log\*.log" | Sort-Object LastWriteTime |
        Select-Object -Last 1 | ForEach-Object { Get-Content $_.FullName -Tail 30 }
    exit 1
}

# ===================================================================
Adim 3 "Sherlock kullanicisini olustur"
# ===================================================================
$sherlockSifreDosya = "$GiteaDir\SHERLOCK_SIFRE.txt"
$mevcut = Gitea admin user list --config $appIni --work-path $GiteaDir
if ($mevcut -match "(?m)^\s*\d+\s+$SherlockUser\s") {
    Tamam "Kullanici '$SherlockUser' zaten var"
    $sherlockSifre = if (Test-Path $sherlockSifreDosya) {
        (Get-Content $sherlockSifreDosya -Raw).Trim()
    } else { '<daha once uretildi - GIRIS_BILGILERI.txt bak>' }
} else {
    Add-Type -AssemblyName System.Web
    $sherlockSifre = [System.Web.Security.Membership]::GeneratePassword(18,3) -replace '[\\"''`$@:/]','x'
    $sonuc = Gitea admin user create --username $SherlockUser --password $sherlockSifre `
        --email "sherlock@oyun.local" --must-change-password=false `
        --config $appIni --work-path $GiteaDir
    if ($sonuc -notmatch 'successfully created') {
        Uyari "Beklenmedik cikti:"; Write-Host $sonuc -ForegroundColor DarkGray
    }
    Set-Content -Path $sherlockSifreDosya -Value $sherlockSifre -Encoding utf8
    Tamam "Kullanici '$SherlockUser' olusturuldu"
}

# ===================================================================
Adim 4 "Erisim anahtari + depo olustur"
# ===================================================================
$tokenAd = "kurulum-$(Get-Date -Format 'yyyyMMddHHmm')"
$tokenCikti = Gitea admin user generate-access-token --username $AdminUser `
    --token-name $tokenAd --scopes "write:repository,write:user,write:admin" `
    --config $appIni --work-path $GiteaDir
if ($tokenCikti -match '([a-f0-9]{40})') { $token = $Matches[1] }
else { Dur "Erisim anahtari alinamadi:`n$tokenCikti" }
Tamam "Erisim anahtari alindi"

$api = "http://127.0.0.1:$GiteaPort/api/v1"
$basliklar = @{ Authorization = "token $token"; 'Content-Type' = 'application/json' }

# Depo var mi?
$depoVar = $false
try {
    Invoke-RestMethod "$api/repos/$AdminUser/$DepoAdi" -Headers $basliklar | Out-Null
    $depoVar = $true
} catch { $depoVar = $false }

if ($depoVar) {
    Tamam "Depo '$AdminUser/$DepoAdi' zaten var"
} else {
    $govde = @{ name = $DepoAdi; private = $true; default_branch = 'main'
                description = 'Turk mitolojisi taktiksel RPG - Unity 6' } | ConvertTo-Json
    try {
        Invoke-RestMethod "$api/user/repos" -Method Post -Headers $basliklar `
            -Body $govde -ErrorAction Stop | Out-Null
    } catch { Dur "Depo olusturulamadi: $($_.Exception.Message)" }
    Tamam "Depo olusturuldu: $AdminUser/$DepoAdi"
}

# Sherlock'a yazma yetkisi ver
try {
    Invoke-RestMethod "$api/repos/$AdminUser/$DepoAdi/collaborators/$SherlockUser" `
        -Method Put -Headers $basliklar -Body (@{ permission = 'write' } | ConvertTo-Json) | Out-Null
    Tamam "Sherlock'a yazma yetkisi verildi"
} catch { Uyari "Yetki verilemedi (web arayuzunden elle eklenebilir): $($_.Exception.Message)" }

# ===================================================================
Adim 5 "Projeyi Gitea'ya gonder"
# ===================================================================
if (-not (Test-Path "$Proje\.git")) { Dur "Proje bulunamadi: $Proje" }
Push-Location $Proje

# GitHub'i 'github' adiyla yedege al, 'origin' Gitea olsun
$remotes = & git remote
if ($remotes -contains 'origin') {
    $originUrl = (& git remote get-url origin)
    if ($originUrl -match 'github\.com') {
        if ($remotes -notcontains 'github') { & git remote add github $originUrl }
        Tamam "GitHub 'github' adiyla yedek olarak korundu"
        & git remote set-url origin "http://${tsIp}:$GiteaPort/$AdminUser/$DepoAdi.git"
    } else {
        & git remote set-url origin "http://${tsIp}:$GiteaPort/$AdminUser/$DepoAdi.git"
    }
} else {
    & git remote add origin "http://${tsIp}:$GiteaPort/$AdminUser/$DepoAdi.git"
}
Tamam "origin -> Gitea, github -> GitHub yedek"

# Kimlik bilgisini Windows kasasina yaz (her push'ta sifre sorulmasin)
$credGirdi = "protocol=http`nhost=${tsIp}:$GiteaPort`nusername=$AdminUser`npassword=$token`n`n"
$credGirdi | & git credential approve 2>$null
Tamam "Giris bilgisi kaydedildi"

# LFS hazirla
& git lfs install --local 2>&1 | Out-Null
Tamam "Git LFS aktif"

# .gitattributes commit'lenmemisse commit'le
if (& git status --porcelain) {
    & git add -A
    & git commit -m "Sunucu kurulumu: .gitattributes (LFS + satir sonu), senkron scriptleri

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>" | Out-Null
    Tamam "Kurulum dosyalari commit'lendi"
}

Bilgi "Gitea'ya gonderiliyor (ilk gonderim biraz surer)..."
& git push -u origin main 2>&1 | Out-String | Write-Host
if ($LASTEXITCODE -ne 0) {
    Write-Host "HATA: Gitea'ya gonderilemedi." -ForegroundColor Red
    Pop-Location; exit 1
}
Tamam "Proje Gitea'da!"
Pop-Location

# ===================================================================
Adim 6 "Sherlock promptunu uret"
# ===================================================================
$klonUrl = "http://${tsIp}:$GiteaPort/$AdminUser/$DepoAdi.git"
$giteaWeb = "http://${tsIp}:$GiteaPort/"

$prompt = @"
Bu bilgisayari (adi: Sherlock) bir Unity oyun projesinin ikinci gelistirme
makinesi olarak kurmani istiyorum. Karsida "Watson" adinda bir sunucu PC var,
projenin tamami orada duruyor ve oraya baglanacagiz.

== BAGLANTI BILGILERI ==
  Sunucu Tailscale IP : $tsIp
  Gitea (git sunucusu): $giteaWeb
  Depo adresi         : $klonUrl
  Gitea kullanici     : $SherlockUser
  Gitea sifre         : $sherlockSifre
  Unity surumu        : $UnitySurum   (BIREBIR ayni olmali)

== YAPMANI ISTEDIKLERIM (sirayla) ==

1) Tailscale kur:
     winget install --id Tailscale.Tailscale --exact --accept-package-agreements --accept-source-agreements
   Kurulunca BANA "tailscale up" komutunu calistirmami soyle - tarayicidan
   giris yapmam gerekiyor. Watson ile AYNI Tailscale hesabina girecegim.
   Sonra dogrula: "tailscale status" ciktisinda $tsIp gorunmeli.

2) Baglantiyi test et: "$giteaWeb" adresine ulasabiliyor muyuz?
   (Test-NetConnection $tsIp -Port $GiteaPort)

3) Git ve Git LFS kur (yoksa):
     winget install --id Git.Git --exact
     git lfs install
   Sonra bana adimi ve e-postami sor, git kimligimi ayarla.

4) Moonlight kur (Watson'in ekranini uzaktan kullanmak icin):
     winget install --id MoonlightGameStreamingProject.Moonlight --exact
   Kurulunca soyle: Watson'da https://localhost:47990 adresinden Sunshine
   arayuzune girip PIN eslestirmesi yapmam gerekecek.

5) Projeyi klonla. Once bana hangi klasore koymak istedigimi sor
   (ornek: C:\3D OYUN\OYUN). Sonra:
     git clone $klonUrl "<klasor>"
   Kullanici adi/sifre sorulursa yukaridaki Gitea bilgilerini kullan.
   NOT: Depo Git LFS kullaniyor, klonlama sirasinda buyuk dosyalar da iner.

6) Unity Hub ve Unity $UnitySurum kurulu mu kontrol et.
   Kurulu degilse bana Unity Hub'i indirip bu surumu kurmam gerektigini soyle.
   SURUM BIREBIR AYNI OLMALI - farkli surum proje dosyalarini bozar ve
   karsi tarafta sonsuz cakisma yaratir.

7) Klonlanan klasorde "Docs/SUNUCU_KURULUM.md" dosyasini oku ve bana ozetle.

== BU PROJEDE CALISMA KURALLARI (ogren ve uy) ==

- Proje iki PC arasinda TEK KANALDAN senkronlanir: yukaridaki Gitea sunucusu.
  Dropbox/Drive gibi baska bir yolla dosya tasinmaz.

- Ise baslarken     :  .\al.ps1        (karsi taraftan geleni indirir)
  Is bitince        :  .\gonder.ps1 "ne yaptigimin ozeti"

- al.ps1'i Unity ACIKKEN CALISTIRMA. Once Unity'yi kapat.
  Sebep: Unity acikken sahne dosyasi degisirse Unity hafizasindaki eski
  hali diske geri yazar ve karsi tarafin isi kaybolur.

- Sahne (.unity) ve prefab dosyalari otomatik birlestirilemez.
  Is bolumu: Watson = Unity sahnesi/prefab/kod tarafi.
             Sherlock = denge, statlar, ScriptableObject/veri dosyalari.
  Ayni sahneye ayni anda iki kisi dokunmaz.

- Docs/GAME_DESIGN.md denge konusunda TEK dogruluk kaynagidir.
- Yeni is talepleri Docs/INBOX_TASKS.md dosyasindan gelir,
  kararlar Docs/DECISION_LOG.md dosyasina yazilir.

- ASSET dosyalari (fbx, png, ses) ASLA silinmez; kullanilmayacaksa
  "_Arsiv" klasorune tasinir. Kod silinebilir (git geri getirir).

Kurulum bitince bana kisa bir ozet ver: neler kuruldu, ne eksik kaldi.
"@

$promptDosya = "$Proje\Docs\Sunucu\SHERLOCK_PROMPT.txt"
Set-Content -Path $promptDosya -Value $prompt -Encoding utf8

# ===================================================================
Write-Host "`n#############################################" -ForegroundColor Magenta
Write-Host "#            KURULUM TAMAMLANDI             #" -ForegroundColor Magenta
Write-Host "#############################################`n" -ForegroundColor Magenta
Write-Host "  Gitea web arayuzu : $giteaWeb" -ForegroundColor White
Write-Host "  Kullanici         : $AdminUser  (sifre: $GiteaDir\GIRIS_BILGILERI.txt)" -ForegroundColor White
Write-Host "  Sunshine arayuzu  : https://localhost:47990" -ForegroundColor White
Write-Host ""
Write-Host "  SHERLOCK PROMPTU HAZIR:" -ForegroundColor Yellow
Write-Host "  $promptDosya" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Bu dosyanin icerigini kopyalayip karsi PC'de Claude Code'a" -ForegroundColor White
Write-Host "  yapistir - gerisini o halleder." -ForegroundColor White
Write-Host ""
Write-Host "  Panoya kopyalamak icin:" -ForegroundColor Gray
Write-Host "     Get-Content '$promptDosya' -Raw | Set-Clipboard" -ForegroundColor Gray
Write-Host ""
