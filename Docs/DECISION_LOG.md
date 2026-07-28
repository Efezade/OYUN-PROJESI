# Karar Günlüğü (DECISION_LOG)

> **Ne için:** Geriye dönük **NEDEN** kaydı — hangi karar verildi, neden, hangi commit'te, varsa ne
> öğrendik. Ters-kronolojik (en yeni en üstte).
> **Sahiplik:** Watson (Unity/kod tarafı) yazar; Sherlock yalnız okur.
> **Ne YOK burada:** güncel durum / "son push" / "nerede kaldık" blokları (bayatlıyor — canlı kaynağı
> Watson'ın hafızası), dosya-alan enumerasyonu, tamamlanmış fazların blow-by-blow anlatımı.
>
> **Kardeş dosyalar:** `ROADMAP.md` = ileriye dönük plan · `Docs/GAME_DESIGN.md` = canonical
> tasarım/denge sayıları (tek doğruluk kaynağı) · `Docs/DECISION_LOG_ARCHIVE.md` = tam tarihsel arşiv ·
> `Docs/TILE_PIPELINE.md` = karo import hattı · `Docs/UI_LAYOUT.md` = ekran taslakları ·
> `Docs/INBOX_TASKS.md` = görev kuyruğu. Aynı şeyi ikisinde anlatma.
>
> **Yeni giriş formatı:** `KARAR / NEDEN / COMMIT / (varsa) DERS`. Dosya listesi yazma — `git show <hash>`
> zaten söylüyor. Bir gotcha bulduysan aşağıdaki **Tuzaklar/Dersler** bölümüne de bir satır ekle.

---

## 2026-07-28 — TASK-003: DECISION_LOG temizliği

**KARAR:** Bu dosya yalın "aktif kararlar + tuzaklar" logu oldu; tüm blow-by-blow tarih
`Docs/DECISION_LOG_ARCHIVE.md`'ye taşındı (tek satır özet + commit hash + link geride kaldı).
Bayat "güncel durum / son push / DÖNÜŞTE SIRADAKİ İŞLER" bloğu ana logdan **silindi** — arşivde
"BAYAT" etiketiyle tarihsel kayıt olarak duruyor (içerik kaybı yok).
**NEDEN:** Dosya 38 KB'a şişmişti ve büyük kısmı ya tamamlanmış faz detayı ya da bayat durum notuydu;
Sherlock'un okuması gereken "hangi karar hâlâ yürürlükte" bilgisi arasında kayboluyordu. Ayrıca
"son push/sıradaki iş" bilgisi Watson'ın hafızasıyla çelişme riski taşıyordu.
**Ek bulgu (Sherlock'a):** Ana log 2026-06-25'te kesiliyordu — **2026-06-27 → 2026-07-25 arasındaki
işlerin hiçbiri (KÜP → 9-harita geçişi, sis yeniden yazımı, portal, XCOM iki-tık hareket, gece/gündüz
AP döngüsü, uGUI menü/KİTAP) loglanmamıştı**, yalnızca commit mesajlarında duruyordu. Bu boşluk
aşağıdaki "2026-07 — commit'lerden türetilmiş özet" bölümüyle kapatıldı. O bölüm commit mesajlarından
türetildi (o oturumların ayrıntılı notu hiç yazılmamış); yeni bilgi uydurulmadı.
**COMMIT:** (bu giriş)
**DERS:** Log ancak yazıldığı sürece log — oturum sonunda commit'lemek yetmiyor, kararı buraya da
düşmek gerekiyor.

---

## 2026-07-26 — Store.cs derleme hatası

**KARAR/DÜZELTME:** `Store.cs`'e eksik `using TacticalRPG.Grid` eklendi.
**NEDEN:** `HexGridManager` niteliksiz kullanılıyordu.
**COMMIT:** `d44b780`
**DERS:** Tek CS0246 **tüm editor assembly'sini** düşürüyor → `TacticalRPG` menüsünün tamamı kayboluyor.
Menü kaybolduğunda "menü bozuldu" diye aramaya başlama, önce Console'daki derleme hatasına bak.

---

## 2026-07-26 — TASK-002: SessionStart auto-pull hook

**KARAR:** Bu makineye (Watson) SessionStart hook'u kuruldu → oturum başında güvenli `git pull`.
**NEDEN:** İki-PC akışında Watson elle pull yapmayı unutabilir; INBOX/GAME_DESIGN güncelken çalışmak
lazım. Güvenli çünkü: tracked commit'siz değişiklik varsa pull ATLANIR (Unity sık sık sahne/.controller
kirletir), temizse `--ff-only` (asla merge/clobber yok), her zaman exit 0 (oturumu bloke etmez).
**NEREDE:** `C:\3D OYUN\.claude\settings.local.json` + `.claude/hooks/session-pull.sh` — **OYUN
repo'sunun DIŞINDA** (makineye özel; Sherlock etkilenmez).
**COMMIT:** `962b076`
**DERS:** SessionStart olayı çalışan tur içinde tetiklenemez → script'i iki dalda (temiz/kirli) pipe-test
ederek doğrula, canlı ateşleme ancak yeni oturumda görülür. `settings.local.json`'a hook eklerken
watcher mevcut oturumda görmeyebilir (yeni oturum ya da `/hooks` gerekir).

---

## 2026-07-26 — 4 ekran uGUI + Mağaza + TASK-001

**KARAR (UI):** AYARLAR / ÇANTA / HARİTA / KİTAP ekranları uGUI'ye ve ortak "parşömen UI kiti"ne
(`SceneSetupTool.UIKit.cs`) geçirildi; hepsi gerçek içerik + canlı veri gösteriyor (öz deposu,
Kam büyü kartları, sınıf kartları). "ÇANTA/HARİTA" başlık yazıları kaldırıldı — şekil kimliği taşıyor.
**NEDEN:** `game UI.pdf` mockup'larına yaklaşmak + IMGUI'nin cila aşamasına taşınamaması.
**KARAR (Mağaza):** Palete `magaza` karosu (`isStore`) + 5 `ShopItemSO` + `StoreManager` /
`PlayerBuffs` / `StoreHUD`. Öz karşılığı: anlık AP ya da geçici/kalıcı hız-menzil buff'ı (adımla sayılır).
Hook'lar: `ActionPointManager.GrantAP`, `PlayerController.SpeedMultiplier`, `MapInputHandler.BonusMoveRange`.
`SetupStore` TAM KURULUM zincirinde.
**KARAR (TASK-001):** Hafızadaki 3 referans notu repoya yedeklendi → `Docs/GAME_DESIGN.md §5` (canonical
§0-4 korunarak EKLENDİ, üzerine yazılmadı), `Docs/UI_LAYOUT.md`, `Docs/TILE_PIPELINE.md`.
**COMMIT:** `721c3bf` (UI+mağaza) · `72aea8d` (TASK-001)
**AÇIK:** Bu ekranlar **Unity'de görsel olarak henüz doğrulanmadı** (kod+push yapıldı, Play denenmedi).

---

## 2026-07 — commit'lerden türetilmiş özet (o dönem log tutulmamış)

> Aşağıdakiler ilgili commit mesajlarından çıkarıldı; ayrıntılı oturum notu yok. Sayılar o günkü hâl —
> canonical değer için `Docs/GAME_DESIGN.md`.

- **AP / zaman döngüsü** (`f080ab7` 23 Tem, `bc0aa71` 25 Tem): 1 karo = 1 AP, öz toplama = 1 AP,
  savaşa girme = 3 AP; savaş boyunca AP motoru **DONAR** (`SetFrozen`) → tüm savaş 3 AP sayılır.
  6 dilim = 1 gün, ilk 4 dilim gündüz / son 2 gece. Dilim başına AP 3 → **9** yükseltildi (54 AP/gün).
  `DayNightCycle` + `DayNightProfile` (keskin geçiş, lerp yok), gece görüşü yarıya iner,
  `TimeDialHUD` dairesel sayaç, `HudScale` ile tüm IMGUI 1920×1080 sanal ekrana çizilir.
  **⚠ AÇIK:** TASK-005 bunu **24 AP/gün**'e çekecek (GAME_DESIGN §0 gerekçeli) — koddaki 54 geçici.
- **Hareket (XCOM tarzı)** (`a350c9e` 16 Tem): iki-tık — ilk tık `PathPreview` (çizgi + hex çerçeveleri),
  aynı karoya ikinci tık yürütür. Menzil **sise bağlı**: sis varken 2 karo, kule ile kalıcı açılmış adada
  sınırsız.
- **Çöküş (kıyamet)** (`a350c9e`, `5e156da`): ada-bağımsız durum (uzak adadaki çöküş ışınlanma sayacını
  sıfırlamaz, dönüşte kalıcı uygulanır); silinen karo başına kırmızı su dalgası; silinecek karolar
  **1 gün önceden** kırmızı hex-çizgi + kalan-AP etiketi, dalga cephesi geçerken yıldırımla açılır.
  Portal karoları çöküşten muaf. `CollapseConfig` startDay 5 → 3.
- **Adalar arası geçiş = yalnız PORTAL** (`5e156da` 14 Tem, `1cb6679` 16 Tem): kenar-karosu geçişi
  güvenilmez çıktı → **tamamen kaldırıldı** (`WorldGridManager` 300 → 56 satır, `TransitionMarker` silindi).
  Portal çiftleri Tile Painter'dan boyanır; ışınlanmada "toz olma" efekti.
- **Sis (FogOfWar) iki katmanlı yeniden yazım** (`4af644c` 12 Tem, `72fbbc5` 13 Tem): parlaklık çarpanı
  `MaterialPropertyBlock` ile (boyalı karo bozulmaz) + görülmemiş karonun üstüne havuzlanmış bulut kapağı
  (collider'sız, tıklamayı engellemez). `WatchtowerManager`: boyalı "kule" karosu 1 karo yakınından ada
  sisini **KALICI** açar, açık adalar hatırlanır.
- **Karakter modelleri + savaş animasyonu** (`f267b03` 27 Haz, `0570f30`, `f080ab7`): placeholder kapsüller
  gerçek modellere (`CharacterModelBinder`, kalıcı bake); Quaternius CC0 sınıf/düşman modelleri;
  `CharacterAnimationImporter` (klasör başına karakter + `_SharedHumanoid` retarget);
  `TurnOrderBarHUD` (For The King tarzı initiative kuyruğu). Gerçekçi grafik preset'i (ACES + bloom +
  vignette) Faz 0'a girdi → her TAM KURULUM'da gelir.
- **Bölüm 1 dünya yapısı: KÜP → 9 harita 3×3 SNAKE** (`986c6cd`/`9b172c7` 28 Haz → `c415d27` 29 Haz):
  gezegen-küp denemesi (6 yüz, 90° döndürme) **TERK EDİLDİ**, yerine düz 9 harita 3×3 snake dünya +
  `WorldGridManager` + TAB minimap geldi. Küp kalıntıları `72fbbc5`'te silindi.
  **⚠ AÇIK:** "Bölüm 1 = 9 harita" varsayımının kendisi sorgulanıyor (doğrusu: 1 bölüm = 1 harita,
  8 bölüm) → **TASK-004** bunu inceleyecek. Bu satır o incelemenin girdisidir, onaylanmış tasarım değildir.

---

## Yürürlükteki kararlar (özet)

Tarihsel gerekçeleri arşivde; burada sadece **hâlâ geçerli olan** hüküm var.

| Karar | Durum |
|---|---|
| **Tek sahne, durum-tabanlı geçiş** (Overworld/ConfirmMission/Deployment/Combat). Combat'ta grid savaş TileMap'iyle yeniden üretilir; ayrı Unity scene YOK. | yürürlükte (KARAR 1) |
| Deployment **bedava**; öz **birim üretiminde** harcanır (üretim savaş öncesi yerleştirme ekranında). | yürürlükte — eski "öz harcayarak deploy" (KARAR 2) Faz D2'de **iptal** |
| Üretilen birimler roster'da **KALICI** (öz bir kez harcanır, sonraki savaşlarda hazır). | yürürlükte |
| **Kam zorunlu + ücretsiz komutan**; deploy zone alt-ortaya otomatik iner. **Yenilgi = Kam'ın ölümü.** | yürürlükte |
| Savaşta **permadeath**: ölen deploy birimi o savaş için silinir. | yürürlükte |
| Initiative **hıza göre** (XCOM/Banner Saga), eşitlikte oyuncu önce. | yürürlükte |
| Düşman **ana haritada yok** — sadece savaş haritasında spawn. | yürürlükte |
| Öz türleri: **Ateş / Su / Toprak** (Kırmızı/Mavi/Yeşil), toplama 1 AP. | yürürlükte — GAME_DESIGN §3'teki taş/doğa şemasıyla TASK-005'te uyumlanacak |
| Öz haritası **el yapımı** (Essence Painter), rastgele spawn kaldırıldı. | yürürlükte — TASK-005 prosedürel terrain'e geçirecek |
| Mana savaş başında **resetlenmiyor** (havuz overworld'de regen eder). | bilinçli erteleme |
| Min-max hasar + kritik vuruş yok (düz `Attack`). | bilinçli erteleme |
| Adalar arası tek geçiş yolu **portal**. | yürürlükte |
| Karo dokuları FBX'e gömülü değil → karolar şimdilik **renksiz**; kullanıcı kararı: ileride gelişmiş karolar. | bilinçli kabul |

---

## Tuzaklar / Dersler

> Sihirli sabitler ve tekrar etmemesi gereken hatalar. **Bu bölüm arşive taşınmaz.**

**Karo / FBX**
- **Ham tasarımcı FBX'i doğrudan grid karosu OLARAK KULLANILAMAZ.** v1 karoları ölçüldüğünde: her biri
  ~262 birim (1.9 m olmalı), geometri merkez-dışı ~450 birim, 13+ ayrı mesh parçası, 61 MB (gömülü dev
  texture). Toplu import bounding-box'ı 1.9 m'ye sığdırınca **asıl karo nokta kadar küçüldü** → harita kaos.
  Geri alma: `git checkout -- TilePalette.asset ...` (kullanıcının 126 karoluk boyaması korundu).
- **Çözüm checklist'i (Blender):** Join (tek mesh) + Mesh > Clean Up > **Delete Loose** + **Ctrl+A All
  Transforms** + FBX export **-Z Forward / Y Up** + texture'ı embed etme. Önce TEK karo doğrula, sonra kalanı.
  Ayrıntılı hat: `Docs/TILE_PIPELINE.md`.
- **Footprint kalkanı — SİLME.** `TileFolderImporter`: footprint **> 50 birim → palete EKLEMEZ**
  ("ATLANDI — Blender'da düzelt" raporu); > 5 birim veya > 20 mesh → ekler ama uyarır. Yukarıdaki
  262-birimlik felaketin bir daha olmamasının tek sebebi bu kalkan.
- **Köprü FBX quaternion flip:** FBX baş-aşağı geldi; prefab YAML'ında child rotasyonu elle
  `(0.5,-0.5,0.5,0.5)` + `pos.y ≈ 0` yapılarak düzeltildi (180° dikey flip). İlk hâli ayrıca 10× büyük +
  Z-up + pivot tepedeydi.
- Karo FBX'lerinin doku yolları `C:\Users\zeynep\Downloads\*.jpg`'ye referans veriyor → dokular projede
  yok. "Karolar neden renksiz" sorusunun cevabı bu, shader değil.

**Render / sahne**
- URP shader property **`_BaseColor`** (`_Color` DEĞİL). Tint her zaman `MaterialPropertyBlock` ile →
  paylaşılan materyal bozulmaz.
- `DefaultExecutionOrder`: `HexGridManager(-100)` → `FogOfWarManager(-50)` → `PlayerController(0)`.
- Hex karo mesh'i 3B prizma, `TileHeight = 0.3f`.
- Kapsül placeholder'ın `_heightOffset = 0.8` = TileHeight(0.3) + kapsül yarısı(0.45) + boşluk(0.05).
  Gerçek model bake edilince `_heightOffset = TileHeight` (yüzeye sıfır clearance) oldu.
- **Birim Y'si sabit ofset değil:** iki karonun `SurfaceHeight` değeri enterpole edilir. Eski `RaycastAll`
  yöntemi terk edildi — köprüde ayak güverte yerine **kemere biniyordu**.
- Sis bulutu yüksekliği `_fogLift`: 0.45 izometrik kamerada komşu karoları örtüyordu → **0.18**.

**Editor / kurulum**
- **Tek CS0246 tüm editor assembly'sini düşürür** → `TacticalRPG` menüsü komple kaybolur (`d44b780`).
  Menü yoksa önce Console'a bak.
- **TAM KURULUM kullanıcı verisini KORUR:** `EssenceConfig`, `EssenceMap`, palet girişleri ve boyalı
  karolar üzerine yazılmaz (upsert / varsa-koru). Yeni asset oluşturan bir faz yazarken bu kuralı bozma.
- **Yeni faz eklediğinde `FullSetup` zincirine de ekle** — kullanıcı her geliştirmede tek tıkla temiz
  kurulum bekliyor.
- **IMGUI ↔ uGUI çakışması:** menü açıkken IMGUI HUD'lar uGUI'nin üstüne çizer → `MenuState` statik
  bayrağıyla gizlenir; tık sızmasını `EventSystem.IsPointerOverGameObject` + `ImguiBlocker` engeller.

**Süreç / git**
- Unity, `.controller` / TMP fallback / `.mat` dosyalarını kendiliğinden sık sık yeniden serileştirir →
  working tree "kendiliğinden" kirlenir. Auto-pull hook'unun kirli tree'de pull'u atlamasının sebebi bu.
- SessionStart hook'u çalışan tur içinde tetiklenemez; script'i temiz/kirli iki dalda pipe-test et.

---

## Tamamlanan fazlar (tek satır özet + commit)

> Ayrıntılı anlatım, dosya listeleri ve test akışları: **`Docs/DECISION_LOG_ARCHIVE.md`**.

| Faz | Ne getirdi | Commit |
|---|---|---|
| Faz 0-1 | Kamera/sahne kurulumu, 10×10 hex grid, A* hareket, FogOfWar, AP + zaman motoru, MapCollapse, DebugHUD | `5b943ed` → `093f08e` |
| Faz 2 / 2.2 | `CharacterClassData` + `CharacterCard` + öz + Kam mana; `KamAbilityData` SO'ları (3 büyü asseti) | `487b7b3`, `8821d52` |
| Faz 2.5 | Tile Painter (palet tabanlı karo boyama, tint + yürünebilirlik senkronu) | `8da509c`, `60f6d45` |
| Faz 3 | Yetenek dikey dilimi (Unit + AbilityCaster + test HUD) — sandbox, Faz A bunu temizledi | `f46adf4` |
| Faz A | Overworld ↔ savaş durum makinesi + görev alanları (+ savaşa giriş 1 AP) | `239f662`, `5fb7991` |
| Karo hattı | Köprü FBX pipeline + karo yüzey yüksekliği + kontur takibi | `285dd0c` |
| Faz B | Deployment akışı — öz ile birim yerleştirme, `Unit` ↔ `CharacterCard` bağı | `88d9b6d`, `c030fb5` |
| Faz C (C1-C3) | Düşman spawn + hıza göre initiative + tur tabanlı muharebe + win/lose | `bbf507e` |
| Faz C4 | Kam komutan + savaş büyüsü + sınıf renkleri | `6040b9c`, `b6125f8` |
| Faz D | Çok-tipli öz + haritadan toplama + tarifle birim üretme | `14301c9` |
| Faz D2 | Üretim savaşa taşındı, el yapımı öz haritası (Essence Painter), yakınlık istemi | `9087edb` |
| Cila | Savaş nameplate'leri (isim + bölümlü can barı + hasar flaşı) | `9311e52` |
| Karo hattı v2 | Tile Painter "Klasörü Tara" + 11 karo prefab + importer düzeltmeleri | `de55ab6`, `ce7be02` |
