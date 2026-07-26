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
