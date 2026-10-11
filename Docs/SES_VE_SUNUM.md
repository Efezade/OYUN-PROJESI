# Ses ve sunum sistemi

11 Ekim 2026. Efe'nin ses, müzik, Bayterek sinematiği, sis, çöküş ve arayüz isteği.

## Ses ve müzik

`Assets/Audio/Original` altında 231 özgün kayıt vardır: 71 olay için üçer varyasyon
(213 WAV), 10 müzik ve 8 ortam döngüsü (OGG). Kaynak üretici
`../Tools/SunumTasarimi/compose_audio.py`; dosya envanteri `manifest.json`.
Dışarıdan alınmış kayıt veya örnek kullanılmadı. Müzikler ortak modal ezginin
bölgeye göre tempo, çalgı ve yoğunluk değişimleriyle düzenlendi. Menü, yedi bölge,
savaş ve boss ayrı temalardır; müzikler 24 ölçü / 96 vuruş uzunluğundadır.

`GameSoundDirector` oyun olaylarını dinler. Yüzeye göre adım, saldırı/hasar/ölüm,
büyü/iyileşme/kalkan, tur/raunt, görev, hasat, alışveriş, gelişim, ışınlanma, kule,
kök, deprem ve çöküş sesleri bağlıdır. `UISoundFeedback` uGUI seçim, üzerine gelme,
tıklama, kaydırıcı, aç/kapa ve sürüklemeyi; `ModernGUI` eski HUD düğmelerini kapsar.
Olayın kendisini yöneten sistemler `SoundSignals` üzerinden ses isteği gönderir.

`Assets/Data/Presentation/SoundLibrary.asset` bütün kayıt, seviye, varyasyon,
bekleme süresi, eşzamanlı ses sınırı ve müzik geçişlerini Inspector'da tutar.
24 seslik havuz kullanılır; art arda aynı varyasyon seçilmez. Müzik ve ortam
ikişer kanalla yumuşakça değişir. Menüde ve sinematikte müzik kısılır.
Ana ses, müzik, efekt, ortam ve arayüz sesleri ayrı ayarlanır ve PlayerPrefs'e yazılır.
Müzik streaming, ortam compressed-in-memory, kısa efektler decompress-on-load'dur.

## Bayterek kökleri

Yeni görev olayı kamerayı önce Bayterek'e yükseltir, sonra büyüyen kökün ucunu izler.
İlk harita kuruluşu sinematik başlatmaz. Kamera, hareket ve menü kontrolü geçici
olarak alınır; bitişte, savaş geçişinde, harita değişiminde veya component kapanışında
geri verilir. Esc / Boşluk sahneyi atlatır; kök büyümesi devam eder.
Sinematiğin açtığı görüş koridoru kalıcı keşif bilgisine yazılmaz.

Kökler üç kıvrımlı liften oluşan, kabuk çıkıntıları ve hafif özsu ışığı taşıyan
mesh'lerdir. Son Play geri bildiriminden sonra ana yarıçap 0,83 m; büyüme / çekilme
süreleri 0,32 / 0,12 saniye/karo oldu. Gövde yaklaşık üç karoda bir zeminin altına
dalıp yeniden çıkar. Uygun yerlerde ikinci karoya yan kök yayılır ve
**o karo da gerçekten yürünemez olur**. Oyuncu, düğümler, büyük yapılar ve gerekli
hedeflere erişim korunur. Ortak dalların sahipliği ayrı tutulur. Görev tamamlanınca
gövde ve yan kökler karo karo geri çekilir, hareket engeli de aynı sırayla kalkar.

`RootRuptureTile` geçilen karonun yerine altı kalkmış toprak parçası, çukur toprak
tabanı ve on savrulan taş içeren bir mesh koyar. Üst yüzey biyom rengini alır;
özgün karo/model dosyasını değiştirmez. Toprak tabanı gömülü kökü gerçekten örter.
Toz patlaması `RootRuptureBurst.prefab` ile ayarlanır. Mevcut kökten yeni bir dal
geçince toprak tekrar sarsılır. Bayterek modelinin kendisi gizlenmez.

Asıl görselin görünürlüğü `HexCell.SetVisualSuppressed` sahipliğiyle yönetilir:
ortak dallar, çöküş, onarım ve savaştan dönüş birbirlerinin gizlemesini bozamaz.
Son kök çekilince asıl karo geri döner. Havuz kullanılır; fizik cismi üretilmez.

`QuestRootCinematic` kamera süre/boyutlarını; `BayterekRoots.asset` kök ayarlarını;
`Art/Presentation/RootAwakening.prefab` toz ve ışığı Inspector'da sunar.

## Okunaklı ışık ve yumuşak zaman geçişi

