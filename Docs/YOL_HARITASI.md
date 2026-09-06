# YOL HARİTASI — Efe'nin Gelecek Planları (2026-09-02 bilgilendirmesi)

Bu belge Efe'nin 2026-09-02'de anlattığı **15 maddelik gelecek planının** kaydı + her maddenin
bugünkü durumu, kimin yapacağı ve ilk adımı. Sayılar (gün, HP, ödül, öz miktarı) **ÖRNEKTİR** —
Efe açıkça "sayılara ve kesin tanımlara takılma" dedi. Kesinleşen her sayı `DECISION_LOG.md`'ye
yazılır, buraya değil.

> Bu belge bir **niyet kaydı**dır, sözleşme değil. Madde sırası önem sırası değildir; önerilen
> çalışma sırası en altta (§ FAZLAR).

---

## Efe'nin cevapları (2026-09-02, aynı gün)

- **Map sayısı:** şimdilik **toplam 8 map**. İlk evren 3 map'lik tasarlanırsa toplam **11 map**
  olur. (Yani 8 = map sayısı, evren sayısı değil.)
- **Skill tree:** **tüm oyunu kapsar, ölünce SIFIRLANIR.** Her map'te geliştirilebilir ve o map'e
  ÖZGÜ skiller de oradan açılır. Ayrıca roguelike bir meta-ekonomi olacak: her yeniden başlayışta
  bir biçimde daha avantajlı başlanır.
- **Puzzle:** kesin tasarım yok, ama yön belli — **karo üstü bulmacamsı** bir sistem.
- **Karo silinmesi:** bir **sisteme ve sayıya bağlı** olmalı. Şu an bazen oluyor bazen olmuyor —
  **bug var**, sonra bakılacak (bkz. § AÇIK HATA).
- **Öz modelleri:** şimdilik **koddan üretilecek** (placeholder), Efe'nin modelleri sonra girer.
- **Sinematik:** yandan çıkan karakterli **pop-up anlatım**, ilerisi için AI üretimi video
  düşünülüyor. **Bu iş bende değil — planlamıyorum**, Efe isterse sorar.
- **Zorluk ölçeği (3. çatışmanın cevabı):** zorunlu görev sayısı **her map'in İÇİNDE** artar;
  evrenin zorlaşması ise **sonraki map'lerde daha zor karakter/düşman tasarımı** olarak gelir.
  Hedef: oyun kalıcı olarak **kompleksleşsin**.
- **Kam'ın canı (1. çatışmanın cevabı):** savaştan sonra canın **bir kısmı yenilenir ama tavan
  kalıcı olarak düşer**. Yani iki-çubuk modeli kabul: MEVCUT can kısmen dolar, MAX can düşer.

## Bir bakışta

| # | Konu | Durum | Kim |
|---|------|-------|-----|
| 1 | Karodan öz tipinin anlaşılması + tıkla-incele | Kısmen (öz sistemi var, görsel dil yok) | Birlikte (modeller Efe) |
| 2 | Karakter/öz sinematikleri | Yok | Sistem birlikte, içerik Efe |
| 3 | Savaş sisi kenarında ipucu | **YAPILDI 2026-09-02** | — |
| 4 | Skill tree → Kam'ın yetenekleri | **BÜYÜK ÖLÇÜDE YAPILDI 2026-09-04** (KİTAP'ta ağaç, 15 büyü / 5 dal, öz ile açma+yükseltme, ölünce sıfırlanma, draft havuzu bağlı). Eksik: roguelike META EKONOMİ, map'e özgü dallar | Birlikte |
| 5 | Zorunlu görev zinciri (sayaç, tip çeşitliliği, ekonomiye bağlı sayı) | **Büyük ölçüde var** | Birlikte |
| 6 | Kam: yüksek can + kalıcı hasar + can kazanma | Yok | Birlikte |
| 7 | Her map'te Kam'ın ANA mekaniği değişir | Yok — **omurga** | Birlikte |
| 8 | İlk map'te mekanik kazanma + mekanik füzyonu | Yok — 7'ye bağlı | Birlikte |
| 9 | Map'e özgü geçici özler + transfer kuyuları | Yok | Birlikte |
| 10 | Silinen karoları geri getirme (tanrısal yerleştirme) | **YAPILDI 2026-09-02** (çöküşün kendisinde bug var) | — |
| 11 | Anlatım tarzının map'ten map'e değişmesi | Yok | Sistem birlikte, yazım Efe |
| 12 | Görevin hikayesine göre düşman çeşitliliği | Yok | Birlikte (tablolar Efe) |
| 13 | Görev dizinleri + her map'e giriş sinematiği | Kısmen (zorunlu zincir var) | Birlikte |
| 14 | İlk 3 map = öğretici evren, sonrakiler 1-2 map'lik evrenler | Yok — yapıyı etkiler | Efe kararı |
| 15 | Ana hikaye / kozmoloji (8 büyük evren) | Yok (yazıldı) | Efe |

