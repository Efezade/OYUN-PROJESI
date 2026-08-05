# =====================================================================
#  OYUN PROJESI - SUNUCU KURULUMU (Watson PC)
#  Bu scripti YONETICI olarak calistir. Tekrar calistirilabilir.
#
#  Kurar:
#    1) Tailscale  - iki PC arasi ozel sifreli ag (ucretsiz)
#    2) Gitea      - kendi git sunucun, sinirsiz LFS (ucretsiz)
#    3) Sunshine   - uzaktan ekran kontrolu, RTX 4060 donanim encode
#    4) RDP        - dahili uzak masaustu (sadece Tailscale agina acik)
#    5) Guc ayarlari - sunucu uykuya dalmasin
# =====================================================================

$ErrorActionPreference = 'Stop'

# --- Ayarlar -------------------------------------------------------
$GiteaDir     = 'C:\Gitea'
$GiteaPort    = 3000
$AdminUser    = 'watson'
$AdminEmail   = 'kardelenefezade1@gmail.com'
$TailscaleNet = '100.64.0.0/10'   # Tailscale'in CGNAT araligi
# -------------------------------------------------------------------

function Adim($n, $t) { Write-Host "`n=== $n. $t ===" -ForegroundColor Cyan }
function Tamam($m)    { Write-Host "  [OK] $m" -ForegroundColor Green }
function Bilgi($m)    { Write-Host "  [..] $m" -ForegroundColor Gray }
function Uyari($m)    { Write-Host "  [!!] $m" -ForegroundColor Yellow }

# Gitea'yi guvenli calistirir.
# Neden gerekli: PowerShell 5.1'de $ErrorActionPreference='Stop' iken bir
# native program stderr'e TEK SATIR yazsa bile bu olumcul hata sayilir.
# Gitea normal calisirken de stderr'e uyari yazar (ornek: SCRIPT_TYPE "bash"
# is not on PATH). Bu sarmalayici olmadan script zararsiz uyarida duruyor.
function Gitea {
    param([Parameter(ValueFromRemainingArguments = $true)]$Arg)
    $eski = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try     { $cikti = & $giteaExe @Arg 2>&1 | Out-String }
    finally { $ErrorActionPreference = $eski }
    return $cikti
}

# --- Yonetici kontrolu ---------------------------------------------
$pr = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $pr.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "HATA: Bu script YONETICI olarak calistirilmali." -ForegroundColor Red
    Write-Host "PowerShell'e sag tik -> 'Yonetici olarak calistir' ile ac, sonra tekrar dene." -ForegroundColor Red
    exit 1
}

Write-Host "`n#############################################" -ForegroundColor Magenta
Write-Host "#   OYUN PROJESI - SUNUCU KURULUMU          #" -ForegroundColor Magenta
Write-Host "#############################################" -ForegroundColor Magenta


# ===================================================================
Adim 1 "Tailscale kurulumu"
# ===================================================================
if (Get-Command tailscale -ErrorAction SilentlyContinue) {
    Tamam "Tailscale zaten kurulu"
} else {
    Bilgi "winget ile indiriliyor..."
    winget install --id Tailscale.Tailscale --exact --silent `
        --accept-package-agreements --accept-source-agreements
    $env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' +
                [Environment]::GetEnvironmentVariable('Path','User')
    Tamam "Tailscale kuruldu"
}


# ===================================================================
Adim 2 "Sunshine kurulumu (uzaktan ekran)"
# ===================================================================
if (Get-Service -Name 'SunshineService' -ErrorAction SilentlyContinue) {
    Tamam "Sunshine zaten kurulu"
} else {
    Bilgi "winget ile indiriliyor..."
    winget install --id LizardByte.Sunshine --exact --silent `
        --accept-package-agreements --accept-source-agreements
    Tamam "Sunshine kuruldu (arayuz: https://localhost:47990)"
}


# ===================================================================
Adim 3 "Gitea kurulumu (kendi git sunucun)"
# ===================================================================
New-Item -ItemType Directory -Force -Path $GiteaDir, "$GiteaDir\custom\conf", `
    "$GiteaDir\data", "$GiteaDir\repositories", "$GiteaDir\log" | Out-Null

$giteaExe = "$GiteaDir\gitea.exe"
if (Test-Path $giteaExe) {
    Tamam "Gitea binary zaten var ($((Gitea --version).Trim()))"
} else {
    Bilgi "En son surum GitHub'dan sorgulaniyor..."
    $rel = Invoke-RestMethod 'https://api.github.com/repos/go-gitea/gitea/releases/latest' `
        -Headers @{ 'User-Agent' = 'oyun-sunucu' }
    $asset = $rel.assets | Where-Object { $_.name -match 'gitea-.*-windows-4\.0-amd64\.exe$' } |
             Select-Object -First 1
    if (-not $asset) { throw "Gitea Windows binary bulunamadi" }
    Bilgi "Indiriliyor: $($asset.name) ($([math]::Round($asset.size/1MB,1)) MB)"
    Invoke-WebRequest $asset.browser_download_url -OutFile $giteaExe -UseBasicParsing
    Tamam "Gitea $($rel.tag_name) indirildi"
}

