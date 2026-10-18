# B · Büyüyen Sefer — oynanabilir prototip

## Güncel — Hibrit savaş atölyesi v8 (18 Ekim 2026)

Efe hibrit üretimi onayladı: tekil zemin + bağımsız çok hücreli yapılar. Zeynep'in MODELLER/Savas-Karo-Atolyesi_Paylasim_2026-10-18.zip paketi incelendi; 85 BattleMapPack/BattleTilePack dosyası GUID çakışması olmadan, kaynak baytları korunarak alındı. ProjectSettings/Packages, atölye Editor otomatik açılışları ve başka kişinin companion notları aktarılmadı. Kaynak 41 karolu sahne ve Blender dosyaları korunur. Aktarım SHA-256 listesi Tools/SavasPrototipleri/HIBRIT_AKTARIM_KAYDI.json.

Ayrı HybridBattle.unity + HybridBattle.asset + HybridBattleLayout.asset. Kaynak 2 m uçtan uca, 0,30 m taban korunur. 91 + 91 + 127 = 309 tekil zemin; 0,30 / 0,90 / 1,50 m üç avlu; iki bağımsız geçici köprü, gerçek LOD kapı ve geçici taş siperler. Köprüler 4,20 m yürünebilir genişlik; altı yürünmez. Yükseklik, geçilebilirlik ve görüş aynı harita modelini kullanır. Kapı ayakları engel; kemer ortası açık. Eğik kamera ve kameraya göre WASD hareketi yalnız hibrit sahnede.

Yeni Sefer mevcut iki silah / altı seçim / üç görev / boss akışını kullanır. Haritayı Gez düşmansız, ödülsüz ve kayıtsız dolaşım sağlar. checkpoint-hybrid-v8.json ayrıdır; eski checkpoint-v5 ve USTALIK_BASLAT.cmd korunur. Yeni HIBRIT_BASLAT.cmd, Builds/HybridBattleV8/KamHybrid.exe açar. Ana xd.unity entegrasyonu yapılmadı. FullSetup asset zincirine bağlı; EnsureScene var olan düzeni ezmez. Yerleşim SO'su ile görsel düzen birlikte tutulmalı; keyfi sahne model taşıması otomatik navigasyon üretmez.

Kontroller: 405 eski + 36 yeni = 441 işlev kontrolü; iki silahla üç görev/boss, mabet geçişi ve kapı-köprü-teras gidiş/dönüş geçti. Otomatik savaş sürücüsü yüksek can kullanır, normal zorluk/keyif kabulü değildir. Sahne denetimi 1.257 kontrol, 271 gerçek collider zemin örneği: hareket yüksekliği ile görünür yüzey eşleşir, eksik script/materyal yok, kapıda 3 LOD, tekrar kurulum sahneyi değiştirmez. Kanıt: OYUN/Logs/hybrid-validation.json, hybrid-scene-validation.json.

Sonraki insan denemesi: geçiş genişliği, kapı okunurluğu, kamera/WASD ve yüksek terasta boss. Efe bossu genel olarak onayladı ama oyunun kolay olduğunu belirtti; bu tur hasar/can dengesi yükseltilmedi. Modelcinin sonraki işi, onaylanan geçici köprü ölçülerini ve siperleri finalleştirmek: Tools/SavasPrototipleri/MODELCI_HIBRIT_V008.txt. Kule/varyant seri üretimi henüz gerekmiyor.


Windows build tamamlandı: Logs/hybrid-final-build.log. Son player görüntüleri: hybrid-gate-final.png, hybrid-bridge-final.png, hybrid-terrace-final.png, hybrid-boss-final.png; dördü incelendi, günlüklerde CAPTURE_OK ve exception/LogError/LogWarning yok. Gezi yakalamaları normal hareketle köprüye 99, son teras merkezine 296 simülasyon adımında ulaştı; boss görseli yüksek canlı açık QA fixture kullanır. Oyuncu kayıtları yazılmadı.

## Güncel — Üç aşamalı boss v7

Efe, aşamalı ve özel saldırılı bir boss; epik ama geçici görünüm; muhafız çağırma aşaması istedi. MasteryJourney üçüncü normal dalgası artık boss içermez; normal düşmanlar temizlenip güç seçimleri alındıktan sonra Kül Yeminli Muhafız ayrı giriş yapar. Geçici özgün görünüm: koyu zırh/kırık taç/iri kılıç/kor çekirdek; prefab `Assets/Data/Prototypes/Warden/AshWarden.prefab`. Final model veya final rig animasyonu değildir.

