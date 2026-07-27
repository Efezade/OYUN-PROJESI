# Harita / Öz Ekonomisi Denge Çalışması — Devam Eden Durum (Sherlock oturum notu)

> Bu dosya bir **oturum devamlılık notudur** — "nerede kalmıştık" dendiğinde buradan devam
> edilir. İçindeki HİÇBİR SAYI KİLİTLİ DEĞİL, hepsi tartışma/simülasyon aşamasında taslak.
> Watson'ın kodundaki mevcut değerler de aynı şekilde ilk-geçiş/taslak kararlar.

## Bu oturumda netleşenler (2026-07-27, uzun oturum — GAME_DESIGN.md §3/§4'e de işlendi)

**Model/araçlar:** `Docs/Balance/tools/` altında bir simülasyon seti kuruldu — harita "Orienteering
Problem" (bütçeli ödül toplama, hex grid) + kısmi görüş (fog of war) olarak modelleniyor. Akademik
karşılığı: OP + Team Orienteering Problem (çoklu gün) + Prize-Collecting TSP (zorunlu node).
`harita_sim.py`(v1, düz) → `harita_sim_v2.py` (tip'e göre değer/maliyet farklı node, zaman-bağımlı
zindan zorluğu, "value_ratio" greedy) → `harita_terrain_v2.py` (gerçek terrain taksonomisi) →
`harita_map1_sim.py` (bölüm-1 haritasının gerçek node/terrain modeli). `gap% = (planlı−greedy)/planlı`
= rota seçiminin ne kadar önemli olduğunun ölçüsü; hedef bant ~%20-35.

**Harita 1 içeriği (kararlaştırıldı, GAME_DESIGN.md §3'e işlendi):**
- 22×25 hex, ~%20 engel: sık orman / dağ / göl / nehir (+ köprü geçit).
- Öz = ayrı node değil, **karonun kendisi**: ova(0), taşlık ova(1 taş), bol taşlık ova(2 taş),
  az ağaçlı ova(1 doğa), orman(2 doğa), nadir yüksek orman(3 doğa, nadir). **Tek seferlik** (tükenir).
- İnteraksiyon: encounter (hafif, tekrar edilebilir savaş), zindan (ağır, yan görev, değişken
  zorluk+ödül), 3× harita-kurtarma görevi (zorunlu, sabit konum, sis'ten bağımsız hep bilinir),
  1× ana boss (**konumdan bağımsız**, her yerden girilir, rota modelinin dışında), 2× portal (saf
  kolaylık, zorunlu değil), gündüz+gece mistik marketi (sabit konum, sayı TBD), gözetleme kulesi
  (5x5 sis açar, kalıcı — sis zaten hiç geri kapanmıyor).
- **Hedef: haritayı bitirmek için ~70 öz harcanması.** Mevcut ağırlıklarla arz ~295 (4.2× tampon) —
  BİLİNÇLİ fazlalık, sorun değil. Taş/doğa dengesi de kasıtlı eşitsiz (tarifler zaten dengeli
  olmayacak).
- Zindan/encounter: zorluk ÖNCEDEN gösterilir (şeffaf risk), ödül değişkenliği YÜKSEK olsun (çok
  iyi/çok değersiz çıkabilir) — "riski bil, ödülü bilme" ilkesi.
- Gün 10'dan itibaren zindan/encounter maliyeti kademeli artar + harita karoları kademeli silinmeye
  başlar (taslak sayı: gün10:10 → gün14:60 karo), gün 14 sert kesim. Silinecekler ÖNCEDEN
  çatlar/telegraph edilir — sessiz silinme yok (adaletsizlik hissi riski).
- Bölüm 1 özleri (taş+doğa) SADECE bu bölüme özel; ileriki bölümler (toplam 8 bölüm) kendi temalı
  elementini getirecek (örn. bölüm2~ateş/volkanik, bölüm3~teknoloji). **Ateş/Su/Toprak kod enum'u
  artık geçersiz/deneme amaçlıydı.**

**Simülasyon bulguları (gap% üzerinden):**
- Görüş menzili en güçlü lever ama oyuncuyu boğma riski var diye vision=1'e ÇEKİLMEDİ.
- Engel yoğunluğu (%10-20) gap'i DÜŞÜRÜYOR (dar koridorlar greedy'yi otomatik optimale kanalize
  ediyor) — sezgiye aykırı, dikkat edilmeli.
- Özü seyrekleştirmek de gap'i düşürüyor (az içerik + bol bütçe = herkes her şeyi topluyor) —
  "az içerik = zor" sezgisi burada YANLIŞ çıktı, asıl mesele içerik/bütçe oranı.
- Gün10-14 kademeli collapse mekaniği: gap'i hafif artırıyor (%18.9→%20.2, gün 12) ama asıl faydası
  gap'ten çok TEMPO — sonsuz oyalanmayı engelliyor, gün 14 sonrası ilerleme donuyor (test edildi).
- **ÖNEMLİ SINIRLAMA:** bu model sadece MAKRO/rota katmanını ölçüyor (hangi hex'e ne sırayla
  gidilir). Savaş içi risk/kaynak yönetimi (zindana girmeye değer mi, kayıplar) TAMAMEN AYRI bir
  katman ve simüle edilmiyor — düşük makro-gap endişe verici olmayabilir, asıl "kafa yorma" o
  katmanda olabilir. Sayıları daha fazla inceltmek yerine gerçek insan playtest'i önerildi.

**Taslak harita görseli:** parşömen temalı, canvas tabanlı hex harita + terrain/node istatistik
paneli yayınlandı (artifact). Sürekli güncellenebilir, en son taksonomiyle (encounter/zindan ayrımı,
6 terrain alt-tipi) henüz yeniden çizilmedi — istenirse güncellenir.

## Kapanan sorular (2026-07-27 içinde çözüldü)

- ~~"Bölüm" vs "harita" ilişkisi~~ → **ÇÖZÜLDÜ:** 1 bölüm = 1 harita, toplam 8 bölüm. GAME_DESIGN.md
  §0'daki "9 harita/3x3 snake" bilgisi YANLIŞTI, düzeltildi. Watson muhtemelen buna dayanan bir UI
  (`WorldMapView`) kurmuş — **TASK-004** olarak INBOX_TASKS.md'ye düşürüldü, Watson kontrol edecek.
- ~~Roguelite reset kapsamı~~ → **ÇÖZÜLDÜ:** kayıp = sadece o bölüm/harita baştan başlar, TÜM RUN
  değil. Kalıcı roster/Meta-Öz güvende, harcanmamış ham öz+keşif riskte. Kam ölümü ayrıca
  ağırlaştırılmıyor. GAME_DESIGN.md §0/§3'e işlendi.

## Kapanan sorular (devamı, 2026-07-27)

- ~~Harita retry'de prosedürel üretim gerekli mi~~ → **ÇÖZÜLDÜ:** ilk deneme DAHİL hepsi prosedürel,
  ama sonsuz rastgelelik değil — 300 aday seed taranıp adalet/oynanabilirlik filtresini geçen en
  yüksek-gap'li **10 seed sabit havuz** olarak seçildi (script: `tools/harita_seed_secimi.py`,
  liste: GAME_DESIGN.md §3). **Önemli yan bulgu:** rastgele seed'lerin medyan gap'i %0 — yani
  rota-bulmacası derinliği DOĞAL olarak nadir, elle seçim şart, rastgele 10 almak yeterli olmazdı.