---

## 1) Karo → hangi öz, ne kadar

**İstenen:** Karonun TİPİNDEN hangi öze ait olduğu bakışta anlaşılsın; sol tıklayınca hangi özden
ne kadar olduğu görünsün. İki öz varsa hem renk hem tasarım **yarı yarıya** bunu göstersin
(kırmızıya alev çukuru, yeşile yüce çam gibi karo İÇİ unsurlar). 3+ öz varsa o karoya **özel tek
karoluk tasarım/model**.

**Bugün:** `EssenceFieldManager` özü haritaya saçıyor (60-80), küreyle görünüyor, toplanınca karo
kuraklaşıp çatlıyor. Yani **veri var, görsel dil yok**: hangi öz olduğu karodan okunmuyor.

**İlk adım:** Öz türü → görsel eşlemesi bir `EssenceVisualSO`'ya taşınsın (renk + prefab + ikon).
Karo görselleştirici tek öz için tek model, iki öz için karoyu ikiye bölen iki model koysun; 3+ öz
"özel karo" id'sine düşsün. Tıkla-incele için mevcut karo istem paneline "öz dökümü" satırı.

**Not:** Bu işin yarısı MODEL işi (Efe), yarısı boru hattı (birlikte). Modeller gelene kadar
koddan üretilen placeholder'larla ilerlenebilir — 5 sınıfın modellerinde aynı yöntem işledi.

## 2) Sinematikler

**İstenen:** Sık kullanılan karakterler için, onları var eden ÖZLE ilişkili sinematikler
(Savaşçı taştan yaratılıyorsa dağa çıkıp taşın ruhunu içine çekmesi gibi).

**Öneri:** Sinematik = kod değil VERİ olsun. `CinematicSO` (adım listesi: kamera hedefi, süre,
metin, ses, opsiyonel görsel) + tek bir `CinematicPlayer`. Efe içerik yazar, kod hiç değişmez.

**Neden erken:** Aynı oynatıcı 11, 13, 14 ve 15'i de taşıyor (map girişi, ton değişimi, ana
hikaye). Tek sistem, dört maddeyi açıyor — bu yüzden omurgalardan biri.

## 3) Savaş sisi kenarında ipucu

**İstenen:** Oyuncuya YAKIN taraftaki sisli karolar, uç kısımlarından renk/model olarak ne
olduklarını sızdırsın; tam bilgi değil, ipucu.

**Bugün:** Sis ikili: ya keşfedilmiş ya değil. Ara durum yok.

**İlk adım:** `FogOfWarManager`'a üçüncü bir durum — "kenar/yarı görünür": karo silueti + baskın
renk, ikon ve düğüm bilgisi YOK. Rota planlayıcısındaki "TAHMİNİ" kuyruk zaten aynı felsefeyi
kullanıyor (bkz. `RouteMarker`), tutarlı olur.

**Küçük iş, görünür kazanç** — hemen yapılabilir.

## 4) Skill tree

**İstenen:** Kam'ın yetenek kullanımı skill tree'ye bağlı; geliştirmeler oradan.

**Açık soru:** Ağaç **run içi** mi (ölünce sıfırlanır, roguelite) yoksa **meta** mi (runlar arası
kalıcı)? Bu cevap ekonomiyi ve 6. maddedeki "kalıcı +2 can" büyüsünün anlamını belirliyor.

**İlk adım:** Cevap gelince: `SkillNodeSO` grafiği (önkoşul + maliyet + etki) + tek bir
`SkillTreeState`. UI taslağı `Docs/UI_LAYOUT.md`'de var.

## 5) Zorunlu görev zinciri — genişletme