Aşama 1: ön yay biçişi, tek hücum, yer darbesi. %65 eşik: iki 18 canlı muhafız, çift hücum ve daha hızlı yürüyüş. %30 eşik: üç muhafız, genişleyen 6,5 m dalga ve art arda en çok üç kilitli yer darbesi. Hücumlar ayrı ayrı hazırlanır; yer hedefleri hazırlıktan sonra takip etmez. Dalga görüş kesen siperden geçerek vurmaz, aynı hedefe bir kez hasar verir. Boss çekime/geri itmeye bağışık; kamın ilgili yetenekleri muhafızlarda çalışır.

Boss 360 can; muhafız varken gelen hasarın %45'ini, çekirdek açıkken %160'ını alır. Son muhafız ölünce 3,5 sn açılır; hücum, dalga ve yer darbeleri sonrası da açık verir. Muhafızlar XP vermez, aşamaya göre iki/üç sınırı vardır, boss ölünce canlı/bekleyen muhafızlar kaldırılır. Eşiklerde 1,6 sn geçiş koruması; tek patlamayla aşamalar atlanmaz. İkinci/üçüncü aşama oyuncunun kombosunu alan temizliği ve boss hasarı arasında kullanmasını teşvik eder; belirli rastgele kart zorunlu değildir.

Boss HUD'si ve gerçek hasar geometrisini gösteren uyarılar eklendi. Modelci notu `Tools/SavasPrototipleri/BOSS_MODELCI_V007.txt`. Kurulum WardenSetup ile idempotent; FullSetup asset zincirine bağlı, Mastery menüsü sahne bağlantılarını tamamlar. Kullanıcı tweakleri ezilmez; yeni rig alanları boşsa doldurulur.

21 yeni + 384 eski = 405 işlev kontrolü ve Windows build geçti. İki silahla üç görev ve yeni boss yüksek canlı sürücüyle tamamlandı; insan denge/keyif kabulü değildir. Aç: `Tools/SavasPrototipleri/USTALIK_BASLAT.cmd` → `Builds/MasteryJourneyV7/KamMastery.exe`. Önceki v6 build korunur, aynı checkpoint-v5.json. Sonraki adım Efe'nin üçüncü görev kombosuyla aşamaları, muhafız baskısını, kaçış ve açık pencerelerini denemesi. Ana harita/final sanat/ekonomi hâlâ ayrı işler; push/senkron yok.



Son görsel doğrulama: Logs/warden-phase2-final.png ve warden-phase3-final.png gerçek Windows player görüntüleri incelendi; doğru 234/360 ve 108/360 eşikleri, soldan azalan can çubuğu, aşama/hedef metni, muhafız sayıları ve dalga sınırları görüldü. İki player günlüğünde CAPTURE_OK Fighting; exception/LogError/LogWarning yok. Yakalamalar açık QA fixture ile yüksek can ve kapalı dost saldırıları kullanır; normal denge kanıtı değildir ve oyuncu kaydı yazmaz. Son build: Logs/warden-final-build.log; boss kontrolleri: Logs/warden-validation.json.

## Güncel — Hareketli düşmanlar v6

Efe sıçrama, hızlanma/özel hareket ve klonlama gibi farklı düşman davranışları istedi. Üç rol MasteryJourney'ye eklendi: ilk görevden Sıçrayan (kilitli iniş halkası, gerçek görsel yükseklik yayı, iniş sonrası açık); ikinci görev ikinci dalgadan Hiddetli (hazırlık, süreli hızlı kovalama, kilitli yakın vuruş, yorgunluk); üçüncü görev ikinci dalgadan Yansıtıcı (iki kırılgan geçici kopya).

Yansımalar 6 canlı, 7 saniyelik ve 2 hasarlı; ilerleme puanı vermez, çoğalmaz; asıl düşman ölünce canlı ve bekleyen kopyalar hemen kaldırılır. Sahada en çok dört kopya, kaynak başına iki. Sıçrayışın havadaki bölümü de iki saldırılık hazırlık/atılış bütçesine dahil. Yeni roller eski düşük canlı grupların bir kısmını değiştirir; ustalık ve seçkinler korunur.