$appIni = "$GiteaDir\custom\conf\app.ini"
if (Test-Path $appIni) {
    Tamam "app.ini zaten var, dokunulmuyor"
} else {
    Bilgi "Gizli anahtarlar uretiliyor..."
    $secretKey  = (& $giteaExe generate secret SECRET_KEY).Trim()
    $intToken   = (& $giteaExe generate secret INTERNAL_TOKEN).Trim()
    $jwtSecret  = (& $giteaExe generate secret JWT_SECRET).Trim()
    $lfsJwt     = (& $giteaExe generate secret JWT_SECRET).Trim()
    $hostName   = $env:COMPUTERNAME

    # Servis LocalSystem olarak calisacak -> RUN_USER buna uymali
    $runUser = 'NT AUTHORITY\SYSTEM'

    @"
APP_NAME  = OYUN Projesi Sunucusu
RUN_USER  = $runUser
RUN_MODE  = prod
WORK_PATH = $GiteaDir

[server]
PROTOCOL         = http
DOMAIN           = $hostName
HTTP_ADDR        = 0.0.0.0
HTTP_PORT        = $GiteaPort
ROOT_URL         = http://${hostName}:$GiteaPort/
DISABLE_SSH      = true
OFFLINE_MODE     = true
LFS_START_SERVER = true
LFS_JWT_SECRET   = $lfsJwt
APP_DATA_PATH    = $GiteaDir\data

[database]
DB_TYPE = sqlite3
PATH    = $GiteaDir\data\gitea.db

[repository]
ROOT             = $GiteaDir\repositories
DEFAULT_BRANCH   = main
; Unity projeleri buyuk olabilir - limitleri kaldir
[repository.upload]
FILE_MAX_SIZE = 4096
MAX_FILES     = 100

[security]
INSTALL_LOCK   = true
SECRET_KEY     = $secretKey
INTERNAL_TOKEN = $intToken
PASSWORD_COMPLEXITY = off

[oauth2]
JWT_SECRET = $jwtSecret

[service]
DISABLE_REGISTRATION              = true
REQUIRE_SIGNIN_VIEW               = true
DEFAULT_KEEP_EMAIL_PRIVATE        = true
ENABLE_NOTIFY_MAIL                = false

[mailer]
ENABLED = false

[log]
MODE      = file
LEVEL     = info
ROOT_PATH = $GiteaDir\log

[cron.archive_cleanup]
ENABLED = true
"@ | Set-Content -Path $appIni -Encoding utf8

    Tamam "app.ini olusturuldu"
}

# --- Veritabanini hazirla ------------------------------------------
Bilgi "Veritabani semasi hazirlaniyor..."
Gitea migrate --config $appIni --work-path $GiteaDir | Out-Null
Tamam "Veritabani hazir"

# --- Admin kullanici -----------------------------------------------
$krtDosya = "$GiteaDir\GIRIS_BILGILERI.txt"
$mevcut = Gitea admin user list --config $appIni --work-path $GiteaDir
if ($mevcut -match "(?m)^\s*\d+\s+$AdminUser\s") {
    Tamam "Kullanici '$AdminUser' zaten var"
} else {
    Add-Type -AssemblyName System.Web
    $sifre = [System.Web.Security.Membership]::GeneratePassword(18, 3) -replace '[\\"''`$]', 'x'
    $sonuc = Gitea admin user create --admin --username $AdminUser --password $sifre `
        --email $AdminEmail --must-change-password=false `
        --config $appIni --work-path $GiteaDir
    if ($sonuc -notmatch 'successfully created|has been successfully created') {
        Uyari "Kullanici olusturma ciktisi beklenmedik:"
        Write-Host $sonuc -ForegroundColor DarkGray
    }

    @"
GITEA GIRIS BILGILERI  -  BU DOSYAYI GUVENDE TUT
=================================================
Adres    : http://$env:COMPUTERNAME`:$GiteaPort/
Kullanici: $AdminUser
Sifre    : $sifre

Sherlock icin ikinci kullaniciyi web arayuzunden olustur:
  Site Administration -> Identity & Access -> User Accounts -> Create User
"@ | Set-Content -Path $krtDosya -Encoding utf8

    Tamam "Admin kullanici olusturuldu -> sifre: $krtDosya"
}