## Açık sorular / bir sonraki adım (KALDIĞIMIZ YER)

1. **Encounter/zindan gerçek zorluk/ödül sayıları** — kullanıcı "değişken ama aşırı değil, yeniden
   hesaplanacak" dedi, henüz somut sayı yok.
2. **Seviye atlama (level) sistemi tanımsız** — bu oturumda hiç netleşmedi, ayrı tartışma konusu
   olarak bekletiliyor (kullanıcı notu 2026-07-27).
3. **Collapse/zorlaşma taslak sayıları** (gün10:10→gün14:60 karo, ×2 zindan maliyeti) gerçek
   playtest'ten geçmedi, kullanıcı "atıyorum" diyerek örnek verdi — son sayı değil.
4. **Gözetleme kulesi** maliyeti/haritada kaç adet olacağı belirsiz.
5. Portal + gece mistik marketi **ileriki bölümlere ertelenmesi öneriliyor** (Watson tarafı önerisi,
   kullanıcı koşullu kabul etti) — kesinleşmedi.
6. ~~Watson'a implementasyon görevi henüz YAZILMADI~~ → **YAZILDI (2026-07-27):** kullanıcı kalan
   detayları (encounter/zindan sayıları, seviye sistemi, collapse'ın kesin sayıları) kendi oynayıp
   netleştirmenin daha verimli olacağına karar verdi — teorileştirmeye devam etmek yerine **TASK-005/
   006/007** olarak INBOX_TASKS.md'ye yazıldı (terrain+AP ekonomisi / node sistemi / collapse+retry),
   commit+push edildi (`7004f23` sonrası). Ayrıca fark edilen önemli bir uyumsuzluk düzeltildi: bu
   oturumun TÜM hesapları 24 AP/gün varsayıyordu ama kodda 54 AP/gün vardı — TASK-005'te 24'e
   değiştirilmesi istendi (GAME_DESIGN.md §0'a işlendi). Watson TASK-003/004'ten sonra sırayla bu
   üçünü işleyecek, her birinde `awaiting_review`'da durup onay bekleyecek.

## Paralel/arka plan durumu
- Watson'daki **TASK-003** (DECISION_LOG.md temizlik geçişi) `Docs/INBOX_TASKS.md`'de hâlâ
  **pending** (2026-07-27 itibariyle kontrol edildi, değişmemiş).
- Dosya sahipliği kuralı aktif: DECISION_LOG.md=Watson, INBOX_TASKS.md=append-only,
  GAME_DESIGN.md=Sherlock (bkz CLAUDE.md §9).
- Watson'a henüz somut bir implementasyon görevi YAZILMADI — önce yukarıdaki açık soruların
  (özellikle #1, #2) netleşmesi bekleniyor, sonra INBOX_TASKS.md'ye düşürülecek.

## Devam etmek için ilk adım
Yeni oturumda: `git pull` yap (Watson TASK-004'ü işlemiş olabilir), sonra "Açık sorular"
bölümündeki **1. maddeden** (harita retry'de prosedürel yeniden üretim — ilk deneme de mi,
sadece retry mi) devam et.