`USTALIK_BASLAT.cmd` artık `OYUN/Builds/MasteryJourneyV6/KamMastery.exe` açar. Önceki v5 exe korunur; aynı checkpoint-v5.json kullanılır. Tamamı ayrı prototip; final düşman modelleri/rig animasyonları üretilmedi. Hareket ve renk/şekil işaretleri mevcut aktörlere uygulanır. Modelci için `DUSMAN_HAREKET_NOTU_V006.txt`: önce mevcut rig üstünde kaba sıçrama, final sipariş değil.

21 yeni + 363 eski = 384 işlev kontrolü geçti. Windows build ve gerçek player sıçrama/kopya görüntüleri kontrol edildi: Logs/dynamic-leap.png, dynamic-clone.png; günlüklerde exception/LogError/LogWarning yok. Bu iki açık QA yakalaması yüksek can, kapalı dost saldırıları ve kopya örneğinde ilk dalgayı temizleme kullanır; normal oyun veya denge kanıtı değildir, oyuncu kaydı yazmaz. Sıçrama kilidi/kaçış/hasar/havada duraklatma, hızlanma/yorgunluk, klon sınırı/ödülsüzlük/ölüm/sona erme kontrol edildi. İnsan keyif ve zorluk kabulü bekliyor. Sonraki adım Efe'nin Yeni Sefer ile rol tanıtımını ve üçüncü görevde birleşen baskıyı denemesi. Kaynak `OYUN/Logs/dynamic-enemy-validation.json`; kod `ExpeditionDynamicEnemies.cs`. Push/senkron yok.


## v5 — Ustalık ve dönüşüm v5

Efe garantili silah ustalığı, üçüncü görevde eski düşmanları ezme ve seçkin baskısını onayladı. Aç: `Tools/SavasPrototipleri/USTALIK_BASLAT.cmd` → Yeni Sefer. Build `OYUN/Builds/MasteryJourney/KamMastery.exe`; sahne `MasteryJourney.unity`; ayrı `checkpoint-v5.json`. Mevcut IKI_SILAH_BASLAT.cmd'deki yerel v3'e dönüş değişikliği korunarak ayrı başlatıcı oluşturuldu.

Temel Hilal hasarı 9/14/18, Mızrak 6/9/12; saldırı sıklığı 1/1,12/1,24. Ek bıçak/disk/kaçış izi ve kırılma hasarı da ustalıkla artar. Ustalık görev indeksinden türetilir; ölüm veya yükleme tekrar güç vermez. Üçüncü göreve girişte Hilal üç yönlü yarık (Üçüncü Darbe varsa her iki isabette), Mızrak üçlü yaylım (Çifte Mızrak varsa dört) ve sekiz saplı mızrak. Kartların dönüşüme katkısı korunur; sonraki tekliflerde eksik imza/birleşim kartı sunulur, seçmek zorunlu değildir.

Sıradan düşmanlar 18 can/3 hasarda kalır. Görev ilerledikçe daha kalabalık gruplar, ikinci dalgadan itibaren 45 canlı altın işaretli seçkinler: ikinci görevde seçkin Yarıcı, üçüncüde ayrıca seçkin Kuşatmacı. İlk dalga güç denemesi; final boss 150 can. Kalkancı koruması hasarı azaltır; korunan sıradan düşman tek vuruş garantisi taşımaz. İki eşzamanlı saldırı hazırlığı sınırı korunur. Güç eşikleri 3/9/25/34/55/62; toplam altı kart, ilk iki görevde ikişer kart. Büyük dönüşüm kart RNG'sine bağlı değildir.

76 yeni + 287 eski = 363 işlev kontrolü, Windows build ve iki silahın gerçek player görsel kontrolü geçti. Görseller üçüncü göreve yüklenen açık QA fixture kullanır, oyuncu kaydı yazmaz. Üç görev akış testi yüksek canlıdır; insan keyif/zorluk kabulü değildir. Sonraki adım Efe'nin Yeni Sefer ile iki silahın güç farkını ve seçkin baskısını denemesi. Ana harita/final sanat/ekonomi açık; mevcut arşiv ve eski build'ler korunur. Push/senkron yok. Ayrıntı `USTALIK_OKU.txt`, `OYUN/Docs/Prototypes/B_EXPEDITION.md`.