**İstenen (2026-09-02 yeniden anlatım):** Zorunlu görevler yapılınca **boss taşı**; zaman
ilerledikçe zorunlu görev sayısı **ekonomiye bağlı** artabilir; belli günlerde açılan görevler bir
**UI sayacıyla** takip edilsin (7. günde 3., 10. ve 13. günlerde diğerleri); görev bir **savaş,
bir puzzle ya da yepyeni bir mekanik** olabilir; sis olsun olmasın minimapte **yeri görünür**;
oyunun zorlaşması zorunlu görev sayısına bağlı; görev **erken de geç de** tamamlanabilir, iki
durumda da boss taşı gelir, **bossa girmek oyuncunun kararı**. Oyuncu ya erken bitirip kendini
garantiye alır, ya da geciktirip 4. ve 5. görevlerin **büyük ödüllerini** alarak riskle grind
yapar. Ödüller 1'den 5'e **logaritmik/üstel** büyür.

**Bugün (2026-08-28/31'de yapıldı):** 2 görevle açılıyor, gün 5/8/11'de yenisi düşüyor, hepsi
bitince boss taşı + zincir kapanıyor, ödül 20/32/51/82/131, minimapte görünüyor, bitmiş görev
mühürleniyor. Boss'a girmek zaten oyuncunun kararı ve erken/geç fark etmiyor.

**Eksik olan üç şey:**
1. **UI SAYACI** — "3. zorunlu görev: 2 gün sonra" diye görünen bir gösterge. Şu an takvim koda
   gömülü ve oyuncuya hiç söylenmiyor.
2. **GÖREV TİPİ SOYUTLAMASI** — bugün her zorunlu görev bir SAVAŞ. Puzzle / yeni mekanik için
   `IMissionKind` benzeri bir arayüz gerekiyor: "girildi → şu ekran açılır → başarı/başarısızlık
   dönerse zincir devam eder". Bunu yapmadan puzzle eklemek her seferinde `MissionManager`'ı
   yamamak olur (CLAUDE.md'deki spagetti yasağı).
3. **EKONOMİYE BAĞLI SAYI** — görev sayısı ve açılış günleri sabit; "ekonomi büyüdükçe artar"
   için tek bir zorluk eğrisi (curve) ve onu okuyan bir üretici gerekiyor.

**İlk adım:** (2) ile başla — sayaç ve ekonomi ölçeği onun üstüne kolay oturur.

## 6) Kam'ın canı: kalıcı hasar + can kazanma

**İstenen:** Kam yüksek canlı; hasar aldıkça **kalıcı hasar** birikiyor. Ana haritada bazı
karolarda görev yaparak ya da başka mekaniklerle can kazanabiliyor (max can artışı ya da yüksek
oranlı yenileme). Savaşta büyü olarak anlık veya kalıcı +2 can; bu skill tree'den açılmalı.

**Tasarım uyarısı (7. maddeyle çakışma):** 7'de can bir KAYNAK (harcanıyor), 6'da can bir TAVAN
(kalıcı azalıyor). Aynı sayıya iki anlam yüklenirse kural okunmaz olur. Önerilen ayrım:

- **TAVAN (max can):** yalnız kalıcı hasar düşürür, yalnız kalıcı ödül yükseltir.
- **MEVCUT can:** harcama, yenileme ve savaş hasarı burada olur; tavanı geçemez.

Böylece "canını harcadın" ile "kalıcı hasar aldın" oyuncuya iki ayrı çubukta anlatılır.

## 7) Her map'te Kam'ın ana mekaniği değişir — **OMURGA**

**İstenen:** Örnek verilen map: **Kam'ın canı o map'in kullanılabilir özü**. Büyü atarken,
karakter yaratırken, o map'e özgü karolarda savaşa girerken ya da daha güçlü summon yaparken can
harcanır. O map'te toplanan özlerin çoğu can dolduran türden olur. Can çok düşerse Kam
spawnlayamaz. Yetenek ikonlarının altında can barı eşikleri (10/8/6/4) görünür ve 4'e
yaklaştıkça yetenek güçlenir.

**Neden omurga:** Bu madde "map = arazi düzeni" fikrini bitiriyor; **map = KURAL SETİ** oluyor.
7, 8, 9, 11, 14 aynı yapıya yaslanıyor. Bu yapı kurulmadan 2. map'i yapmak, her map için
`ChapterRunManager`'a `if (bolum == 2)` eklemek demek — üç map sonra kod okunmaz olur.

**Önerilen yapı:**
- `ChapterRulesSO` — bir bölümün kimliği: hangi Kam mekaniği açık, hangi özler var, hangi ton,
  hangi görev zinciri, hangi düşman tablosu.
- `IKamMechanic` — küçük bir arayüz (kaynak nedir, maliyeti kim öder, eşikte ne değişir).
  Her map'in mekaniği ayrı bir sınıf; çekirdek kod hiçbirini bilmez.
- Yetenek maliyetleri "can mı, mana mı, öz mü" diye SORMAZ; aktif mekanikten **kaynak** ister.

## 8) Mekanik kazanma + füzyon

**İstenen:** İlk map'in olayı, ana mekaniğin iyi ya da kötü ihtimalle kazanılması — örneğin tur
başına 2 mi yoksa 3-4 mü gidileceği, o map'te yapılan bir görevin sonucunda belli olur. 5. map'te
Kam mana harcıyorsa ek bir enerji mekaniği açılır, limiti oyun başı 2-8 arası rastgele. Yeni ana
mekaniklerin bazıları **eski mekaniklerle füzyonlanabilir**.

**Uyarı:** Serbest füzyon kombinasyon patlamasıdır (5 mekanik = 10 çift, 8 = 28). Öneri:
mekanikler 7'deki aynı arayüzü paylaşan **değiştiriciler** olsun; füzyon ise **elle yazılmış çift
tarifleri** (whitelist) — otomatik değil. Böylece her füzyon tasarlanmış bir şey olur, kazayla
oluşan bir şey değil.

> **KARAR (Efe, 2026-09-02): bu öneri KABUL EDİLDİ.** Füzyon whitelist olarak yazılacak; listede
> olmayan çift birleşmez, iki mekanik yan yana çalışır. Faz 2'de uygulanacak.

**Bağımlılık:** 7 yapılmadan 8 yapılamaz.

## 9) Map'e özgü geçici özler + transfer kuyuları

**İstenen:** Ana öz sınıfları sabit; map'e özgü "+1 kan özü", "+1 kalkan özü" gibi hep **fazlalık
+1** özler olur, o map'in mekaniklerinde kullanılır, **geçicidir** (diğer map'lere geçmez).
Haritada **transfer kuyuları** olur: belli karolarda ana özü map'in özüne **kalıcı olarak**
çevirebilirsin.

**İlk adım:** Öz türü listesi bugün sabit; `ChapterRulesSO` bölüme özel öz türü ekleyebilmeli ve
cüzdan "geçici" özleri bölüm bitince temizlemeli. Transfer kuyusu = yeni bir düğüm tipi (mevcut
kamp/han/kule düğüm boru hattını izler, sıfırdan iş değil).

## 10) Silinen karoları geri getirme

**İstenen:** Rastgele silinen bir karoya yakında yürünebilir karo varsa oraya atlanıp, ya riske
girerek ya özel bir ekonomi harcayarak birkaç karo geri getirilebilsin. Geri getirilen karolar
**seçilebilsin**: "+5 karo geri getirme" kazandıysan, **tanrısal bakış açısıyla** haritanın sisi
olmayan herhangi bir yerine o 5 karoyu sen yerleştirirsin.

**Bugün:** Karo çöküşü var (rota bile "harita altından kayan durak"ı temizliyor), geri getirme yok.

**İlk adım:** Yerleştirme ekranı için **hazır desen var**: minimap + `MinimapTravelSelector`'ın
tıkla-seç düzeni + `RouteMarker`'ın çoklu-durak mantığı. "Tanrısal yerleştirme" bunun bir kipi
olarak yazılabilir — sıfırdan UI gerekmez.

## 11) Anlatım tarzı map'ten map'e değişir

**İstenen:** Bazı map'ler ciddi, bazıları şakacı, bazıları 4. duvarı kıran, bazıları romantik.
Her map'te o tona özgü içerik: romantik map'te yan görev ya da ana görev olarak bir ilişkinin
hikayesine oyuncu şahit olur.

**İlk adım:** Ton, `ChapterRulesSO`'da bir alan olsun ve **metin havuzu bölüme bağlansın** (aynı
olay için farklı map'te farklı cümle). Kod tarafı küçük; asıl iş yazım (Efe).

## 12) Görevin hikayesine göre düşman çeşitliliği

**İstenen:** Her savaşın hikayeyle ilişkili bir görev dizini olsun; düşmanlar o hikayeye uysun —
Dune'da solucan avlanıyorsak toprak yaratıkları, Muad'Dib'i kral yapacaksak insan düşmanlar.

**İlk adım:** `EnemyTableSO` (bölüm + görev zinciri başına düşman havuzu). Savaş kurucusu sabit
listeden değil, aktif görevin tablosundan çeksin. Küçük kod, büyük "his" kazancı.

## 13) Görev dizinleri + map giriş sinematiği

**İstenen:** 2-5 görevlik diziler. Kendi hikayesi olan evrenlerde (Dune, Marvel gibi) Kam'ın
hikayesi önemsizdir; Kam'ın işi map'i bitirmektir. Her map geçişinde tekrarlanan bir sinematik:
Kam yere düşer, o evrenden biri (örn. bir rahip) elini kaldırır ve ona bir görev verir.

**Bugün:** Zorunlu görev **zinciri** var ama "dizi" anlamında hikaye taşımıyor — sayı ve ödül var,
metin ve bağlam yok.

**İlk adım:** `QuestChainSO` (sıralı adımlar + her adımın metni + ödül) ve **zorunlu zincir bunun
bir örneği olsun** — iki ayrı zincir sistemi yazma.

## 14) Evren = 1-3 map

**İstenen:** Oyun belki ilk 3 map boyunca sadece mekanik öğretir; hikayede bunun karşılığı, Kam'ın
bozukluğun başladığı **küçük ama sınıfına göre çok büyük** bir evrende olması. Sonraki evrenler
normal boyda olduğu için 1-2 map sürer.

**Yapıya etkisi:** Bugünkü kural "1 BÖLÜM = 1 HARİTA, 8 bölüm". Bu madde araya bir katman koyuyor:
**EVREN → 1..3 BÖLÜM**. Toplam map sayısı ve evren sayısı ilişkisi netleşmeli (bkz. § SORULAR).

## 15) Ana hikaye — kozmoloji

Gerçeklikte **8 büyük evren** vardır; örümcek ağı gibi birbirine bağlıdırlar ve her şey **denge**
üzerinedir. Bir fazlalık ya da azlık olduğunda evrenler suyun mekaniği gibi ya birbirini besler ya
sömürür ve eşitliğe dönerler. Bu besleme/sömürme iyi bir süreç değildir: sömürülen evrenin iç
dengesi bozulur, kaos çıkar, hatta o evrendeki **yaşam-ölüm mekanizması** ve varlık-yokluk
gerçekliği değişebilir.

Bir büyük evren sebebi bilinmeyen şekilde **büyümektedir**; diğer 7'si onu sömürmeye başlamıştır.
Her büyük evrenin içinde milyarlarca evren, onların içinde milyarlarca alt evren vardır; her
kademede evrenler birbirine eşittir, yoksa alt kademelerde de sömürme başlar. Bir **alt evren**
çevresindeki evrenleri yemeye başlamış ve büyümektedir. Sebebini araştırsın diye bir **Kam**
görevlendirilir.

Kam'ın amacı o evrenin düzensizliğini yaratan engelleri kaldırmak, evreni **tamamlamak** ve onun
etkilediği evrenleri düzeltmektir. Evrenlerin hikayeleri, temaları ve anlatım tarzı değişir; bazı
evrenler (örn. 1. ve 5.) doğrudan Kam'ı ve onun hikayesini anlatır, bazıları tamamen bağımsız
kendi hikayeleridir. Kam kendi hikayesini anlatan evrenler dışında **o evrenin sorununu** çözer;
arkadaki tek bilinci "bu evreni düzelt"tir, kendi hikayesinin orada önemi yoktur. İlk map'ten bir
bilinç kaldığı için her yeni evrende ne yapması gerektiğini bilir. Sonunda bozuk evreni düzeltip,
sömürülen büyük evreni diğerleriyle eşitleyerek kaosu durdurur.

**Üretim notu:** "Dune evreni", "Marvel evreni" örnekleri **tür tarifi** olarak duruyor. Yayınlanan
oyunda o isimler ve tanınır karakterler kullanılamaz (telif). Aynı hissi veren özgün evrenler
(çöl-solucan-kehanet evreni, süper kahraman evreni) yazılır; iç belgelerde kısa yol olarak
"Dune-vari" demek sorun değil.

---

## ÇÖZÜLDÜ — "sisli yere ışık hüzmesi düşüyor ama hiçbir şey olmuyor"

Efe'nin 2026-09-02 raporu. **Işık hüzmesi bir hata değildi, ÇÖKÜŞ UYARISIYDI**
(`CollapseWaveEffect.DoomStrike` — 9 birim yüksekliğinde yıldırım). Yıldırım "bu karo düşecek"
demek için çakıyor ve karonun YÜZEYİNE kırmızı çerçeve + kalan-AP sayacı bırakıyordu.

**Kök neden:** sis bulutu karonun ÜSTÜNDE (0.18 birim havada) ve opak; uyarı çerçevesi
yüzeyde (0.06). Sisli bölgede çerçeve bulutun altında kalıyor → oyuncuya yalnız sebepsiz bir
yıldırım görünüyordu. Harita çoğunlukla sisli olduğu için işaretlerin ÇOĞU böyle kayboluyordu.

**Aynı kök, ikinci şikayeti de açıklıyor:** "karo silinmesi bazen oluyor bazen olmuyor." Sistem
aslında düzenli — `CollapseConfig`: **3. günde başlar, günde 2 karo, her gün +1, tavan 10, 2 gün
önceden uyarı.** Ama seçim TÜM haritadan rastgele yapıldığı için çöken karoların çoğu oyuncunun
görmediği sisli bölgede kalıyor; "olmuyor" sanılan şey görünmeyen şeydi.

**Çözüm (2026-09-02):** uyarı görünen katmana taşındı — işaretli karonun **BULUTU kızıl yanıyor**
(`FogOfWarManager.SetCloudAlarm` / `ClearCloudAlarms`, `MapCollapseManager` çerçeveyle birlikte
kurar/kaldırır). Açık arazide eski kırmızı çerçeve aynen duruyor. Çöküş dalgası bulutu geçici
boyadığında alarm rengi SİLİNMİYOR (`CapBaseColor` ortak okunuyor).

**İkinci yarısı da yapıldı (Efe: "bunu yap, rastgele şekilde baskılayıcı bir biçimde olsun"):**
çöküş artık iki havuzdan seçiyor — YAKIN (oyuncunun `NearPlayerRadius` çevresi) ve UZAK. Günün
payının `NearPlayerShare` kadarı (varsayılan **%60**) yakın havuzdan gelir. Seçim her iki havuzda
da **rastgeledir**; değişen tek şey havuzun daralması. Günlük karo SAYISI değişmedi — çöküş
sadece artık görülebilecek yerde oluyor.

Oyuncunun **2 karo çevresi muaf** (`MinPlayerDistance`): dibindeki halka da çökseydi oyuncu
hiçbir yöne yürüyemeyip sert kesime kadar donardı. Yine de 2 günlük uyarı süresinde oyuncu
çökmekte olan bir bölgeye YÜRÜYEBİLİR — bu bilinçli bir risk, kaçış kapısı madde 10'daki karo
geri getirme hakkı.

## BEKLEYEN İŞLER — İKİSİ DE YAPILDI (2026-09-03)

Efe 2026-09-02'de sıraya koymuştu, 2026-09-03'te "uygula" dedi. Aşağıdaki iki maddenin
İSTEK metni olduğu gibi duruyor; her birinin sonuna **NE YAPILDI** bloğu eklendi.

### B1 — Zorunlu görev geldiğini ana haritada FARK ETMEK

Efe uzaktan bir ışık görüp "bug mu?" sandı; meğer `MandatoryQuestFallEffect` imiş (yeni zorunlu
görev gökten düşerken ışık sütunu + altın kütle). Kendi hatası olduğunu söyledi **ama haklı bir
sorun buldu**: efektin menzili var ve oyuncu ekranın ucunda görürse ne olduğunu anlamıyor;
görüş alanı dışında düşerse hiçbir şey görmüyor. Bugün geri bildirim yalnız minimap ikonu ve
üstteki zincir barında — yani **UI takip etmeyi zorunlu kılıyor.**

> Kural: oyuncu ana haritada yürürken UI'a bakmak zorunda KALMADAN yeni zorunlu görevden
> haberdar olmalı.

Düşünülecek fikirler (hiçbiri onaylanmadı):
- **Kalıcı gök sütunu:** düşüş efektinden sonra o karonun üstünde birkaç gün duran, uzaktan
  görülen ince ışık huzmesi (kule ışığından ayrı renk — altın).
- **Ekran kenarı oku:** görev ekran dışındaysa kenarda yön oku + mesafe (klasik hedef göstergesi).
- **Kameranın kısa bir "bakış"ı:** düşüş anında kamera oraya bir saniye kayıp geri döner.
- **Duyulur işaret:** yönü belli olan bir gök gürültüsü/çan sesi.

**NE YAPILDI (2026-09-03):** ilk iki fikir BİRLİKTE uygulandı — biri dünyada, öbürü ekran
kenarında; üçüncüsü (kamera bakışı) kamerayı oyuncunun elinden aldığı için, dördüncüsü (ses)
projede ses katmanı olmadığı için alınmadı.

- **`MandatoryQuestBeacon` (fener).** Açık her zorunlu görevin karosunda duran altın gök sütunu.
  İki kademe: görev düştüğü günden `_freshDays` (2) gün sonrasına kadar KALIN ve nabız gibi atar
  ("bir şey oldu"), sonra görev bitene kadar İNCE ve sabit kalır ("şurada hâlâ bir görev var").
  Beş görev birden açıkken ekranı altına boğmasın diye incelme şart. Prosedürel — düşüş
  efektiyle aynı desen, prefab istemez. Görev bitince/harita yenilenince söner.
- **`QuestBeaconCompassHUD` (pusula).** Fener EKRAN DIŞINDAYSA ekran kenarında yön oku + karo
  cinsinden mesafe. Yeni görev = parlak ok + "ZORUNLU GÖREV" yazısı, eskiyen = soluk ok + yalnız
  mesafe. Kameranın arkasında kalan hedef aynalanır (yoksa ok ters yönü gösterirdi). Veriyi
  üretmez, feneri okur.
- Kurulum `SetupMandatoryQuestChain` içinde (TAM KURULUM zincirinde) + `SetupQuestChainBatch`
  doğrulaması: `fener:True pusula:True pusula-kamera:True`.

### B2 — Kam çöken karonun üstünde kalmasın

**Hata (Efe, doğrulanmış):** karo tam silinirken Kam o karonun üstüne yürüyebiliyor. Oyun
kilitlenmiyor (başka karoya yürüyebiliyor, sonra o karoya bir daha giremiyor) ama Kam bir an
**boşlukta duruyor gibi görünüyor** — çirkin.

İstenen: bir karo silinecekse ve Kam o an üstündeyse (ya da üstüne gelmek üzereyse), silinmeden
hemen önce Kam **yanındaki silinmemiş/silinmeyecek bir karoya itilsin**. Kam hiçbir zaman
boşlukta durur gibi görünmemeli.

Not: `PickDoomed` zaten oyuncunun O ANKİ karosunu ve 2 karo çevresini muaf tutuyor — ama işaret
2 gün önceden konuyor, oyuncu o süre içinde işaretli karoya YÜRÜYEBİLİYOR. Yani çözüm seçimde
değil, **silme anında** olmalı.

**NE YAPILDI (2026-09-03):** güvenlik tam da silme anına kondu.

- `MapCollapseManager.DayBoundaryRoutine` günün çökenlerini topladıktan HEMEN SONRA, deprem
  başlamadan `ShovePlayerToSafety` çalışır: oyuncu bugün çökecek bir karodaysa halka halka
  (`_escapeRings` = 3) güvenli karo aranır — arama yalnız YÜRÜNÜR karolardan geçer, su/dağ
  üstünden atlayıp karşı kıyıya konmaz. Halka içinde seçim rastgele (hep aynı yöne itilme deseni
  olmasın). Önce tertemiz karo aranır; yoksa işaretli ama BUGÜN düşmeyecek karo kabul edilir.
- `PlayerController.ForceShiftTo` → küçük bir sıçrama yayıyla hızlı kaçış (`_shoveSpeed` 7,
  `_shoveHop` 0.45). Süren yürüyüş varsa KESİLİR — hedefi artık var olmayan bir karo olabilir.
- **İtilme AP yemez:** `ActionPointManager.GrantForcedMove()` (bedava hamle stokuna EKLER, Güçlü
  Yol Taşı stokunu yemez). Kural AP yöneticisinde kaldı, `OnMoved` normal aktığı için sis /
  işaretler / rota da normal tazelenir.
- **Kaçacak yer hiç yoksa** karo o gün SİLİNMEZ, bir gün ertelenir — oyuncuyu boşlukta bırakmak
  ya da kapana kıstırmaktansa kıyamet bir gün bekler.
- Savaştan dönüş yolu da kapatıldı: `ApplyCollapseStateForCurrentMap` sonunda oyuncunun karosu
  yürünemez hâle gelmişse aynı itme çalışır (savaşta gün dönmüşse karo veri olarak silinmiş olur).

## Çapraz riskler (birbirine değen maddeler)

- **6 ↔ 7:** can hem tavan (kalıcı hasar) hem kaynak (harcanan) olamaz — iki çubuk kuralı (§6).
- **7 ↔ 8:** füzyon, mekanikler ortak arayüzden yazılmazsa imkânsızlaşır; füzyon whitelist olsun.
- **9 ↔ 10:** ikisi de "geçici mi kalıcı mı" ekonomisine dokunuyor; tek bir kaynak kavramında
  toplansın, yoksa iki ayrı cüzdan mantığı doğar.
- **5 ↔ 14:** zorluk hem "zorunlu görev sayısı"na hem "evren sırası"na bağlanacaksa ölçek TEK bir
  eğride toplanmalı; iki yerde çarpan olursa denge tutmaz.
- **1 ↔ 9:** map'e özgü öz eklenince görsel dil de otomatik gelmeli, yoksa yeni öz gri küre olur.

## FAZLAR (önerilen sıra)

**Faz 0 — küçük, görünür, mevcut sisteme oturan**
- ~~(3) Sis kenarı ipucu~~ → **YAPILDI 2026-09-02** (`FogOfWarManager._edgeHintTiles/Alpha/Brightness`)
- ~~(10) Karo geri getirme + tanrısal yerleştirme~~ → **YAPILDI 2026-09-02**
  (`TileRecoveryManager`, `TileRecoveryHUD`, minimapte KARO GERİ GETİR kipi)
- (1) Öz görsel dili + tıkla-incele (Efe: modeller şimdilik KODDAN üretilecek)

**Faz 1 — zorunlu görevi tamamla (mevcut sistemin üstüne)**
- (5-2) Görev tipi soyutlaması → puzzle/mekanik görevler mümkün olsun
- (5-1) UI sayacı
- (5-3) Ekonomiye bağlı görev sayısı / zorluk eğrisi

**Faz 2 — OMURGA: map = kural seti (2. map'e başlamadan ÖNCE)**
- (7) `ChapterRulesSO` + `IKamMechanic`
- (6) Kam'ın iki çubuğu (tavan/mevcut) — 7'yle birlikte tasarlanmalı
- (8) Mekanik kazanma + füzyon tarifleri

**Faz 3 — hikaye altyapısı**
- (2) `CinematicSO` + `CinematicPlayer`
- (13) `QuestChainSO`; zorunlu zincir onun bir örneği olur
- (12) Düşman tabloları
- (11) Ton alanı + bölüme bağlı metin havuzu

**Faz 4 — derinlik**
- ~~(4) Skill tree~~ → **ÇEKİRDEĞİ 2026-09-04'te YAPILDI** (Efe istedi, sıradan öne alındı):
  `KamSkillTreeSO` + `KamSkillProgress` + KİTAP'ta yer imli ağaç sayfası + `InkArtFactory` ile
  el çizimi UI. Ağaç HAVUZU belirler, davul draftı rastgele seçmeye devam eder; ölünce sıfırlanır.
  **Kalan:** roguelike meta ekonomi (her yeniden başlayışta avantaj) ve map'e ÖZGÜ dallar —
  ikisi de Faz 2 omurgasına (ChapterRulesSO) yaslanacak.
- (9) Geçici özler + transfer kuyuları
- ~~(10) Karo geri getirme~~ → Faz 0'a çekildi, YAPILDI

**Sürekli (Efe):** modeller, metinler, düşman/ton içerikleri, sayı kararları.

---

*Kaynak: Efe'nin 2026-09-02 tarihli bilgilendirmesi. Kararlaştırılan her madde `DECISION_LOG.md`'ye
taşınır; bu belge niyeti korur.*
