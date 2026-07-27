# Taktiksel RPG (OYUN) — Tasarım / Denge Belgesi

> Bu dosya, harita boyutları, yaratık/birim statları ve denge kararları için **tek doğruluk kaynağıdır**.
> Efe + Kardelen arasında ayrı bir PC'de (Unity dosyaları olmadan) tartışılan kararlar buraya işlenir,
> somut uygulama görevleri `Docs/INBOX_TASKS.md`'ye düşer. Unity tarafındaki Claude bu dosyayı
> uygulamadan önce okur; kararları değiştirmeden uygular, çelişki görürse görevi `blocked` işaretleyip
> INBOX_TASKS.md'ye not düşer.

**Kapsam ayrımı (2026-07-26 kararlaştırıldı):**
- **Bu taraf (2. PC, bu dosya + `Docs/Balance/`):** sistematik/sayısal oyun tasarımı — statlar, harita
  boyutu/pacing, öz ekonomisi, combat formülleri. Hikayeden bağımsız kurulur (hikaye henüz net değil;
  mekanik iskelet önce, hikaye keşfe-bağlı parçalar halinde sonradan haritadaki düğümlere/olaylara eklenir).
- **Unity tarafı:** hikaye + hikayeye bağlı görsel/asset tooling + kod implementasyonu. Modelleme burada
  yapılmıyor.
- `.md` dosyaları (bu dosya, INBOX_TASKS.md) makine-makine iletişimi + Claude hafızası içindir, kullanıcı
  bunları okumak zorunda değil. **İnsan-okunur çıktı** (Excel/Word, ör. combat mekanikleri) için
  `Docs/Balance/` altında ayrı `.md` kaynakları yazılır ve `Docs/Balance/tools/md2docx.py` +
  `Docs/Balance/tools/md_tables_to_xlsx.py` ile otomatik `.docx`/`.xlsx` ikizleri üretilir — tek kaynaktan,
  elle kopyalamadan, hep senkron.

---

## 0) Şu Ana Kadar Sabitlenmiş Kararlar (ROADMAP.md + Docs/DECISION_LOG.md'den derlendi)

Bunlar zaten kod tarafında uygulanmış/kararlaştırılmış gerçekler — çelişkiye düşmemek için referans:

- **Harita:** Hex grid, pointy-top, axial koordinat. ~~Bölüm 1 dünyası = 9 harita, 3x3 snake dizilim~~
  → **YANLIŞ/GEÇERSİZ (Sherlock, 2026-07-27):** böyle bir mekanik hiç kararlaştırılmadı. Doğrusu:
  **1 bölüm = 1 harita**, toplam **8 bölüm** (her biri kendi temalı elementiyle — bkz §3). Watson'ın
  yakın zamanda kurduğu 3×3 "HARİTA" ekranı (`WorldMapView`/`WorldGridManager.CurrentMap`, bkz
  DECISION_LOG 2026-07-26) bu yanlış varsayıma dayanıyor olabilir — bkz INBOX_TASKS.md yeni görev.
- **Karo (tile) spec:** footprint 1.90×1.645 m, kalınlık 0.30 m sabit, pivot alt-orta, FBX -Z Forward/Y Up.
- **Öz (Essence) sistemi:** ~~3 tip — Ateş / Su / Toprak~~ → **GEÇERSİZ (Sherlock, 2026-07-27):**
  sadece bir denemeydi. Bölüm 1 özleri **Taş + Doğa** (terrain'in kendisinden türer, ayrı boyama
  yok) — bkz §3. İleriki bölümler kendi temalı elementini getirecek.
