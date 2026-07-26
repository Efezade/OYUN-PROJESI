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

- **Harita:** Hex grid, pointy-top, axial koordinat. Bölüm 1 dünyası = **9 harita, 3x3 snake dizilim**, kenardan geçişli.
- **Karo (tile) spec:** footprint 1.90×1.645 m, kalınlık 0.30 m sabit, pivot alt-orta, FBX -Z Forward/Y Up.
- **Öz (Essence) sistemi:** 3 tip — Ateş / Su / Toprak (Kırmızı/Mavi/Yeşil). Haritaya elle boyanıyor (Essence Painter).
- **Zaman/AP:** 3 AP = 1 dilim, 6 dilim = 1 gün. Gün 4'ten itibaren Dünyanın Çöküşü (karo silinmesi).
- **Sınıflar (planlı 5):** Barbar, Okçu, Büyücü, Rahip, Serseri + komutan **Kam** (zorunlu, ücretsiz, mana/büyü sistemi).
- **Üretim tarifleri (örnek, ayarlanabilir):** Savaşçı = 2 Ateş + 1 Toprak, Ranger = 2 Su + 1 Toprak.
- **Savaş:** Tur tabanlı, hıza göre initiative; kayıp koşulu = Kam'ın ölümü (tüm run kaybı, roguelite reset).

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

---

## 4) Bekleyen Sorular (Unity tarafına / kod tarafına yönelik)

*(Kod tarafının netleştirmesi gereken açık noktalar buraya düşer.)*

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