18 Ekim 2026. Efe'nin seçtiği gerçek zamanlı B yönünün devamı. Bu küçük deney, büyük arena + savaş içinde büyüme + savaşlar arasında kombinasyon sürekliliğini birlikte sınar. Tam ilk harita veya Steam demosu değildir.

## Güncel deney: İki Silah / Üç Görev (v3)

**Son düşman güncellemesi (v4):** Efe çeşitliliğin arttığını ama düşmanların düz yürüyüp kolay öldüğünü bildirdi. Aynı `IKI_SILAH_BASLAT.cmd` artık `Builds/WeaponJourneyV4/KamWeaponJourney.exe` açıyor; önceki v3 derlemesi korunuyor. Silah, seçimler ve checkpoint-v3 biçimi değişmedi.

| Düşman | Yeni karar / karşılık |
|---|---|
| Yancı | Kam'a yandan yaklaşır; yakında saldırı hazırlar, beklerken yan hareket yapar. Pembe işaret. Ön yayı yönlendir, çevrilmeden yer değiştir. |
| Avcı | Mesafe korur, yan yürür; atıştan sonra kısa toparlanma verir. Turuncu hattı önceden gösterir ve kilitler. Yana çık veya sipere gir; yaklaşırken atış sonrası açıklığı kullan. |
| Kuşatmacı | İkinci görevden başlar. 1,6 sn uyarıdan sonra kilitli zeminde 2,6 sn tehlike alanı bırakır. Aynı anda en fazla iki alan; içinde kalmak aralıklı hasar verir. Tam yarıçap çizilir. |
| Kalkancı | Önündeki düşmanı takip ederek koruma alanını taşır; yalnız kalınca mesafeli konum alır. |
| Yarıcı | Kam'ı hedef alır; beklerken yan hareket yapar. Atılış yönü hâlâ önceden kilitlidir. |

En fazla iki düşman aynı anda saldırı hazırlayabilir/atılabilir. Normal düşmanlar da ayrışma hareketiyle aynı noktada yığılmayı azaltır. Uyarı başladıktan sonra nişan oyuncuyu takip etmez. Düşman canı, temel hasarı ve toplam dalga adedi topluca artırılmadı. Bunlar davranış değişiklikleridir; zorluk/keyif için insan testi hâlâ gerekir. Yeni roller eski geçici modelleri kullanır, isim/işaret/uyarı şekliyle ayrılır; final düşman modelleri üretilmedi.

`EncounterValidation` hareket, kilitli atış, yan kaçış, koruyucu takibi, eşzamanlı saldırı sınırı, bomba uyarısı/hasarı/kaçışı/süresi ve yığılma ayrışmasını doğrular. Yeni 16 kontrol, eski 271 kontrolle birlikte çalışır. Görsel deneme `--encounter-capture --weapon-evolved --weapon-spear` bayraklarıyla, kayıt yazmadan yapılır. Önceki v3 ayrıntıları aşağıdadır.

Efe, önceki güçlerin yeterince farklı hissettirmediğini ve Q/E spamını bildirdi. İki ayrı başlangıç saldırısı ve görevler arasında korunan gelişim isteği üzerine **ayrı** sahne/derleme oluşturuldu. `Tools/SavasPrototipleri/IKI_SILAH_BASLAT.cmd`; Windows `Builds/WeaponJourney/KamWeaponJourney.exe`; Unity menüsü `TacticalRPG > Prototipler > B - Iki Silah Uc Gorev`; sahne `Assets/Scenes/Prototypes/WeaponJourney.unity`. Önceki B v2 kendi başlatıcısında korunuyor.

Başta iki gerçek seçenek vardır; ikisi de sunulur, yalnız sıraları tohuma göre değişir. Dört silahlık rastgele havuz henüz uygulanmadı. Hilal 2,7 metre ön yayı otomatik biçer; Q imleç yönüne atılıp biçer, E üç isabetli temel saldırıyla dolan çevre bitirişidir. Mızrak 7,5 metre hattı otomatik deler ve en fazla dört mızrak bırakır; Q bunları oyuncunun **yeni konumuna** geri çağırır. Dönüş hattı görüş engeline uyar. Mızrağın E'si yakınları iter ve kısa kalkan verir. Q/E mana kullanmaz; E isabetlerle dolar. Boş mızrak geri çağırması reddedilir. Temel saldırının hedefi en yakın düşmandır; sol tık odağı değiştirebilir. Q ekran düğmesi hilalde son saldırı yönünü, klavyedeki Q imleci kullanır. SPACE kaçışı ayrı kalır.