Gün altı dilimdir: Sabah, Öğle, Öğleden Sonra, Akşam **gündüz**; Gece ve Gece Yarısı
**gece**. AP ve takvim dengesi değişmedi. `DayNightCycle._transitionSeconds=2.4`;
güneş/ay açısı, rengi, şiddeti, gölgesi, ortam ve gökyüzü birlikte SmoothStep ile
geçer. Arka arkaya AP harcamak o andaki gerçek değerlerden yeni hedefe yönelir.
Güneşin yatay/dikey açıları ayrı karışır; geçiş sırasında ışık ufkun altına dönmez.

Son Play geri bildiriminde önceki aydınlatma hâlâ fazla karanlıktı. `DayNightProfile.asset`
yeniden açıldı: gündüz ana ışık 1,7–2,1; gece 1,15–1,3. Gece ortam renkleri
(0,53–0,57; 0,62–0,66; 0,75–0,78) seviyesinde. Akşam sıcak, gece serin mavi kalır;
karolar ve ağaçların gölge yüzleri artık karanlıkta kaybolmaz. Paletler değiştirilmedi.
Biome Terrain materyallerinde `_AmbientLift=1.35` ve `_LightWrap=.16`, gökyüzü
dolgusunu ve yan yüzeylerin yumuşak ışığını düzenler; ekran üstüne beyaz perde eklenmez.
Sahnede kalan ikinci varsayılan Directional Light kapatıldı; nesnesi silinmedi.
`RenderSettings.sun` gün/gece sisteminin ışığına bağlıdır. Aksi durumda URP,
gece ışık kısılınca daha parlak sabit ışığı seçip hem gölgeyi sıçratıyor hem geceyi
gündüz ışığıyla gösteriyordu. Referans: [Unity URP ışık seçimi](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/universalrp-asset.html#lighting).

Gece sınırında sis ve sis rengi de 2,4 saniyede değişir. Yeni hacim sunumunda
`_BaseColor` sise göre siyaha çarpılmaz. Keşif, gece görüşü ve savaşta sisin
kapanması aynı kurallardadır.

## Geçici sınırsız ekonomi

`Assets/Data/Config/TestEconomy.asset` içindeki **Unlimited** açıktır. Beş öz türü,
üretim, karakter seviyesi, evrim/ağaç bedelleri, sunular, karo onarımı, market ve
stok yenileme ücretsizdir. Sahip olunan iksirler ve yol taşları tükenmez; sayaçlar
**Sınırsız** yazar. Eşyayı/potu edinmek için ücretsiz market kullanılabilir.

Gerçek banka ve `TotalGained` şişirilmez: zorunlu görev ekonomi eşikleri yalnız
toplanan gerçek özü sayar. AP, takvim, savaş manası, maksimum seviye ve gelişim
önkoşulları işlemeye devam eder. Unlimited kapatılınca gerçek miktarlar ve normal
tüketim geri gelir; tek ayar cüzdan, pot ve yol taşlarını birlikte yönetir.

Önceki kök/ekonomi turunun doğrulaması: `RootRuptureQA.Run` izole projede 300 kontrol / sıfır hata.
Gerçek AP ile altı dilimin öncesi/arası/sonu, yarım geçişte hedef değişimi ve
component kapatma/açma; kök/çöküş/arena/iyileşme; ekonomi ve normal moda dönüş
kontrol edildi. Render edilmiş nötr yüzeyin ölçülen parlaklığı öğlen 0,680,
gece yarısı 0,265: gece ayrımı ışık ayarından değil gerçek görüntüden doğrulandı.
Rapor: `../Tools/SunumTasarimi/kontrol/kok_ekonomi_isik.json`.

## Sis ve çöküş

`WorldFogEffect`, ana kamerada URP Render Graph geçişiyle derinlikten dünya
konumunu kurar; sekiz yükseklik örneğiyle sis yoğunluğunu toplar. Sis alanı,
FogOfWarManager'ın mevcut opaklığından küçük bir RGBAHalf dokuya yazılır; yalnız
değişen karelerde yüklenir. Üst üste gelen modeller, yüksek taçlar, kökler ve
saydam dünya efektleri aynı sis perdesinden geçer. HUD ve veri tabanlı minimap etkilenmez.

Eski `alpha > .92` renderer kapatma eşiği kaldırıldı. Ağaç/çevre/öz/çürüme/kovuk
görselleri de `CanRenderWorldModel` üzerinden hacme bırakılır; `IsKnown` ve rota
kuralları değişmez. Yaklaşırken önce soluk siluet, sonra renk ve yüzey ayrıntısı
açılır. İpucu bandının dış ucundaki ani opaklık değişimi de yumuşatıldı.
Keşfedilmiş yüzeye öndeki bulut taşmaz; sisin üstüne yükselen taçlar yine örtülür.

Inspector: WorldFogEffect yüksekliği 12 m, yoğunluğu 0,24, gündüz/gece sis renkleri;
FogOfWarManager yürüyüş solma hızı 1,1/sn, geçiş bandı 2,6 karo, ipucu bandı 1,8 karo.
Gece sınırında 2,4 sn geçiş sürer. Eski Mist prefabı efekt devre dışı kalırsa yedektir.
Tam gizli geometri de bellekte/render'da tutulur; ani belirmeyi önlemek için çizim
maliyeti arttı. Bu turdaki kontroller FPS profili yerine geçmez.

Deprem karoları rastgele titreşmek yerine birlikte sarsılıp aşağı çöker. Eski sert
kırmızı çemberler, düzensiz ve yer yer dağılan toprak/toz cephesine dönüştürüldü.
Dalganın oyun kapsamı ve çöküş takvimi değişmedi. Silinen karonun yerinde karanlık
merkezli, ince dönen izleri ve açılan kenarı olan bir boşluk yüzeyi kalır.
Geri kazanılan karoda bu yüzey kalkar. Görsel boşluk yürünebilir alan oluşturmaz.

## Arayüz

Kitap, yetenek/karolar ağacı, çanta/KAM/potlar, harita, ayarlar ve oyun HUD'ları
aynı koyu petrol, fildişi ve ölçülü altın temasını kullanır. Tema verisi
`Assets/Data/Presentation/Theme.asset` içindedir. Kullanıcının çizimleri, karakter
portreleri ve işlevsel simgeler korunur. Eski kâğıt zeminler, çerçeveler, düğme
yüzeyleri, yazı renkleri, seçim durumları ve sekmeler yenilenmiştir.

Ayarlar ekranı kaydırılabilir içerik, görünür scrollbar ve sekiz kaydırıcı içerir:
beş ses kanalı, parlaklık, harita ve savaş yakınlığı. İnce çubuğun çevresinde geniş
tıklama alanı bulunur. Gizli ayar paneli de kurulumda bulunur; eski kurulumun
inactive paneli atlaması giderildi. Runtime yenilemeler tema renklerini geri bozmaz.

Görev ve gün şeritlerinin kesilen metinleri, HUD üstüne taşan dünya etiketleri,
yerleştirme panelindeki yatay taşma ve gizlenen geri dönüş düğmesi düzeltildi.
Yerleştirme cüzdanı artık tariflerde kullanılan özleri gösterir. Kart seçimi ve tur
sırası da tema renklerini kullanır. Yetenek ağacı sekmelere ve başlığa çarpmaz.

## Kurulum ve doğrulama

`SceneSetupTool.FullSetup()` içinde `SetupPresentation()` vardır. Ayrı menü:
`TacticalRPG > Sunum - Ses, Kok, Sis ve UI Kur`. Yeni ses kaydı eklemek mümkün;
mevcut kütüphane ayarları yeniden kurulumda korunur. Editör geçişi eski açık sahneyi
değiştirmeden önce `Temp/PresentationBeforeUpgrade/` içine kaydedilmemiş düzenlemeler
dahil bir kopya alır. Ana sahne `Assets/Scenes/xd.unity`.

Revision 3 geçişi yeni sis bileşenini ana kameraya bağlar; tekrarlı kurulum mevcut
sis ince ayarlarını korur. `FogLightingQA.Run` yürüyüş yaklaşımını ve sis örtmesini,
`RootRuptureQA.Run` gerçek AP geçişleri ve kök/arena dönüşünü kontrol eder.
QA girişleri `PresentationQA.Run` ve `BiomeWorldQA.Run` dahil yalnız işaretlenmiş ayrı
test projesinde çalıştırılır. Ana projede açık Unity varken ikinci editör açılmaz.
Raporlar ve görseller `../Tools/SunumTasarimi/kontrol/` ve `onizleme/` altındadır.
Teknik ses kontrolü bütün dosyaları çözüp kırpılma, sessizlik, sonlu örnekler ve
döngü birleşimlerini ölçer; insanın kulakla dinleme/onay vermesi yerine geçmez.

Son sis/ışık turu: 207 sis kontrolü + 311 kök/AP/ekonomi regresyon kontrolü,
toplam **518 / 0 hata**. Windows derlemesi ve dokuz karoluk gerçek yürüyüş geçti;
altı gerçek AP dilimi HUD ile yakalandı. 17 metrelik test yapı ve gerçek model
örtme deneyi de geçti. Rapor `kontrol/sis-isik/final_validation.json`, sessiz
yürüyüş videosu `onizleme/sis-isik/siste-yuruyus.mp4` altında.