- **Zaman/AP:** ~~9 AP = 1 dilim, 6 dilim = 1 gün (54 AP/gün)~~ → **DEĞİŞTİ (Sherlock, 2026-07-27):**
  bölüm 1 için **24 AP/gün**'e güncellendi — bu oturumun TÜM denge simülasyonu (öz hedefi, collapse
  zamanlaması, 10-seed havuzu) 24 AP/gün varsayımıyla yapıldı, `TimeSlotConfig.asset` buna göre
  güncellenmeli (bkz INBOX_TASKS.md TASK-005). Eski 54 değeri hiçbir zaman gerçek bir denge
  tartışmasından geçmemişti (bkz eski not aşağıda) — bilinçli bir değişiklik, hata değil. Gün 4'ten
  itibaren Dünyanın Çöküşü fikri de netleşti: **gün 10'dan itibaren** kademeli karo silinmesi/
  zorlaşma, **gün 14'te sert kesim** — bkz §3, TASK-007.
  *(Eski not: "3 AP" yazan versiyon DECISION_LOG'un eski/Faz-1 anlatısından kopyalanmıştı, kod
  9'a güncellenmişti; şimdi 24'e değişti.)*
- **Sınıflar (planlı 5):** Barbar, Okçu, Büyücü, Rahip, Serseri + komutan **Kam** (zorunlu, ücretsiz, mana/büyü sistemi).
- **Üretim tarifleri (örnek, ayarlanabilir):** Savaşçı = 2 Ateş + 1 Toprak, Ranger = 2 Su + 1 Toprak.
- **Savaş:** Tur tabanlı, hıza göre initiative; kayıp koşulu = Kam'ın ölümü. ~~(tüm run kaybı,
  roguelite reset)~~ → **DÜZELTİLDİ (Sherlock, 2026-07-27):** ceza tüm run'ın DEĞİL, sadece o
  bölümün (=o harita) baştan başlaması — bkz §3. Diğer kayıp koşulları (süre/collapse) için de aynı
  kapsam geçerli, Kam ölümü ayrıca ağırlaştırılmıyor.

Detaylı tarihsel gerekçeler için `Docs/DECISION_LOG.md`.

---

## 1) Harita Boyutları