Üç bağlı görev alanı önceki prototipin coğrafyasını kullanır; ana harita görev seçme UI'sı henüz bağlanmadı. Her görevde üç dalga, son görevde boss vardır. Gelişim eşikleri 3/9/22/29/41/49: bu mevcut dalga sayılarında görev başına iki, toplam altı farklı seçim verir. XP ve seçimler ortak; öldürülen düşmanlardan doğrudan kazanılır, yerde öz/küre toplama yok. Tohumla teklifler değişir; tekrar kartı yok, Kırılma önkoşulu Üçüncü Darbe/Çifte Mızrak. İlk teklif her zaman bir yeni otomatik saldırı ve silah dönüşümü içerir.

| Seçim | Gerçek uygulama |
|---|---|
| Üçüncü Darbe / Çifte Mızrak | Hilalin her üçüncü darbesi ileri yarık; mızrak iki kollu atış ve iki saplı mızrak. |
| Dönen Bıçaklar | Oyuncu çevresinde iki temas bıçağı; otomatik yakın hasar. |
| Çekim Alanı | Q sonunda 2,4 saniyelik çekim/yavaşlatma alanı. |
| Bitirici Yankısı | E 0,6 saniye sonra oyuncunun yeni konumunda tekrar eder; ikinci yük harcamaz. |
| Seken Disk | Yakın hedefe otomatik uzak destek atışı, uygun ikinci hedefe sekme. |
| Kaçış İzi | SPACE sırasında geçilen hat hasar verir; kalıcı hasar zemini değildir. |
| Kırılan Mühür | E kalkanı; hasarla kırılınca geri itme. |
| Kırılma | Hilalin üçüncü darbe hattındaki isabetler veya geri çağrılan mızrak başlangıçları patlar. |
| Karşı Hamle | E ayrıca kaçışı hazırlar ve geri iter. |

Seçimler, silah, XP ve öldürülmüş düşman kayıtları görev geçişinde, mabette diske kayıt/yüklemede ve ölüm sonrası aynı görevi tekrar denemede korunur. Geçici vuruş gücü, yerdeki mızraklar ve aktif efektler yeni göreve/ölümden dönüşe taşınmaz. **Yeni Sefer** açıkça seçilirse kombinasyon sıfırlanır. Mabet kaydı yalnız ilk iki görev sonunda, `KamExpedition/checkpoint-v3.json` yoluna yazılır; v1/v2 kaydını okumaz veya ezmez. Savaş ortası diske kayıt henüz yoktur.

Destek ekibi bu deneyde daha düşük temel hasarla destek olur; oyuncunun silah etkisini görünür kılmak içindir, dengelenmiş son değer değildir. Yeni efektler havuzdan çizilir; final silah mesh'i/saldırı animasyonu üretilmedi. Modelci çevre paketi ayrı iş olarak sürüyor.

Doğrulama: `WeaponJourneyValidation` iki silahla üç görev, altı farklı güç, ön yay/delici hat, siper, boş geri çağırma, E yük tüketimi, otomatik ek yetenekler, silah dönüşümleri ve v3 kayıt/ölüm devamını kontrol eder. Rapor `Logs/weapon-journey-validation.json`. Akış sürücüsü yüksek canlı işlev testidir; normal zorluk/keyif onayı değildir. Eski 140 kontrol de çalıştırılır. Gerçek oyuncu görüntüleri için mevcut `--expedition-capture` ile `--weapon-menu`, `--weapon-spear`, `--expedition-choice` kullanılabilir. `--weapon-evolved` dört güçle son görevi açan **yalnız görsel test** örneğidir; oyuncu kaydını değiştirmez.

Aşağıdaki bölümler önceki v1/v2 deneyinin kurallarıdır; v3 için yukarıdaki silah/gelişim kuralları geçerlidir.

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
