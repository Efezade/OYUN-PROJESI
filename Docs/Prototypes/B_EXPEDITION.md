# B · Büyüyen Sefer — oynanabilir prototip

18 Ekim 2026. Efe'nin seçtiği gerçek zamanlı B yönünün devamı. Bu küçük deney, büyük arena + savaş içinde büyüme + savaşlar arasında kombinasyon sürekliliğini birlikte sınar. Tam ilk harita veya Steam demosu değildir.

## Açılış ve kontroller

- Windows: proje kökündeki `Tools/SavasPrototipleri/B_SEFER_BASLAT.cmd` → **YENİ SEFER**.
- Güncel derleme `Builds/ExpeditionV2/KamExpedition.exe`; önceki v1 derlemesi `Builds/Expedition/` altında korundu. Mabet kayıt biçimi değişmedi.
- Eski A/B karşılaştırması `BASLAT.cmd` ile ayrı açılır.
- Unity: **TacticalRPG → Prototipler → B - Buyuyen Seferi Ac** → Play.
- Sahne: `Assets/Scenes/Prototypes/KamExpedition.unity`.
- WASD / yön tuşları: Kam hareketi. Temel saldırılar ve iki destek biriminin davranışı otomatik.
- Sol tık: ekibin düşman odağı. Q: fare altındaki yakın ve görünür zemine çekim + işaret. E: Kam çevresinde patlama. SPACE: hareket yönüne kısa kaçış; duvar veya uçurumdan geçmez, dokunulmazlık vermez.
- Güç ekranında 1 / 2 / 3 veya karta tıkla. Bu sırada savaşın bütün simülasyonu durur.
- ESC: duraklat / devam. Fare tekeri: yakınlık. Alt düğmeler de çalışır; Q düğmesine basınca zemine tıklayarak hedef seçilir.
- Mabet ekranında ENTER / Devam, sonra **doğuya (D)** yürüyerek sonraki alana geç.

## Şu anda gerçekten çalışanlar

### v2 çeşitlilik deneyi — 18 Ekim

- **Yarıcı:** her grupta bulunur. 1,4 saniyelik uyarı sırasında saldırı doğrultusunu kilitler, ardından 6 metre atılır. Yana kaçmak ve siper kullanmak işe yarar; duvardan/uçurumdan geçmez, aynı atılışta aynı hedefi tekrar vurmaz.
- **Kalkancı:** ikinci ve üçüncü alanda görünür. Görüş hattındaki 3 metre yakın düşmanlara gelen hasarı %55'e indirir; kendisini ve diğer Kalkancıları korumaz. Mavi alan halkası ve ismi hedef önceliğini anlatır.
- **Nişancı:** saldırısı beklerken yaklaşırsan geri çekilir. Son bekçi atılış ile alan darbesini sırayla kullanır.
- **Girdap 2:** Q hedefinde 2,4 saniye çekim/yavaşlatma alanı bırakır. **Yörünge 2:** SPACE sonunda ek hasar halkası. **Mühür Kalkanı 2:** kalkan hasarla kırıldığında yakındakileri geri iter; süresi dolması bunu tetiklemez.

Bu sürüm yeni silah ailesi veya final düşman modeli eklemez. Efe v1'i güzel ama varyasyonu az buldu; v2'nin keyif/zorluk değerlendirmesi henüz yapılmadı. Harita geometrisi v1 ile aynı.

Üç radius-7 arena ve yürünebilir bağlantılar. Her alanda üç düşman grubu; son grubun finalinde kapı bekçisi. Takip kamerası ve belirmeden önce düşman geliş uyarısı var. Yerleşim elde tanımlıdır; her yeni tohum yeni bir coğrafya üretmez.

| Arazi | Oynanış sonucu |
|---|---|
| Kırık sütun | Hareketi ve uygun yükseklikte atış görüşünü keser; iki kaçış hattı bırakır. |
| Çamur | Oyuncu ve düşmanların hareketini aynı oranda yavaşlatır. |
| Su | Su İletimi gücüyle zincir menzili/hasarı artar; düşman elektrikli nişancısından da +2 hasar alınır. |
| Rampa / yükselti | Yükseklik gerçekten değişir. Rampadan çıkılır; dik kenardan yürünmez. Tepede +1,4 menzil; düşman da kullanabilir. |

Yedi davranış geliştirmesi, her biri iki seviye: Sıçrayan Kıvılcım, Su İletimi, Girdap, Yankı Patlaması, Akış, Yörünge, Mühür Kalkanı. Örnek birleşim: çekimle suya topla → işaretli hedefi patlat → iletimle geniş zincir → zincirden mana kazan. Alternatif: yakın yörünge + büyü kalkanı + patlama yankısı.

Gelişim doğrudan ilk kez yenilen düşmanlardan gelir; yerde öz/küre toplama yok. Eşikler 3, 8, 15, 24, 36, 50, 65. Teklifler üç farklı ve henüz azami seviyeye ulaşmamış güçten oluşur; varsa geliştirebileceğin mevcut güçlerden birini de sunar. Her teklif için savunma seçeneği garantisi yoktur. Kombinasyon üç alan boyunca korunur; yeni sefer başlatmak sıfırlar.