*(Henüz bu PC'de tartışılmadı — dolduracağız.)*

| Harita/Bölüm | Boyut | Not |
|---|---|---|
| | | |

---

## 2) Yaratık / Birim Statları

*(Henüz bu PC'de tartışılmadı — dolduracağız.)*

| Birim | HP | Atk | Def | Speed | Move Range | Attack Range | Not |
|---|---|---|---|---|---|---|---|
| | | | | | | | |

---

## 3) Denge Kararları — Açık Notlar

*(Tartıştıkça buraya kısa gerekçeli kararlar eklenir; format: **Karar** — Neden — Tarih)*

**Karar:** Bölüm 1 açılış haritası 22×25 hex, ~%20 engel (sık orman/dağ/göl/nehir+köprü geçit). Öz,
ayrı bir "node" değil, **karonun kendisi** — 6 yürünür alt-tip: ova (öz yok), taşlık ova (1 taş),
bol taşlık ova (2 taş), az ağaçlı ova (1 doğa), orman (2 doğa), nadir yüksek orman (3 doğa, nadir).
Öz **TEK SEFERLİK** — toplanan karo tükenir. — Neden: terrain'den organik türeyen ekonomi, ayrı
saçılmış node'lardan daha doğal; `Docs/Balance/tools/harita_terrain_v2.py` + `harita_map1_sim.py`
ile simüle edildi. — 2026-07-27.

**Karar:** Haritayı bitirmek için **~70 öz harcanması** hedefleniyor; mevcut terrain ağırlıklarıyla
erişilebilir arz ~295 (taş 79 + doğa 216, ~4.2× tampon). Fazlalık BİLİNÇLİ — oyuncu her yere
gidemez/gitmek istemeyebilir (savaşa AP ayırmak, engelli bölgeler vb.). Taş/doğa dengesi de KASITLI
EŞİTSİZ — üretim tarifleri zaten dengeli olmayacak. — 2026-07-27.

**Karar:** Bölüm 1'in özleri (**taş + doğa**) SADECE bu bölüme özel — ileriki bölümler kendi temalı
elementini getirecek (örn. bölüm 2 ~ ateş/volkanik, bölüm 3 ~ teknoloji; toplam **8 bölüm**, her biri
farklı element/tema). Kodda şu an duran Ateş/Su/Toprak enum'u sadece bir DENEMEYDİ, iptal edildi —
bkz §4 (Watson bilgilendirilmeli). — 2026-07-27.

**Karar:** Zindan/encounter zorluğu girmeden ÖNCE oyuncuya gösterilir (şeffaf risk) — ama ödül
değişkenliği yüksek olabilir, çok iyi de çok değersiz de çıkabilir. İlke: **riski bil, ödülü bilme.**
— 2026-07-27.

**Karar (taslak, sayılar netleşmedi):** Gün 10'dan itibaren hem zindan/encounter maliyeti kademeli
artar hem de harita karoları kademeli silinmeye başlar (örn. gün10:10, gün14:60 karo silinmiş —
placeholder sayılar). Gün 14'te sert kesim (harita ilerlenemez hale gelir). Silinecek karolar
görsel olarak ÖNCEDEN çatlar/telegraph edilir — **sessiz silinme YOK** (oyuncu göremediği bir şeyi
"haksızca" kaybetmemeli). — 2026-07-27.

**Karar:** Kayıp kapsamı = **sadece o bölüm/harita** baştan başlar (tüm 8-bölümlük run DEĞİL).
Güvende kalan: kalıcı roster (üretilmiş birimler+seviyeleri), Meta-Öz, Meta-Öz ile açılan kalıcılar
— zaten "harcanan öz kalıcı birime dönüşür" kuralı bunu doğal olarak sağlıyor, ayrı bir "banka"
mekaniği gerekmiyor. Riskte kalan: o bölümdeki harcanmamış ham öz + keşif ilerlemesi. Kam ölümü
ayrıca ağırlaştırılmıyor, aynı kural geçerli. — Neden: "tüm ilerlemeni kaybet" çok acımasız, "hiçbir
şey kaybetme" anlamsız; ortada, ergonomik bir ceza. — 2026-07-27.

**Karar:** Bir bölüm/harita başarısız olup TEKRAR başlatıldığında, harita **YENİDEN PROSEDÜREL
ÜRETİLMELİ** (yeni seed) — aynı layout tekrar yüklenirse oyuncu haritayı ezbere bilir, tüm rota/
keşif gerilimi (bkz simülasyon gap%) anlamsızlaşır. Bu oturumun simülasyon araçları zaten
`seed`parametresiyle prosedürel kuruldu (`harita_terrain_v2.generate_terrain(w,h,seed,...)`) — yani
alt yapı buna hazır, gerçek oyunda da terrain/node üretimi hazır-elle-çizili tek bir harita yerine
aynı yöntemle (seed'e göre) kurulmalı. Netleşmesi gereken: İLK deneme de mi prosedürel, yoksa
sadece BAŞARISIZLIK SONRASI retry mi yeniden üretiliyor (ilkinde elle yerleştirilmiş özel içerik —
boss, zorunlu görev konumları — olabilir)? — 2026-07-27.

**Karar:** Retry'de prosedürel üretim (yukarıdaki karar), **sabit 10 haritalık bir havuzdan seçim**
olarak uygulanacak — sonsuz/tam rastgele üretim değil. Sebep: 300 rastgele seed tarandığında
**medyan gap %0** çıktı (ortalama %3.1) — yani rastgele üretilen haritaların büyük çoğunluğu
"kolayca tam toplanabilir", gerçek rota-bulmacası nadir (~%3-5 seed'de). Bu yüzden 300 aday içinden
adalet/oynanabilirlik filtresini geçen (parçalanmamış, 70-öz hedefine gün 10-12'de ulaşılabilir,
zorunlu görevler erişilebilir) en yüksek gap'li **10 seed elle seçildi** — maliyeti yok, "kafa yorma"
hedefine rastgele seçimden çok daha tutarlı hizmet ediyor. Seçim script'i:
`Docs/Balance/tools/harita_seed_secimi.py`. **Havuz (gap12'ye göre yüksekten düşüğe):**

| Sıra | Seed | Bağlantı% | Öz Arzı | Gün8 Öz | Gap12% |
|---|---|---|---|---|---|
| 1 | 89 | 74.4% | 256 | 65 | 23.1 |
| 2 | 7 | 78.9% | 295 | 69 | 18.9 |
| 3 | 20 | 79.3% | 302 | 45 | 18.3 |
| 4 | 108 | 75.1% | 277 | 82 | 17.4 |
| 5 | 219 | 72.2% | 279 | 86 | 16.8 |
| 6 | 64 | 78.9% | 272 | 79 | 16.6 |
| 7 | 173 | 79.5% | 245 | 90 | 15.9 |
| 8 | 283 | 74.7% | 273 | 63 | 14.9 |
| 9 | 141 | 76.0% | 262 | 44 | 14.4 |
| 10 | 286 | 76.4% | 268 | 67 | 14.3 |

Retry'de bu 10 seed'den biri (rastgele veya sırayla) seçilir; hangi yöntemin kullanılacağı
(rastgele/sırayla/son oynanandan farklı) Watson'ın implementasyon tercihi olabilir. — 2026-07-27.

**Karar (fikir aşamasında):** Mevcut §5'teki **Meta-Öz** kavramı, bölümler arası ekonomi sürekliliği
sorusuna çözüm olarak kullanılabilir: roster/ilerleme kalıcı, o bölümün HAM özü (taş/doğa) o bölümde
kalır/taşınmaz (zaten bir sonraki bölümde tematik olarak anlamsız olurdu). Bölüm sonunda kalan ham
özün küçük bir kısmı Meta-Öz'e çevrilebilir; Meta-Öz ile market'te genel/oyunu bozmayan QoL yükseltmeleri
alınabilir (örn. "günde 2 karo ışınlanma"). NOT: bu tarz kalıcı yükseltmeler sonraki bölümlerin AP/
hareket ekonomisini değiştirir — o bölümleri dengelerken hesaba katılmalı. — 2026-07-27.

---

## 4) Bekleyen Sorular (Unity tarafına / kod tarafına yönelik)

*(Kod tarafının netleştirmesi gereken açık noktalar buraya düşer.)*

- ~~[ÇELİŞKİ — Watson, 2026-07-26] AP/dilim değeri~~ → **ÇÖZÜLDÜ (Sherlock, 2026-07-26):** §0'daki
  "3 AP" benim hatamdı, DECISION_LOG'un eski/Faz-1 anlatısından kopyalamıştım; kod zaten (commit
  `bc0aa71`) 9'a güncellenmişti. §0 düzeltildi, koda dokunulmadı. Bu, AP ekonomisinin dengelendiği
  anlamına gelmiyor — sadece mevcut durumu doğru yansıtıyor. §2/§3 (statlar, denge notları) dolunca
  AP pacing'i (9/dilim, 54/gün) gerçek bir denge konusu olarak yeniden ele alabiliriz.

- **[AÇIK — Sherlock, 2026-07-27] "Bölüm" ile "harita" ilişkisi net değil.** §0'da "Bölüm 1 dünyası
  = 9 harita, 3x3 snake dizilim" yazıyor — yani 1 bölüm birden fazla haritadan oluşuyor. Ama bu
  oturumda tartışılan "8 bölüm, her biri kendi temalı elementiyle" fikri bazen "1 bölüm = 1 harita"
  gibi kullanıldı (bkz `Docs/Balance/HARITA_DENGE_DURUM.md`). Netleşmesi gereken: 8 bölümün her biri
  kendi 9-haritalık 3x3 snake dünyasına mı sahip (toplam 8×9=72 harita), yoksa yapı basitleşip
  "bölüm"="harita" mı oldu? Harita-1 için kalibre ettiğimiz sayıların (70 öz hedefi, 22×25 boyut,
  gün14 collapse) hangi ÖLÇEKTE geçerli olduğu buna bağlı.

- **[AÇIK — Sherlock, 2026-07-27] Roguelite reset kapsamı net değil.** §5: "süre bit / harita çök /
  Kam öl → başa sar, Meta-Özler kalıcı" — bu "başa sar" TÜM RUN'ın sıfırlanması mı (muhtemelen
  bölüm 1'in başına), yoksa sadece o anki haritanın tekrarı mı? Harita-1 için tasarladığımız "gün 14
  collapse" cezasının ağırlığı bu cevaba göre çok değişir (bu haritayı tekrar et vs. tüm ilerlemeni
  kaybet) — ayrıca ilk haritalarda daha AFFEDİCİ bir ceza olması önerildi (oyuncu sistemleri henüz
  öğreniyor), bu da hangi reset-kapsamının seçildiğine bağlı olarak ayarlanmalı.

- **[BİLGİ — Sherlock, 2026-07-27] Watson'ın kodundaki Ateş/Su/Toprak öz sistemi ARTIK GEÇERSİZ.**
  Bölüm 1 özleri Taş+Doğa; ileriki bölümler kendi temalı elementini getirecek (toplam 8 bölüm, her
  biri farklı element/tema — bkz §3). Watson'a görev yazılırken bu eski varsayımla karışmasın diye
  açıkça belirtilmeli.

---

## 5) Hikaye & Mekanik Referansı — "Kutsal Kitap" (Unity/Watson tarafı, hafıza yedeği)

> **TASK-001 (2026-07-26):** Aşağısı Unity tarafı Claude ("Watson") hafızasındaki `reference-game-design`
> notunun repo yedeğidir — kaynak belge `C:\3D OYUN\Gerekli belgeler\oyun hikaye ve mekanik.docx`.
> Bölüm 0-4 (bu PC'nin sistematik/sayısal kararları) **canonical**; bu bölüm hikaye/mekanik iskeleti
> içindir (kapsam ayrımına göre hikaye Unity tarafının alanı). İçerik kaybı olmasın diye ÜSTÜNE
> YAZILMADAN eklendi. Sınıf statları burada TASLAK'tır; sayısal denge bölüm 2'de kararlaştırılır.

### Hikaye (Türk mitolojisi)
- **Kayra Han** (Yaratıcı) evrenin kalbine **Ulu Kayın**'ı (Hayat Ağacı) dikti. Ruh döngüsü: dallardan
  dökül → yaşa → köklerden göğe (huzura) dön.
- **Erlik Han** (yeraltı/karanlık efendisi) bu döngüye başkaldırdı; unutulmaktan korkup ruhların göğe
  dönüşünü "israf" gördü. Demir krallığı **Tamag**'ı güçlendirmek için ruhları çalıp saf enerji
  özütlerine = **Töz**'lere dönüştürdü.
- Erlik'in ölüm elçileri **Aldacılar** yeryüzünü istila etti. Son **Kam** (Şaman = ana karakter / oyuncu)
  yuvanın çığlığıyla uyanır.

### Çekirdek döngü (roguelite)
Overworld keşif → görev düğümüne gir → savaş → ödül seç (draft) → overworld'e dön … ölünce/süre
bitince/harita çökünce başa sar, **Meta-Özler** ana menüye taşınır → kalıcı kilit açımı.

### Keşif mekanikleri (makro harita)
- **Savaş Sisi:** Hex harita tamamen karanlık; bastığın yer aydınlanır. **Kule (Watchtower)** yapısına
  ulaşınca o bölgenin sisi KALICI kalkar.
- **Kut (AP / Aksiyon Puanı):** ilahi yaşam enerjisi. 1 AP = 1 hex ilerle / Töz topla / savaş alanına gir.
- **Zaman baskısı:** Her **3 AP → zaman çarkı 1 dilim**. **6 dilim = 1 Gün** (4 Gündüz + 2 Gece).
- **Dünyanın Çöküşü:** 3. gün sonu → 4. güne devrederken Erlik harita karolarını rastgele silmeye başlar
  (uçurumlar); çıkışa ulaşmak hayatta kalma olur.

### Ekonomi + karakter evrimi
- **Töz/Öz** = temel para birimi; haritadan/savaştan toplanır. **Elementsel/renkli**: Kırmızı (Ateş),
  Mavi (Su) vb. — bunlar eski savaşçıların anılarıdır. (ÖZ DEPOSU UI'da **3 ayrı sayaç** → çok-tipli öz;
  kod artık çok-tipli EssenceWallet.)
- **Asker Kartları:** Kitap menüsünde öz harcayarak asker sınıflarını savaşa hazırlarsın.
- **Evrim (Level 1/2/3):** Öz feda ederek sınıf seviye atlar, ölümcül pasifler kazanır (örn.
  Rogue → "ekstra 2 hex zıplama" / "kritik").
- **Kam Skill Tree:** Kam'ın kendi yetenek ağacından pasifler + aktif büyüler (Alev Topu, İyileştirme) öz ile açılır.

### Taktiksel savaş (ritüel)
- Görev düğümüne (Mezarlık/Tapınak) girince makro harita kapanır, engelli **Battlefield**'a inilir.
  "Çatışma değil, ata çağırma ritüeli."
- **Deployment:** Kam ZORUNLU iner. Etrafındaki **mavi grid**lere envanterdeki **Töz harcayarak** asker
  kartları yerleştirilir (hepsini harca = dev ordu / sakla = Boss'a).
- **İnisiyatif:** hıza göre tur sırası (XCOM/Banner Saga). Hareket + özel yetenek.
- **Kam Manası:** savaş başına kısıtlı **Mana Havuzu** (örn. 10) ile Skill Tree büyülerini yağdırır.
- **Permadeath:** Asker ölürse o savaş için silinir (harcanan öz boşa). **Kam ölürse Run biter.**

### Ganimet + meta-ilerleme
- **Drafting:** savaş kazanınca **3 ödülden** seç (Pot, ekstra öz, silah…) → overworld.
- **Roguelite reset:** süre bit / harita çök / Kam öl → başa sar; kazanılan **Meta-Özler** kalıcı.
- **Kalıcı Diriliş:** ana menü Kitap ekranında meta-öz harcayıp yeni sınıf (Büyücü, Şifacı) / başlangıç
  avantajı aç.

### Asker sınıfları (TASLAK statlar — sayısal denge bölüm 2'de kararlaştırılacak; isim eşlemesi için tut)
İsim eşlemesi: **Barbar↔WARRIOR/Savaşçı, Okçu↔RANGER, Büyücü↔MAGE, Rahip↔HEALER/Şifacı, Serseri↔Rogue**
(kitap UI'da 4 slot: WARRIOR/MAGE/HEALER/RANGER; Serseri muhtemelen kilitli/ekstra sınıf).

| Sınıf | Menzil | Yürü | Can | Hasar | Krit | Aktif / Evrim (taslak) |
|---|---|---|---|---|---|---|
| Barbar | 1 | 2+1 | 42 | 6-11 | 15 | 2 vuruş · L1 alan · L2 x2+alan · L3 %25 can çalma+x2+alan |
| Okçu | 3 | 2+1 | 26 | 3-13 | 18 | üst karo bonusu · L1 5 karodan vur · L2 2 hedef · L3 2 hedef+zehir |
| Büyücü | 2 | 2+1 | 34 | 7-9 | 12 | 1 tur eylemsizlik · L1 1 karo sektir · L2 portal+vuruş · L3 dondurma |
| Serseri | 1 (görüş 4) | 3+1 | 24 | 5-7 | 14 | L1 2 karo zıpla+vuruş · L2 yön sıçra · L3 görünmezlik→garanti krit |
| Rahip | 2 | 1+1 | 46 | 2-4 alan / 6-8 yakın | — | vurduğu hasarın 2× iyileştirme · L1 vuruş→dosta can · L2 bariyer · L3 saldırı engelle |