# --- Windows servisi -----------------------------------------------
$svc = Get-Service -Name 'gitea' -ErrorAction SilentlyContinue
if ($svc) {
    Tamam "Gitea servisi zaten kayitli"
} else {
    Bilgi "Windows servisi kaydediliyor..."
    $bin = "`"$giteaExe`" web --config `"$appIni`" --work-path `"$GiteaDir`""
    New-Service -Name 'gitea' -BinaryPathName $bin -DisplayName 'Gitea (OYUN sunucusu)' `
        -Description 'OYUN projesi icin kendi git sunucusu' -StartupType Automatic | Out-Null
    # Cokerse otomatik yeniden baslat (bunun cmdlet karsiligi yok)
    & sc.exe failure gitea reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
    Tamam "Servis kaydedildi"
}

Bilgi "Servis baslatiliyor..."
Start-Service gitea -ErrorAction SilentlyContinue
Start-Sleep -Seconds 4
$svc = Get-Service -Name 'gitea'
if ($svc.Status -eq 'Running') {
    Tamam "Gitea CALISIYOR -> http://localhost:$GiteaPort/"
} else {
    Uyari "Gitea baslatilamadi. Son log satirlari:"
    Get-ChildItem "$GiteaDir\log\*.log" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime | Select-Object -Last 1 |
        ForEach-Object { Get-Content $_.FullName -Tail 25 }
}


# ===================================================================
Adim 4 "RDP (uzak masaustu) acilmasi"
# ===================================================================
Set-ItemProperty 'HKLM:\System\CurrentControlSet\Control\Terminal Server' `
    -Name fDenyTSConnections -Value 0
Set-ItemProperty 'HKLM:\System\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp' `
    -Name UserAuthentication -Value 1
Tamam "RDP acildi (NLA guvenligi aktif)"


# ===================================================================
Adim 5 "Guvenlik duvari - SADECE Tailscale agina ac"
# ===================================================================
# Onemli: bu servisler internete DEGIL, sadece 100.64.0.0/10 (Tailscale)
# araligina acilir. Disaridan erisim mumkun degil.

$kurallar = @(
    @{ Ad = 'OYUN-Gitea';    Port = $GiteaPort },
    @{ Ad = 'OYUN-RDP';      Port = 3389 },
    @{ Ad = 'OYUN-Sunshine'; Port = 47984,47989,47990,48010 }
)
foreach ($k in $kurallar) {
    Get-NetFirewallRule -DisplayName $k.Ad -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    New-NetFirewallRule -DisplayName $k.Ad -Direction Inbound -Action Allow `
        -Protocol TCP -LocalPort $k.Port -RemoteAddress $TailscaleNet -Profile Any | Out-Null
    Tamam "$($k.Ad) -> port $($k.Port -join ',') (sadece Tailscale)"
}
# Sunshine ayrica UDP kullanir
Get-NetFirewallRule -DisplayName 'OYUN-Sunshine-UDP' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -DisplayName 'OYUN-Sunshine-UDP' -Direction Inbound -Action Allow `
    -Protocol UDP -LocalPort 47998,47999,48000,48010 -RemoteAddress $TailscaleNet -Profile Any | Out-Null
Tamam "OYUN-Sunshine-UDP -> (sadece Tailscale)"


# ===================================================================
Adim 6 "Guc ayarlari - sunucu uyumasin"
# ===================================================================
powercfg /change standby-timeout-ac 0      # fise takiliyken uyuma
powercfg /change hibernate-timeout-ac 0    # hazirda bekletme yok
powercfg /change monitor-timeout-ac 10     # ekran 10 dk sonra kapansin (sorun degil)
powercfg /change disk-timeout-ac 0
Tamam "Fise takiliyken uyku kapatildi"

# Kapak kapaninca hicbir sey yapma (fise takiliyken)
powercfg /setacvalueindex SCHEME_CURRENT 4f971e89-eebd-4455-a8de-9e59040e7347 `
    5ca83367-6e45-459f-a27b-476b1d01c936 0
powercfg /setactive SCHEME_CURRENT
Tamam "Kapak kapaninca calismaya devam edecek"

# Fast Startup kapali olmali (ileride Wake-on-LAN icin sart)
Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' `
    -Name HiberbootEnabled -Value 0
Tamam "Fast Startup kapatildi (Wake-on-LAN hazirligi)"

# Ethernet kartinin uyandirma yetkisi (kablo takilinca aktif olur)
try {
    $eth = Get-NetAdapter | Where-Object { $_.InterfaceDescription -match 'Realtek.*GbE' }
    if ($eth) {
        Enable-NetAdapterPowerManagement -Name $eth.Name -WakeOnMagicPacket -ErrorAction Stop
        Tamam "Ethernet Wake-on-LAN acildi ($($eth.Name)) - kablo takilinca calisir"
    }
} catch { Uyari "Ethernet WoL ayarlanamadi (BIOS'tan da acilmali): $($_.Exception.Message)" }


# ===================================================================
Adim 7 "Ozet"
# ===================================================================
Write-Host "`n#############################################" -ForegroundColor Magenta
Write-Host "#              KURULUM BITTI                #" -ForegroundColor Magenta
Write-Host "#############################################`n" -ForegroundColor Magenta

Write-Host "SIRADAKI ADIM - SENIN YAPMAN GEREKEN:" -ForegroundColor Yellow
Write-Host "  Tailscale'e giris yap (tarayici acilacak):" -ForegroundColor White
Write-Host "      tailscale up" -ForegroundColor Cyan
Write-Host "  Sonra Tailscale IP'ni ogren:" -ForegroundColor White
Write-Host "      tailscale ip -4" -ForegroundColor Cyan
Write-Host ""
Write-Host "Gitea giris bilgileri: $krtDosya" -ForegroundColor White
Write-Host ""
