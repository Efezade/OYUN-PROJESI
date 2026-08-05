# Sunucu Kurulumu ve İki-PC Çalışma Düzeni

Bu proje iki bilgisayar arasında paylaşılıyor:

| PC | Rol | Sorumluluk |
|---|---|---|
| **Watson** (`DESKTOP-M01KKSE`) | Sunucu + geliştirme | Unity sahnesi, prefab, kod, hikâye |
| **Sherlock** | İkinci geliştirme makinesi | Denge, statlar, veri/ScriptableObject |

Watson aynı zamanda **git sunucusunu** barındırıyor. Tüm senkronizasyon oradan geçer.

---

## Kurulu sistem

| Bileşen | Ne işe yarar | Adres |
|---|---|---|
| **Tailscale** | İki PC'yi şifreli özel ağa alır. Port yönlendirme gerekmez, farklı şehirden bile çalışır. | — |
| **Gitea** | Kendi git sunucumuz. Kota yok, LFS dahil. | `http://<tailscale-ip>:3000/` |
| **Sunshine** | Watson'ın ekranını uzaktan kullandırır (RTX 4060 donanım encode). | `https://localhost:47990` |
| **RDP** | Hızlı yönetim için dahili uzak masaüstü. | port 3389 |

**Güvenlik:** Bu servislerin hiçbiri internete açık değil. Güvenlik duvarı kuralları
yalnızca Tailscale ağına (`100.64.0.0/10`) izin veriyor. Modemde port açılmadı.

---

## Günlük kullanım

Tek bilmen gereken iki komut. İkisi de proje klasöründe çalışır:

```powershell
.\al.ps1                          # işe başlarken: karşı taraftan geleni indir
.\gonder.ps1 "ne yaptığımın özeti"  # iş bitince: yaptıklarını gönder
```

`gonder.ps1` sırayla şunları yapar: değişiklikleri commit'ler → karşıdan gelen
varsa önce onu alır → Gitea'ya gönderir → GitHub'a yedekler.

### Uyulması gereken kurallar

1. **`al.ps1`'i Unity açıkken çalıştırma.** Script zaten engelliyor. Sebep: Unity
   açıkken sahne dosyası diskte değişirse, Unity hafızasındaki eski hali geri
   yazar ve karşı tarafın işi kaybolur.

2. **Aynı sahneye aynı anda iki kişi dokunmaz.** `.unity` ve `.prefab` dosyaları
   otomatik birleştirilemez — çakışırsa biri işini kaybeder. İş bölümü bunu
   doğal olarak önlüyor: Watson sahne/prefab, Sherlock veri/denge.

3. **Unity sürümü birebir aynı olmalı:** `6000.2.14f1`. Farklı sürüm proje
   dosyalarını her açılışta değiştirir ve bitmeyen çakışma üretir.

4. **Asset silinmez.** Kullanılmayacak FBX/texture `_Arsiv` klasörüne taşınır.
   Kod silinebilir — git geri getirir.

---

## Yedekleme — üç kopya

Gitea tek diskte duruyor, ama proje verisi üç yerde birden:

1. **Gitea sunucusu** (Watson) — ana kopya
2. **GitHub** (`Efezade/OYUN-PROJESI`) — `gonder.ps1` her seferinde buraya da yedekler
3. **Sherlock'un klonu** — tam geçmişiyle birlikte

Ek olarak her Pazar 03:00'te `C:\Gitea_Yedek` altına tam Gitea yedeği alınır
(kullanıcılar, ayarlar, depolar). Son 4 yedek saklanır.

> Ayda bir `C:\Gitea_Yedek` klasörünü harici diske kopyalarsan disk arızasına
> karşı da tam korunmuş olursun.

---

## Sorun giderme

**"Sunucuya ulaşılamadı"**
```powershell
tailscale status          # Watson listede görünüyor mu?
Get-Service gitea         # Running olmalı
```
Watson PC kapalıysa hiçbir şey çalışmaz — açık olması gerekir.

**Gitea başlamıyor**
```powershell
Get-Content C:\Gitea\log\gitea.log -Tail 30
Restart-Service gitea
```

**Çakışma çıktı**
```powershell
git status            # hangi dosya çakıştı
git rebase --abort    # vazgeç, hiçbir şey kaybolmaz
```
Sahne dosyası çakıştıysa: kimin sürümünün kalacağına karar verin, diğeri
işini o sürümün üstüne yeniden yapsın. Otomatik birleştirme denemeyin.

**Uzaktan ekran (Moonlight) bağlanmıyor**
Sunshine servisi çalışıyor mu (`Get-Service SunshineService`), ve Watson'da
`https://localhost:47990` üzerinden PIN eşleştirmesi yapıldı mı?

---

## Henüz yapılmadı — sonraya bırakıldı

**Uzaktan açma (Wake-on-LAN).** Şu an Watson'ın sürekli açık olması gerekiyor
(uyku kapatıldı, kapak kapanınca çalışmaya devam ediyor).

Uzaktan uyandırma için gerekenler:
- Ethernet **kablosu** takılı olmalı — Wi-Fi ile uyandırma laptoplarda çalışmaz
  (Realtek kartın desteği var, sürücü ayarı kurulumda açıldı)
- BIOS'ta "Wake on LAN" / "Deep Sleep Control" açılmalı
- İnternetten uyandırmak için ev ağında 7/24 açık bir cihaz gerekir —
  eski bir Android telefon + Tailscale + WoL uygulaması yeterli

Fast Startup kurulumda kapatıldı, o hazırlık tamam.

---

## Kurulum scriptleri

`Docs/Sunucu/` altında, sırayla yönetici olarak çalıştırılır:

| Script | Ne yapar |
|---|---|
| `1_sunucu_kur.ps1` | Tailscale, Gitea, Sunshine, RDP, güvenlik duvarı, güç ayarları |
| *(elle)* `tailscale up` | Tarayıcıdan Tailscale girişi |
| `3_bitir_ve_prompt_uret.ps1` | Depoyu oluşturur, projeyi gönderir, Sherlock promptunu üretir |
| `2_yedek_kur.ps1` | Haftalık otomatik yedekleme görevi |

Şifreler: `C:\Gitea\GIRIS_BILGILERI.txt` ve `C:\Gitea\SHERLOCK_SIFRE.txt`
