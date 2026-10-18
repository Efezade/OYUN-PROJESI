# Kam — A/B savaş laboratuvarı

18 Ekim 2026. Yeni savaş yönünü seçmek için iki küçük, bağımsız oynanabilir prototip. Ana oyun demosu veya final savaş kararı değildir.

## Açma

Windows: `C:\3D OYUN\Tools\SavasPrototipleri\BASLAT.cmd`.

Uygulama: `C:\3D OYUN\OYUN\Builds\CombatLab\KamCombatLab.exe`. Taşımak için yalnız exe değil, **CombatLab klasörünün tamamı** gerekir. Derleme Git dışında tutulur; kaynaklar aşağıdadır.

Unity 6000.2.14f1: **TacticalRPG → Prototipler → A-B Savas Laboratuvarini Ac**, ardından Play. Sahne `Assets/Scenes/Prototypes/CombatComparison.unity`. Ana `xd.unity` sahnesi bu test için değiştirilmedi. Menü mevcut kaydedilmemiş sahneyi korumak için Unity'nin kayıt sorusunu gösterir.

## Aynı kalanlar ve değişenler

Her iki yaklaşım: Kam, muhafız, okçu; aynı altıgen arena ve görüşü kesen iki sütun; aynı üç dalga ve son muhafız; dalga 1 ve 2 sonrasında üç seçenekten bir augment. Okçu işaret bırakır, Kam çekimle gruplayıp işaretler; patlama ve müttefik vuruşları işaret zincirini tetikler.

| | A · Hızlı taktik | B · Gerçek zaman |
|---|---|---|
| Kontrol | Birlikleri istediğin sırayla yönet | Kam hareketi oyuncuda, ekip destek saldırılarında |
| Hareket | Birim başına turda bir hareket | WASD/oklar; sürekli hareket |
| Eylem | Bir saldırı veya Kam büyüsü | Normal saldırılar otomatik; Q/E elle |
| Rakip | Turu bitirince hızlı düşman sırası | Kırmızı alanla hazırlanan, kaçılabilen saldırılar |
| Kaynak | Tur başında mana | Zamana bağlı mana ve büyü bekleme süreleri |

Bu farklar nedeniyle iki mod matematiksel olarak birebir eşit dengede değildir. Karşılaştırma kontrol hissi ve karar ritmini test eder. Son denge veya oyuncu tercihi otomatik testlerden çıkarılamaz.

## Kontroller

- Menüden A veya B seç. Üstte **Diğer yaklaşım** anında yeni deneme başlatır; bitmemiş girişimler sonuç olarak kaydedilmez.
- **A:** 1/2/3 veya birlik seçimi; yeşil karoya tıklayarak hareket, düşmana tıklayarak saldırı. Q ardından hedef tıkla; E Kam çevresinde patlatır. SPACE düşman turunu başlatır. Birlik sırasını serbestçe değiştir.
- **B:** WASD/oklar Kam'ı yürütür; düşmana tıklama destek ekibinin odak hedefini değiştirir. Q imleç çevresine çekim, E Kam çevresinde patlama. Ekip normal saldırıları otomatik yapar. Kırmızı alanlardan çık.
- ESC duraklatır veya çekim hedeflemesini iptal eder. B'de SPACE de duraklatır. Sağ tık hedeflemeyi iptal eder.
- Her iki modda düğmelerle büyü/hedefleme, yeniden deneme ve mod değiştirme kullanılabilir. Alt+F4 uygulamayı kapatır.
- Uygulama odağı kaybedince savaş duraklar; geri gelince **Devam** düğmesine bas.

## Kısa karşılaştırma oturumu

Önce A→B; sonra B→A dene. İlk seferde öğrenme avantajını tek moda yazmamak için sırayı değiştir. Her biten denemenin sonunda tekrar oynama isteğini 1–5 puanla.

Sorular: Ne zaman güçlü hissettin? Hangi seçim sonucu değiştirdi? Bekleme mi, karar mı daha fazlaydı? Kaybın nedenini anladın mı? Son denemeyi hemen tekrar etmek istiyor musun? Daha hızlı biten modu otomatik kazanan sayma.

Uygulama sonuç ekranında süre, komut, zincir, alınan hasar ve son denemeleri gösterir. Kayıt `Application.persistentDataPath/CombatLab/comparison.json` içindedir; kampanya kaydına yazmaz. Otomatik görsel yakalamalar oyuncu sonucu sayılmaz. Sonuçlar buluta gönderilmez.

## Uygulanan / uygulanmayan

Uygulanan: iki mod, arena, hareket/görüş engeli, işaret–çekim–zincir, üç dalga, boss, iki augment seçimi, zafer/yenilgi, yeniden deneme, duraklatma, yerel karşılaştırma kaydı, mevcut karakter modelleri ve basit ses/efekt geri bildirimi.

Henüz uygulanmayan yeni yönler: silah aileleri ve kalıcı ilerleme, satın alma/faiz, zırh ve potlar, mabetten dönüş, yeni hikâye/sekiz harita, final saldırı animasyonları ve final sanat. Ana oyundaki eski sistemler arşiv ve mevcut sahnede korunur. Karakterler bu prototipte mevcut modellerdir; hareket/görsel saldırı kalitesi final oyun hissini temsil etmez.

## Doğrulama ve kaynaklar

- 39 simülasyon kontrolü: geçersiz hareket/büyü, eylem sınırı, görüş, işaret zinciri, mana, duraklatma, yeniden başlama, augment geçişleri, zafer ve yenilgi. `Logs/combat-lab-validation.json`.
- Üç dalga geçiş kontrolü yüksek can fixture'ı kullanır. Normal denge veya eğlence doğrulaması değildir.
- Windows geliştirme derlemesi Unity 6000.2.14f1 ile oluşturuldu. Derleme günlüğü `Logs/combat-lab-build.log`.
- Görsel yakalama: `--lab-capture <mutlak.png>` ve B için `--lab-realtime`. Gizli pencerede ekran tamponu çizilmeyebildiğinden aynı kamera ve HUD RenderTexture üzerinden yakalanır. Bu, kullanıcı girdisiyle elle oynama testi değildir.
- Kod: `Assets/Scripts/Prototypes/CombatLab/`. Editör kurulum/doğrulama: `Assets/Scripts/Editor/CombatLabSetup.cs`, `CombatLabValidation.cs`.
- Denge/görsel ayarları: `Assets/Data/Prototypes/CombatLab.asset`. İzole sanat/prefablar: `Assets/Art/Prototypes/CombatLab/`.
- `SceneSetupTool` yalnız yeni prototip veri varlıklarını kurulum zincirine ekler; ana sahneye savaş laboratuvarı eklemez.

Yeni tasarım önerileri: [TASARIM_TASLAGI.md](TASARIM_TASLAGI.md). Modelci föyü: `C:\3D OYUN\Tools\SavasPrototipleri\ModelciPaketi\IS_FOYU.html`.

Eski sürüm: `C:\3D OYUN_ARSIV\2026-10-18_Degisiklik_Oncesi\OKU.md`. Ana proje commit'i `d675d124933d8117580b27f13426f0758a0a7d0c`, Unity commit'i `56500ed252daef0561dcd72a72d7022205278ab1`. ZIP bütünlüğü ve LFS nesneleri `DOGRULAMA.json` ile doğrulandı. Yeni çalışma dalı `codex/combat-comparison`; yayın veya uzak depo senkronu yapılmadı.
