# ALTERNATİF TASARIM — "9 Harita / 3×3 Snake Dünya"

> **Bu bir YEDEK/ALTERNATİF'tir, ölü kod değil.** Kullanıcı talimatı (2026-07-28):
> *"şu anki harita tasarımını alternatif tasarım olarak tut, silme ve bir yere kaydet,
> şu ankinin üzerine gitmiycez."*
>
> **SİLİNMEYECEK.** Yeni harita tasarımı (bkz TASK-004/005) bunun ÜZERİNE değil, YANINA kurulur.
> Yeni tasarım tutmazsa buraya dönülür.

**Kayıt tarihi:** 2026-07-28 · **Canlı olduğu son commit:** `3dafb5f` (o tarihte `Assets/` içinde tam çalışır durumdaydı)

---

## Bu klasörde ne var

`Data/` — 9 haritanın **elle boyanmış** TileMapSO asset'lerinin birebir kopyası + palet:

| Dosya | Ada | Boyalı karo |
|---|---|---|
| `TileMap.asset` | Harita 1 | 101 |
| `Face_2.asset` … `Face_9.asset` | Harita 2-9 | 99-101 (her biri) |
| `TilePalette.asset` | — | karo tanımları (boyamaların çözülebilmesi için) |

**Toplam ~900 elle boyanmış karo.** İçerik: ada başına 1 `kule` (gözetleme kulesi), ada başına 1
savaş karosu (`deneme11`…`deneme20`), 12 portal çifti (`portal1`…`portal12`) ile örülü ada ağı,
ağaç/çiçek/mantar/su/köprü dekoru.

> Bu kopyalar **`Assets/` DIŞINDA** duruyor — Unity onları import etmez, GUID çakışması olmaz.
> Orijinaller hâlâ `Assets/Data/Map/` altında ve **canlı**; buradaki sadece güvenlik kopyası.

---

## Tasarımın özeti (ne işe yarıyordu)

Bölüm 1 dünyası = 9 ayrı ada, kavramsal olarak 3×3 snake dizilimde (`9 8 7 / 6 5 4 / 3 2 1`).
Adalar sahnede **tek tek** yüklenir (aynı origin'e), aralarındaki geçiş **yalnızca portal** ile olur.
3×3 yerleşim iki yerde gerçekten kullanılır: (1) HARİTA ekranı/minimap'teki konum gösterimi,
(2) uzak adadaki çöküşün dalgasının **hangi yönden ve ne kadar uzaktan** geleceği hesabı.

## Bu tasarıma bağlı kod (hepsi hâlâ `Assets/` altında, silinmedi)

| Katman | Dosya | Bağımlılığın türü |
|---|---|---|
| Çekirdek | `Scripts/Core/WorldGridManager.cs` | `_maps[9]`, 1-9 sınır kontrolleri, snake satır/sütun matematiği, `VirtualPositionOnCurrentMap` (3×3 geometri) |
| Çekirdek | `Scripts/Core/TeleportManager.cs` | portal eşini 1-9 arasında tarar |
| Çekirdek | `Scripts/Core/MapCollapseManager.cs` | ada-başına çöküş durumu; uzak-ada dalgası 3×3 yerleşimden türer |
| Çekirdek | `Scripts/Core/WatchtowerManager.cs` | ada-başına kalıcı sis-açma hafızası |
| UI | `Scripts/UI/WorldMapView.cs` | 9 düğüm, index = ada no |
| UI | `Scripts/UI/MinimapHUD.cs` | sabit `Layout = {9,8,7 / 6,5,4 / 3,2,1}` (TAB) |
| Editor | `Scripts/Editor/SceneSetupTool.cs` → `SetupWorld3x3()` | 9 harita asset'ini yükler/oluşturur; TAM KURULUM zincirinde |
| Editor | `Scripts/Editor/SceneSetupTool.BagMap.cs` → `PopulateMapScreen()` | HARİTA ekranındaki 3×3 pin yerleşimi + pusula |
| Editor | `Scripts/Editor/TilePainterWindow.cs` | 9 harita seçici |

## Geri yükleme

Boyamalar bozulur/kaybolursa: `Data/` içindeki `.asset` + `.meta` dosyalarını `Assets/Data/Map/`
üzerine kopyala (`.meta`'ları da al — GUID'ler korunsun, sahnedeki referanslar kopmasın), sonra
Unity'de `Assets → Reimport`. Kod tarafı için: `git show 3dafb5f:Assets/Scripts/Core/WorldGridManager.cs`
(diğer dosyalar için de aynı kalıp).

## Neden alternatife düştü

`Docs/GAME_DESIGN.md §0`'a göre "Bölüm 1 = 9 harita, 3×3 snake" hiç kararlaştırılmamış bir varsayımdı;
doğrusunun **1 bölüm = 1 harita, toplam 8 bölüm** olduğu yazıldı (2026-07-27, Sherlock).
Kullanıcı 2026-07-28'de bu yönde karar verdi (seçenek A) — ama **bu tasarımı silmeden**.
Ayrıntı: `Docs/INBOX_TASKS.md` TASK-004 + `Docs/DECISION_LOG.md` 2026-07-28 girişi.

## Yerine ne geldi (ve neyin hâlâ çalıştığı)

| | Eski (bu alternatif) | Yeni (yürürlükte) |
|---|---|---|
| HARİTA ekranı | 3×3 pin ızgarası, aktif ada altın | 8 bölümlük yol: tamamlandı / şu an / kilitli |
| TAB | 3×3 ada minimap'i | 8 bölüm ilerleme şeridi |
| Veri kaynağı | `WorldGridManager.CurrentMap` | `ChapterProgress` + `ChapterConfigSO` |

**Hâlâ tamamen çalışır durumda:** portal ışınlaması, gözetleme kulesi, ada-bağımsız çöküş,
`WorldGridManager`, 9 haritanın kendisi ve `SetupWorld3x3` (TAM KURULUM zincirinde duruyor).
Yani bu alternatif "arşivlenmiş ölü kod" değil — **oynanabilir hâlde bekliyor**; yalnızca iki UI
bileşeni artık bölüm ilerlemesini gösteriyor.