Roguelike değişkenliği: tohumla tekrarlanabilen düşman yerleşimi, alanlara göre değişen düşman bileşimi/değiştiricisi ve güç teklifleri. **Aynı tohum**, benzer oynayış ve seçimlerle karşılaştırma içindir; tamamen farklı davranan oyuncuda aynı anda aynı konum garantisi değildir. Savaş hızı sürekli yükseltilmez; gelişim kombinasyon ve konum kararlarından gelir. Alan temizlenince güvenli mola ve iyileşme sağlanır.

## Mabet, ölüm ve kayıt sınırı

- Ölünce **Mabede dön** aynı mücadeleyi başlatır; kazanılmış güçler ve gelişim korunur. Öldürülmüş aynı düşman kimliği tekrar gelişim vermez. Sadece yeniden keserek sınırsız güç kazanılamaz.
- İlk iki alan temizlendiğinde mabet kaydı otomatik diske yazılır. **Mabet kaydından devam**, sonraki alanı aynı güçlerle başlatır.
- Aktif savaşın ortası kaydedilmez. Ölümden dönüşte korunan o anki güçler uygulama açık olduğu sürece yaşar; çıkıp dönünce son tamamlanmış alan kaydı kullanılır.
- Yeni sefer açmak önceki mabet kaydını hemen silmez; yeni sefer ilk mabede ulaşınca onun yerine yazar. Tek kayıt yuvası vardır.
- Kayıt: `Application.persistentDataPath/KamExpedition/checkpoint-v1.json`; atomik geçici dosya/replace, önceki kayıt `.bak`. Ana kampanya cüzdanı/kaydı kullanılmaz.
- Altın, faiz, geri alınabilir para kaybı ve kalıcı meta gelişim bu prototipe eklenmedi. Bu ölüm deneyi nihai ekonomi kararı değildir.

## Sanat ve kapsam

Mevcut karakterler/idle-walk animasyonları kullanılır. Arazi, siper ve mabet işlevi gösteren kaba modellerdir; yeni final modeller/rig/saldırı animasyonları değildir. Efekt havuzu, okunur patlama halkaları, basit atış/zincir sesi var. Ana oyunun `xd.unity` sahnesi değiştirilmedi.

Modelci için güncel iş föyü: `Tools/SavasPrototipleri/ModelciPaketi/IS_FOYU_MODULER_V004.html`. Efe'nin son tercihi: zeminler ile yapılar bağımsız modellenir, Unity'de birleşir. 12 yapı/çevre parçası ve altı zemin örneği tasarım görselidir; gerçek 3D dosya veya oyuna uygulanmış avlu değildir. Yeni prototipin test karosu köşeden köşeye 1,60 m; mevcut ana oyun sanat hattı 1,90 m. Kaynakları küçültüp üzerine kaydetme; prefab ölçeğinde uyarlama yap. İki ölçü aynı sanılmamalı.

Yeni silah aileleri, eşya/zırh/pot, satın alma, hikâye sahneleri, sekiz harita ve tam demo hâlâ ayrı işlerdir. Eski kaynak sürüm arşivi korunuyor.

## Tekrarlanabilir doğrulama

v2 sonucu: Windows derlemesi başarılı; 86 sefer + 39 eski A/B + 15 yeni taktik kontrolü geçti. Yeni testler kilitlenen yön, bekleme, atılışta tek hasar, koruma ve kaybı, girdap süresi/duraklatma, kaçış halkası ve kalkan geri itmesini doğrular. Rapor `Logs/expedition-tactics-validation.json`. Bunlar normal zorluk veya keyif kabulü değildir.

- `TacticalRPG.Editor.ExpeditionSetup.BuildWindows`: eski A/B kontrolleri + yeni sefer mantık kontrolleri + bağımsız Windows derlemesi.
- Kontroller: coğrafya/koordinatlar, rampa/kenar/görüş, gerçek çamur hareketi ve tepe menzili, tohum, seçimde donma, üç alanı fiziksel geçme, güçlerin JSON kaydından geri gelmesi, çift gelişim engeli, ölümden dönüş, yankı/kalkan/mana.
- Kaynak: `Assets/Scripts/Editor/ExpeditionValidation.cs`; rapor: `Logs/expedition-validation.json`. Akış testindeki yüksek canlı sürücü **oyuncu dengesi testi değildir**.
- Gerçek Windows oyuncusu görüntüsü: `--expedition-capture "tam_yol.png"`; güç ekranı için ayrıca `--expedition-choice`. Bu görsel kontrol oyuncu kaydını yazmaz.

### Efe ile oynanacak kabul deneyi

İlk denemede çekim + patlamayı öğren, ilk güçten sonra davranışının değişip değişmediğine bak. İkinci denemede başka bir kombinasyon seç. Tepede durmak, suya çekmek ve sütun arkasına kaçmak farklı sonuç yaratmalı. Yeni alana geçince eski gücünün devam ettiğini ve duraklama sonrası tekrar savaşa girmenin temposunu kontrol et. Özellikle “güçlenme hissi var mı, ekip kendiliğinden fazla mı iş yapıyor, arena gereksiz boş mu, hareket takılıyor mu?” geri bildirimi gerekli. Henüz insan tarafından bu yeni sürümün keyif/zorluk kabulü yapılmadı.
