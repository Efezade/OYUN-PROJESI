# Taktiksel RPG (OYUN) — Geliştirme Karar Günlüğü / Arşiv

> Bu dosya, `C--3D-OYUN` hafızasındaki `project_tactical_rpg.md`'nin **tam tarihsel arşividir**
> (faz-faz uygulama detayları, kararlar, öğrenmeler).
> **Otomatik recall EDİLMEZ** — token tasarrufu için buraya taşındı; gerekince elle okunur.
> Güncel durum & "nerede kaldık" cevabı için: hafızadaki `project_tactical_rpg.md`.
> Arşivlenme tarihi: 2026-06-26.

---

---
name: project-tactical-rpg
description: For The King/XCOM tarzı hex-grid taktiksel RPG projesi; Unity 6; C:\3D OYUN\OYUN
metadata: 
  node_type: memory
  type: project
  originSessionId: 6cf13e54-92d2-4da1-9e73-3d0e693a5085
---

Hex-grid tabanlı, sıra tabanlı taktiksel RPG oyunu. For The King ve XCOM tarzı.

**Proje yolu:** `C:\3D OYUN\OYUN`  
**Motor:** Unity 6, URP  
**Dil:** C#  
**Namespace:** TacticalRPG.Grid / .Core / .Data / .UI / .Editor  
**GitHub:** https://github.com/Efezade/OYUN-PROJESI.git, branch: main  
**Son push:** `9087edb` (Faz D2 — birim yerleştirme+savaş, test ✓). **DİKKAT: working tree'de büyük COMMIT'LENMEMİŞ yığın** (DebugHUD fix + 11 karo import pipeline + savaş nameplate özelliği) — bkz "Bir Sonraki Adım".
**Faz D (`14301c9`, push'lu):** çok-tipli öz + harita toplama + tarifle birim üretme; TAM KURULUM ile setup assetleri (EssenceConfig + recipe'ler) + sahne kaydedildi.
**Önceki:** `b6125f8` Faz C4 asset verileri; `af29863` öksüz FBX/prefab temizliği (kulekaro 58MB+kare silindi, sadece köprü karo kaldı); `6040b9c` Faz C4 Kam komutan+büyü (test ✓); `bbf507e` Faz C savaş (C1+C2+C3, test ✓); `285dd0c` köprü FBX+yüzey
**Son düzeltme:** 2026-06-17 — CS0177 derleme hatası giderildi (`HexGridManager.TryGetCell` out parametresi)

### Faz 2.5 — Tile Painter (TEST EDİLDİ ✓ ÇALIŞIYOR — tint+yürünebilirlik+9 renk COMMIT BEKLİYOR)
- `TilePaletteSO` (`Assets/Scripts/Data/`) — karo türleri: id, ad, prefab, editorColor, isWalkable
- `TileMapSO` — HexCoordinate→tileId atamaları; `GetTileId/SetTileId/RemoveAssignment`
- `TilePainterWindow` (Editor) — `TacticalRPG → Tile Painter - Karo Boyama`; Scene'de sol tık boya / sağ tık sıfırla
- `HexGridManager`: `TilePalette`, `TileMap` property; `RegenerateCellVisual(coord)`, `GenerateGrid()`
- **YENİ (2026-06-21):** `HexGridManager.SpawnVisual` artık placeholder karoyu `editorColor` ile boyuyor (`MaterialPropertyBlock` + `_BaseColor`; entry.prefab==null iken) ve `cell.IsWalkable = entry.isWalkable` senkronu yapıyor. Gerçek FBX prefab atanınca tint kapanır.
- **Palet 9 renkli karoyla dolduruldu:** default, cimen, orman, kum, kaya, kar (yürünür) + dag, su, lav (yürünmez)
- Assetler: `Assets/Data/Map/` (TileMap, TilePalette), `Assets/Data/Characters/` (Kam, Ranger, Savaşçı)
- GridNavigator.cs + InputManager.cs silindi
- **Karo tasarım kılavuzu** `Docs/Karo_Tasarim_Klavuzu_GUNCEL.docx` tasarımcıya GÖNDERİLDİ — spec: pointy-top, %95 ölçek (1.90×1.645 m), kalınlık 0.30 m sabit, pivot alt-orta, FBX -Z Forward/Y Up. Karolar bekleniyor.

---

## Tamamlanan Fazlar

### Faz 1 — Hex Grid + Navigasyon (TAMAMLANDI ✓)
- 10x10 hex grid, pointy-top, axial koordinat
- HexGridManager, HexCell, HexMetrics, HexCoordinate
- FogOfWarManager (Hidden/Explored/Visible)
- PlayerController — A* ile hareket, sol tık
- ActionPointManager — 3 AP/dilim, 6 dilim/gün
- TimeSlotConfig SO, CollapseConfig SO
- MapCollapseManager — Gün 4'ten itibaren karo çöküşü
- DebugHUD — Gün/Dilim, AP bar, Öz, Kam mana, Kıyamet

### Faz 2 — Karakter Sistemi + Öz + Kam Mana (KOD HAZIR — derleme hatası giderildi, TAM KURULUM çalıştırılacak)
- `CharacterClassData` SO (`Assets/Scripts/Data/`)
- `CharacterCard` (plain C# class)
- `EssenceManager` MonoBehaviour
- `PartyManager` MonoBehaviour
- `KamManaManager` MonoBehaviour
- DebugHUD: Öz (yeşil) + Kam mana (mavi) etiketleri

---

## SceneSetupTool — Yeni Menü Yapısı (2026-06-16 sadeleştirildi)

**Eski 10 kalem → 7 kaleme indirildi. GridNavigator.cs + InputManager.cs silindi.**

```
TacticalRPG/
├── TAM KURULUM (Tek Tikla)
├── Faz 0 - Kamera ve Sahne
├── Faz 1 - Hex Grid ve Sistemler
├── Faz 2 - Karakter Sistemi
├── Faz 3 - Yetenek Test Kurulumu   ← kukla düşman + AbilityCaster + test HUD (Faz A bunu temizler)
├── Faz A - Overworld-Savas Gecisi  ← GameStateManager + görev alanı + harita geçişi
├── Debug HUD - Ekle
├── Tani - Sahne Kontrolu
└── 0 - Sahneyi Temizle
```
(TAM KURULUM artık TEK TIKLA tüm zinciri kurar: Faz 0-2 + Debug HUD + Faz A + B + C + C3 = temiz rebuild → Ctrl+S → Play. `_silentSetup` flag'i alt-faz dialoglarını bastırır, tek özet gösterir. Faz 3 ESKİ yetenek test sandbox'ı, FullSetup'a DAHİL DEĞİL. **YENİ faz (C4, D…) ekleyince FullSetup zincirine de eklemeyi unutma — kullanıcı her geliştirmede tek tıkla temiz kurulum istiyor.**)

## Bir Sonraki Adım — DÖNÜŞTE BURADAN DEVAM (güncel: 2026-06-25)

**ÖZET — nerede kaldık:** Son push `9087edb` (Faz D2, test ✓). Working tree'de **BİRİKMİŞ, HENÜZ COMMIT'LENMEMİŞ** 3 iş var:

1. **DebugHUD UI çakışması fix** (test ✓) — Canvas yalnız Overworld/ConfirmMission'da açık (Combat/Deployment'ta gizli). Değişen: `DebugHUD.cs`, `SceneSetupTool.cs` (_state wiring SetupDebugHUD+SetupPhaseA).
2. **KARO PIPELINE ÇALIŞIYOR ✓** — temiz re-export yapıldı; **11 karo başarıyla import edildi** (`Assets/Art/Models/Tiles/*.fbx` → `Assets/Prefabs/Grid/Tile_*.prefab`: default, agac1/2/3, cicek, mantar, kum, su, lav, kopru, kule). "Klasörü Tara" boru hattı sağlam. **YENİ KARO EKLEME ADIMLARI → [[reference-tile-pipeline]]** (kullanıcı "nerede kalmıştık / karo nasıl eklerim" deyince BUNU hatırlat: klasör `Assets/Art/Models/Tiles/` → Tile Painter "🔍 Klasörü Tara" → paletten seç → Scene'de boya).
3. **YENİ: Savaş nameplate'leri** (isim + bölümlü can barı + hasar flaşı/"-N") — KODLANDI 2026-06-25, **Unity'de TEST BEKLİYOR.** Detay aşağıda "Savaş Nameplate".

**DÖNÜŞTE SIRADAKİ İŞLER:**
- (a) Nameplate'i Unity'de **TEST ET** (TAM KURULUM ya da Faz C3 → Play → savaş): birim üstü isim + bölümlü can barı + sağda sayı; vuruşta model kırmızı flaş + kırmızı "-N" → sayı bir an bekleyip düşer.
- (b) **Bekleyen HER ŞEYİ COMMIT'LE** (büyük yığın): DebugHUD fix + karo pipeline (`Tiles/` FBX'ler + `Tile_*.prefab` + `TileFolderImporter.cs` + `TilePainterWindow.cs` + `TilePalette/TileMap.asset`; eski `TileBatchImportTool.cs` silindi) + nameplate (`Unit.cs`, `CharacterClassData.cs`, `UI/CombatNameplateHUD.cs`, `SceneSetupTool.cs`). Mantıklı parçalara bölünebilir.
- (c) Sonra **ANA GELİŞTİRME**: win→3-ödül draft / seviye atlama UI / mana savaş-başı reset / min-max+krit hasar / öz ekonomi dengesi.
- (Temizlik: `Assets/_Recovery/` untracked artık — silinsin/gitignore mi? kullanıcıya soruldu.)

**Faz D2 test akışı (referans):** Play(Overworld) → seed öz karolarına git (Q2R4/Q4R4/Q4R5) "Topla(1AP)" → görev Q5R5'e ≤1 hex yaklaş → üstte "Savaşa Gir" → Evet → YERLEŞTİRME'de "Üret: Savaşçı/Ranger" (öz harca) → kart seç → mavi pede tıkla → "Savaşı Başlat". Öz yerleşimi: `TacticalRPG → Essence Painter - Oz Boyama`.

### Savaş Nameplate — birim üstü isim + can barı + hasar geri bildirimi (KODLANDI 2026-06-25, TEST BEKLİYOR)
Kullanıcı isteği: savaşta her birimin (dost+düşman) üstünde **İSİM + CAN BARI**; bar **BÖLÜMLÜ** (2 can/bölüm → 10 can = 5 bölüm, 4 hasar = 2 bölüm eksilir) + sağında **güncel can sayısı**; vuruşta birim modeli **saniyeliğine KIRMIZIYA çakar** (animasyon) + barın yanında kırmızı **"-N"** belirir, sayı **bir an bekleyip sonra düşer** ("-N" kaybolur).
**Yeni dosya:** `UI/CombatNameplateHUD.cs` — tek bileşen, iki iş. (1) **İsimlendirme:** savaş başına benzersiz ad — sınıf isim havuzundan (`CharacterClassData.UnitNames`) ya da boşsa "Sınıf N" (Goblin 1/2/3); komutan numarasız "Kam". `Unit.SetInstanceName` ile yazılır → tur paneli/savaş mesajları da aynı adı kullanır. Sayaçlar Overworld'e dönünce sıfırlanır. (2) **Çizim:** birimin başını dünya→ekran yansıtıp IMGUI ile isim çipi (dost yeşil / düşman kırmızı / aktif birim sarı isim) + bölümlü can barı + sağda sayı çizer. Hasarı **POLLING** ile yakalar (event değil) → "shown" değeri `_damagePopupDuration` (0.6s) bekler, sonra düşer; o sırada kırmızı "-N". Yalnız Deployment+Combat'ta. Whitebox IMGUI (cila aşamasında dünya-uzay uGUI prefab'ına).
**Değişen:** `Core/Unit.cs` — `_runtimeName` + `SetInstanceName`/`HasInstanceName`; `DisplayName` önce runtime adı. **Hasar flaşı Unit içinde** (`MaterialPropertyBlock` `_BaseColor`, paylaşılan materyali bozmaz; Awake'te base renk cache; `_hitFlashColor`/`_hitFlashDuration`=0.5s; `TakeDamage` gerçek can kaybını hesaplayıp `PlayHitFlash`). `Data/CharacterClassData.cs` — opsiyonel `_unitNames` havuzu (`UnitNames`). `Editor/SceneSetupTool.cs` — Faz C3'e `CombatNameplateHUD` ekle+wire (state/unitManager/turnManager/Camera.main); **zaten TAM KURULUM zincirinde** (ayrı menü yok).
**Ayarlar (Inspector → GameManager):** `Hp Per Segment`=2, `Damage Popup Duration`=0.6, bar/isim renkleri; flaş süresi `Unit`'te. **İsim havuzu boş → numaralı gelir**; flavor isim istenirse sınıf asset'lerinde (Goblin/Savaşçı/Ranger/Kam) Inspector "Unit Names" doldurulur (tema: özler eski savaşçıların anıları → isimli olması mantıklı).

### Karo İçe Aktarma — Tile Painter "Klasörü Tara" + KRİTİK ÖĞRENME (2026-06-25)

**ÖĞRENME (TEKRARLAMA!):** Tasarımcının v1 karo FBX'leri (`Görseller/oyun karoları 3d model v1/`: standartkaro, ağaçkaro1-3, çiçekkaro, mantarkaro, kumkaro, sukaro, lavkaro, köprükaro, kulekaro) **doğrudan grid karosu olarak KULLANILAMAZ.** Sorunlar (üretilen prefab YAML'ından ölçüldü): her biri **~262 birim** (1.9m olmalı), geometri **merkez-dışı ~450 birim**, **13+ ayrı mesh parçası** (tek hex değil), **61 MB** (gömülü dev texture, 11×≈641MB). Toplu import bounding-box'ı 1.9m'ye sığdırınca **asıl karo nokta kadar küçüldü** → harita kaos (kullanıcı "sıçışş.png" ss). **Geri alındı:** `git checkout -- TilePalette.asset Tile_KopruKaro.prefab`; kullanıcının boyaması (TileMap 126 atama) KORUNDU.

**ÇÖZÜM = Blender'da temiz re-export** (kullanıcı bu yolu seçti). Her karo için checklist: **Join** (tek mesh) + **Mesh>Clean Up>Delete Loose** (başıboş/uzak geometri sil → kutu=karo olsun) + **Ctrl+A All Transforms** (262/merkez-dışı kökten düzelir) + FBX export **-Z Forward / Y Up** + texture 1K'ya düşür/embed etme. **ÖNCE TEK karo (standartkaro) doğrula**, sonra kalan 10.

**YENİ ARAÇ — Tile Painter "Klasörden Karo Ekle":** `Assets/Scripts/Editor/TileFolderImporter.cs` (paylaşılan static) + `TilePainterWindow.cs`'e folder alanı (`DefaultAsset`, EditorPrefs'te hatırlanır, default `Assets/Art/Models/Tiles`) + **"🔍 Klasörü Tara → Palete Ekle"** düğmesi. Klasördeki FBX'i işler (footprint→1.9m ölçek + alt-orta pivot + MeshCollider → `Assets/Prefabs/Grid/Tile_<id>.prefab`), hazır `.prefab`'ı doğrudan referanslar, palet girişini **id'ye göre upsert** eder (NON-DESTRUCTIVE — klasörde olmayan girişe dokunmaz). Bilinen karolar için güzel varsayılan tablosu (`Overrides`: standartkaro→default, su/lav yürünmez, ağaç/çiçek/mantar/kule surfaceHeightOverride=TileHeight), bilinmeyen için dosya adından genel giriş + hash-renk. **GÜVENLİK KALKANI:** footprint **> 50 birim → palete EKLEMEZ**, "ATLANDI — Blender'da düzelt" raporu verir (262-birimlik bozuk modeller böyle reddedilir, harita bir daha bozulmaz); >5 birim veya >20 mesh → eklenir ama uyarır. **Eski `TileBatchImportTool.cs` SİLİNDİ** (işlevi buraya taşındı, tek temiz yol). Kopru palet entry'si hâlâ eski `Tile_KopruKaro.prefab`'a bağlı (geri alma sonrası).

### Faz D2 — Faz D düzeltmeleri (KODLANDI 2026-06-25, test BEKLİYOR)
Kullanıcı istekleri: (1) ana haritada birim üretme OLMASIN → üretim **savaş öncesi yerleştirme ekranına** taşındı; (2) özler **rastgele DEĞİL** → elle boyama aracı; (3) özler çok yukarıdaydı → **yere yakın**; (4) "Savaşa Gir" yazısı uzaktan çıkıyordu → sadece göreve **≤1 hex** yakınken; (5) öz görseli sonradan kolay değişsin → **tip başına prefab**.
**Yeni dosyalar:** `Data/EssenceMapSO.cs` (coord→(tür,miktar) el yapımı harita; SetAmount/AddAmount/ClearCoord/BuildLookup), `Editor/EssencePainterWindow.cs` (menü `TacticalRPG → Essence Painter - Oz Boyama`; tür seç+fırça miktarı gir, sol tık=EKLE/stack, sağ tık=karoyu temizle; scene'de renkli disk önizleme — TilePainter deseni).
**Değişen:** `EssenceConfigSO` (rastgele-spawn alanları SİLİNDİ [TileChance/min/max/spawnWeight/RandomWeightedType]; TypeStyle'a `prefab` eklendi + `PrefabOf`; artık sadece ad+renk+prefab), `EssenceNodeManager` (rastgele→`_map.BuildLookup`; `_nodeHeight` 0.9→**0.12**; her TÜR için tek küre `_ringRadius`=0.34 halkada [üst üste binmez]; prefab varsa Instantiate, yoksa placeholder küre; `Config`/`Map` prop'ları painter için), `OverworldEssenceHUD` (ÜRETİM ÇIKARILDI — sadece cüzdan+topla+salt-okunur roster), `DeploymentHUD` (ÜRETİM EKLENDİ — ÖZ DEPOSU+`_recipes`+`_party.TryCreate`; `_wallet`/`_config`), `MissionManager` (`_enterRange`=1 + `EnterRange` + `GetEnterableMission(from)`), `MapInputHandler` (görev tıklaması yakınlık kapısı: uzaksa marker'a yürü), `OverworldCombatHUD` (Overworld'de yakınlık istemi `DrawNearbyMissionPrompt` + `_missionManager`/`_player`), `SceneSetupTool` (Faz D: EssenceMap.asset oluştur/**KORU**+wire `_map`+height/scale/ring; EssenceConfig artık **varsa KORUNUR** [prefab atamaları silinmesin]; üretim wiring OverworldEssenceHUD→**DeploymentHUD**'a taşındı; Faz A'da OverworldCombatHUD'a mission+player wire).
**Asset notları:** `Assets/Data/Map/EssenceMap.asset` ilk oluşumda 3 örnek karo seed'lenir (Q2R4=3Toprak, Q4R4=2Su, Q4R5=2Ateş+1Toprak), sonra **TAM KURULUM korur** (boyamalar silinmez). EssenceConfig.asset da korunur. Kullanıcı kendi animasyonlu öz prefab'ını EssenceConfig'de ilgili türe atayınca otomatik kullanılır.
**KARAR (netleştirilecek):** üretilen birimler roster'da KALICI (öz bir kez harcanır, sonraki savaşlarda hazır) — savaş-bazlı değil. İtiraz gelirse battle-scoped'a çevrilir.
**Sıradaki (D2 testi sonrası):** win→3-ödül draft / seviye atlama UI / mana savaş-başı reset / min-max+krit hasar / öz ekonomi dengesi.

### Faz D — Çok-tipli öz + harita toplama + tarifle birim üretme (BİTTİ ✓ commit `14301c9`, push'lu)
Kullanıcı isteği: maptaki karolarda öz olsun, topla, öze göre savaş için birim (Savaşçı/Ranger) üret; en az 3 öz türü; üretim 2-öz kombinasyon tarifi. **Kullanıcı kararları:** (1) parti SADECE Kam'la başlar, diğerleri özle üretilir; (2) toplama = "Topla" butonu **1 AP**; (3) 3 tür **Ateş/Su/Toprak** (Kırmızı/Mavi/Yeşil). **Varsayılanlar (itiraz gelmedi):** deployment artık BEDAVA (öz üretimde harcanır); tarifler Savaşçı=2Ateş+1Toprak, Ranger=2Su+1Toprak (Inspector'dan ayarlanır); seviye atlama geçici tek-tip (Toprak); EssenceConfig.TileChance=0.55 (ayarlanır).
**Yeni dosyalar:** `Data/EssenceType.cs` (enum Ates/Su/Toprak + `EssenceAmount` struct), `Data/EssenceConfigSO.cs` (tip ad/renk + spawn şansı/min-max/ağırlık — `NameOf/ColorOf/RandomWeightedType`), `Data/UnitRecipe.cs` (sınıf+`EssenceAmount[]` maliyet + `CostString`), `Core/EssenceWallet.cs` (3 sayaçlı typed cüzdan — **EssenceManager'ın yerine, o SİLİNDİ**), `Core/EssenceNodeManager.cs` (overworld karolara renkli öz küreleri, **sis-duyarlı** görünürlük [PlayerController.OnMoved], `CollectAt`=1AP→wallet+node sil; MissionManager deseni), `UI/OverworldEssenceHUD.cs` (sağ-üst: ÖZ DEPOSU 3 sayaç + "Topla (1AP)" + "Üret" tarif butonları + roster).
**Değişen:** `PartyManager` (wallet + `TryCreate(recipe)` + `OnRosterChanged`; start=Kam), `DeploymentManager` (deploy bedava, `_essence` çıktı), `DeploymentHUD` (öz/maliyet çıktı), `DebugHUD` (tek öz→3 sayaç), `SceneSetupTool` (Faz 2 EssenceWallet 4/4/4 + start=Kam; Faz B essence wiring çıktı + DM._party; **Faz D menüsü** order 21 [EssenceConfig+2 recipe asset + EssenceNodeManager + OverworldEssenceHUD wire]; FullSetup zincirine eklendi).
**Test akışı:** TAM KURULUM → Play (Overworld) → sağ panelde ÖZ DEPOSU + roster(Kam) → renkli öz karosuna git → "Topla (1 AP)" → yeterince toplayınca "Üret: Savaşçı/Ranger" → marker(Q5R5)→Evet→üretilenleri BEDAVA yerleştir→savaş. **(BİTTİ ✓ — kullanıcı onayladı, commit+push'landı `14301c9`; 6 yeni .cs + .meta + EssenceConfig/recipe assetleri + sahne dahil.)**

### (önceki) Faz C4 — Kam komutan + büyü (TEST EDİLDİ ✓ `6040b9c`)

**Faz C4 ne getirdi (KOD — dosya referansı):**
- `CharacterClassData._isCommander`(bool)+`_unitColor`(Color)+props; `CharacterCard.IsCommander`/`RestoreFull()`; `Unit.IsCommander`.
- `DeploymentManager`: `_party` ref + `SpawnCommander()` (Kam'ı deploy zone'a ÜCRETSİZ/otomatik indirir, alt-orta hücre) + `TryPickCommanderCell` + her deploy edilen birimi `card.Data.UnitColor` ile boyar (komutan kartı listede gizli + elle deploy engelli) + `card.RestoreFull()` (savaş başı tam HP).
- `UnitManager.GetCommander()`/`HasAliveCommander()`.
- `TurnManager`: `_commanderPresent` (başta tespit) + `CheckEnd` yenilgi = komutan ölümü (yoksa eski "tüm oyuncu öldü") + `RegisterCommanderAction()` (cast eylem defteri).
- `AbilityCaster` YENİDEN YAZILDI: combat kasteri. Origin = komutan **Unit** konumu (PlayerController DEĞİL); yetenekler komutan kartından; arm/cast yalnızca Kam'ın oyuncu turunda + eylemi harcanmamışken; mana `KamMana`'dan; hedef doğrulama (Damage→Enemy, Heal/Buff→Player). Public üyeler korundu (AbilityTestHUD derlenir). `_player`/`_partyManager`/`_casterClassName` alanları KALDIRILDI.
- `MapInputHandler` Combat dalı: büyü hazırsa `caster.TryCastAt`, değilse `turnManager.HandlePlayerClick`.
- `CombatHUD`: Kam turunda mana + 1/2/3 arm butonları paneli; yenilgi banner "Komutan (Kam) düştü". `DeploymentHUD`: komutan satırı + listeden gizleme.
- `SceneSetupTool`: **Faz C4 menüsü** (order 20) + **TAM KURULUM zincirine eklendi**; Kam isCommander+altın, Savaşçı mavi, Ranger turkuaz renkleri; Kam'a 3 büyü atanır (AteşTopu Damage/m3/r4/p6, Şifa Heal/m2/r3/p5, RuhKalkanı Buff/m4/r2/p3 — `GetOrCreateAbilitySO` ile create-or-load); AbilityCaster ekle+wire (turnManager/kamMana/unitManager); MapInput._caster, Deployment._party, CombatHUD._caster/_kamMana wire. Faz 3 (eski test) caster wiring de yeni alanlara uyarlandı.

**Faz C4 KARARLARI:** Kam zorunlu+ücretsiz komutan, deploy zone alt-ortaya otomatik iner; büyü = Kam'ın TUR EYLEMİ (basit saldırı yerine ya da onun yanında, hareketten bağımsız); her sınıf kendi `_unitColor`'ı (Inspector'dan ayarlanabilir, Whiteboxing); enemyler kırmızı kalır; lose=Kam ölümü (run-reset/meta SONRAKİ faz). **Bilinçli ertelenenler:** mana savaş-başı resetlenmiyor (havuz overworld'de regen, kalıcı) — istenirse hızlı eklenir; deploy birimleri savaş arası kalıcı hasar tutmuyor (RestoreFull her savaş). **Sıradaki olası iş:** Faz C4 testi → sonra öz-ekonomi/çok-tipli öz (aşağıdaki "Anamap öz toplama" + ÖZ DEPOSU 3 sayaç) / win→3-ödül draft / mana savaş-başı reset / min-max+krit hasar.

**Faz C ne sağladı (C1+C2+C3, test edildi ✓ `bbf507e`):**
- **C1 (veri temeli) ✅ kod:** `CharacterClassData._speed`(varsayılan 5)+`_attackRange`(1) + props; `CharacterCard`'a `MoveRange/Speed/AttackRange` props + ctor `level` param (`new CharacterCard(data, level)`); `Unit`'e kartsız fallback statları (`_attack/_defense/_speed/_moveRange/_attackRange`) + Attack/Defense/MoveRange/Speed/AttackRange props (kart varsa karttan); `MissionData`'ya `EnemySpawn` struct {enemyClass, coord, level} + `_enemyRoster` + `EnemyRoster` prop (+`using Grid`).
- **C2 (düşman spawn) ✅ kod:** `EnemySpawner` (`Assets/Scripts/Core/EnemySpawner.cs`) — OnStateChanged dinler; Deployment'ta `ActiveMission.EnemyRoster`'dan KARTLI düşman Unit spawn (kırmızı kapsül, collider'sız, IsInBounds guard), overworld'de temizler, Combat'ta korur (DeploymentManager aynası). SceneSetupTool **Faz C - Dusman Spawn** menüsü (order 18): Goblin sınıfı (`Assets/Data/Characters/Goblin.asset`; HP8/atk3/def0/move3/**speed4**/range1) + Mission1 `_enemyRoster`'a 3 Goblin (Q2R7,Q4R7,Q3R8 lvl1) + EnemySpawner wire. `GetOrCreateCharacterSO`'ya `speed`/`attackRange` opsiyonel param eklendi.
- **C2 TEST ✓:** düşmanlar (3 Goblin) savaşta belirdi — kullanıcı doğruladı.
- **C3 (tur sistemi) ✅ BİTTİ+TEST:** `TurnManager` (Core) — Combat'a girince `UnitManager.Units`'tan hıza göre azalan (eşitlikte oyuncu önce) initiative kuyruğu kurar; oyuncu turu `HandlePlayerClick` (boş hex=hareket / düşman=saldırı) + `EndPlayerTurn`; düşman turu coroutine AI (en yakın oyuncuya BFS ile yaklaş + menzilde vur); `ComputeReachable` BFS (birim-engelli, MoveRange) hem highlight hem AI hem yol kurar; win=`CountAlive(Enemy)==0`, lose=`CountAlive(Player)==0`; permadeath=`OnDied`→`Destroy`. `Unit`'e `MoveAlongPath(List<HexCell>, onComplete)` coroutine + `_moveSpeed`(8) + `IsMoving` + `SurfacePosition` refactor. `UnitManager.CountAlive(team)`. `CombatHUD` (UI/IMGUI, sol-üst: sıra/HP/hareket-saldırı durumu/"Turu Bitir" + merkez ZAFER/YENİLGİ banner+dön). `CombatHighlighter` (Core: aktif birim sarı top Update'te takip; yeşil ulaşılabilir + kırmızı saldırılabilir pedler OnTurnChanged'de). `MapInputHandler`'a Combat dalı + `_turnManager`. SceneSetupTool **Faz C3 - Tur Sistemi** menüsü (order 19): TurnManager+CombatHUD+CombatHighlighter ekle/wire + input bağla.
- **C3 TEST ✓:** tam savaş döngüsü çalışıyor — kullanıcı doğruladı. (Oyuncu sınıfları hız 5 > Goblin 4 → oyuncu önce oynar.)
- **SONRA → C4:** Kam zorunlu/ücretsiz birim (commander) + `AbilityCaster` origin=Kam Unit (PlayerController değil) + savaş-başı mana + **lose=Kam ölümü** (tüm Run'ı kaybetme, roguelite reset sonraki faz).
- **Kararlar (C için):** initiative eşitliğinde oyuncu önce; düşman=kartlı (simetrik hasar); spawn Deployment'ta (combat'a taşınır). Min-max hasar+krit ileriye bırakıldı (şimdilik düz `Attack`).

**Faz B test akışı (referans):** TAM KURULUM → Faz A → Faz B menüleri (sırayla) → Ctrl+S → Play → sarı marker (Q5R5) → Evet → savaş haritası + YERLEŞTİRME (alt 2 satır mavi pedler) → sol panelden kart seç → mavi pede tıkla (öz -deployCost, birim spawn) → "Savaşı Başlat" → Combat → "Geri Dön" (birimler temizlenir).

**Faz B adımları (HEPSİ ✅ 2026-06-22):**
1. ✅ `CharacterClassData._deployCost` + `DeployCost` prop (default 3).
2. ✅ `Unit` ↔ `CharacterCard`: `Bind(card)`+`PlaceAt(coord)`+`Configure(grid,um,team)`. Kart varsa HP/stat karttan, yoksa `_maxHP`. Kart `OnHPChanged`→Unit `OnStatsChanged`/`OnDied`. Hasar Defense asimetrisi Faz C'ye bırakıldı.
3. ✅ `GameState.Deployment` + `StartBattle()`; `DeploymentManager` (Core: bölge=alt N satır, mavi ped vurgu, `TryDeployAt`→öz harca→spawn→Bind→PlaceAt, OnStateChanged dinler, Combat'ta birim kalır/overworld'de temizlenir) + `DeploymentHUD` (UI: kart listesi+öz+Savaşı Başlat) + `MapInputHandler` deployment tık yönlendirme.
4. ✅ SceneSetupTool **Faz B** menüsü (UnitManager geri ekler, DM+DH kurar+wire, başlangıç özü 20).

**SONRA → Faz C (sıradaki iş — TASARIM BELGESİNE GÖRE):** Tasarım kutsal kitabı artık okundu+kaydedildi → bkz [[reference-game-design]] (hikaye+mekanik) ve [[reference-ui-layout]] (6 ekran UI). Faz C planı:
1. `MissionData`'ya **düşman roster** ekle → savaş haritasına düşman Unit spawn (kartlı/kartsız).
2. `TurnManager` — **HIZA GÖRE initiative** (XCOM/Banner Saga; "önce oyuncu sonra düşman" DEĞİL). Oyuncu turu: Unit hareketi (PlayerController-benzeri) + yetenek via `AbilityCaster`. Düşman turu: basit AI.
3. **Kam savaşta ZORUNLU bir birim** + savaş başına kısıtlı Mana havuzu ile büyü. Win: tüm düşman ölünce. Lose: **Kam ölünce TÜM Run kaybedilir** (roguelite reset, sonraki faz).
4. **Permadeath:** ölen deploy birimi o savaş için silinir, harcanan öz boşa gider.
- `AbilityCaster` zaten `Unit.TakeDamage/Heal/AddShield` çağırıyor (Faz B'de korundu).
- **İleride (Faz C kapsamı DIŞI):** çok-tipli öz (ÖZ DEPOSU'nda 3 sayaç; mevcut `EssenceManager` tek havuz); win→3-ödül draft; sınıf "Aktifleştirme kuralları"; meta-ilerleme.

**NOT (temizlik):** `Assets/_Recovery/` Unity çökme-kurtarma artığı — commit'lenmedi, untracked duruyor. Unity'de silinebilir ya da .gitignore'a eklenebilir (kullanıcıya soruldu).

**Değişmez kararlar:** tek sahne durum-tabanlı (KARAR 1); öz harcayarak deployment (KARAR 2). Detay: "Hedef Oyun Döngüsü" bölümü.

### Planlanan: Anamap karolarında ÖZ TOPLAMA (kullanıcı notu 2026-06-22 — SAVAŞTAN SONRA, şimdi YAPILMIYOR)
Kullanıcı isteği (Faz C bitince ele alınacak, muhtemelen öz-ekonomi/Faz D adımı): Overworld'de **köprü gibi özel yapı OLMAYAN her ana harita karosunda** ana karakterden küçük **yuvarlak öz** görselleri durur. Karodayken "topla" komutu → o karonun özleri **1 AP karşılığında** hazineye girer. Özler **çok-tipli + rastgele**: her karoya rastgele sayıda/karışık renk (örn. bir karoda 3 mavi+2 kırmızı+1 turuncu) — **oranları kullanıcı ayarlayacak**. Tasarımla uyumlu (bkz [[reference-game-design]]: Töz haritadan toplanır, elementsel/renkli; mevcut `EssenceManager` tek havuz → çok-tipliye genişleyecek).

---

## Planlanan Sonraki Fazlar

- **Faz 2.2:** ✅ TAMAMLANDI (2026-06-21, kod yazıldı — Unity'de derleme doğrulanacak). `KamAbilityData` SO (`Assets/Scripts/Data/`, script guid `48c8c234de2b4433a83f3ad5f6aab12c`) + `AbilityEffectType` enum (Damage/Heal/Buff). Alanlar: id, displayName, description, icon, manaCost, range, effect, power — hepsi `[SerializeField] private`+property (CharacterClassData stili). 3 örnek asset `Assets/Data/Abilities/`: AtesTopu (Damage/m3/r4/p6), Sifa (Heal/m2/r3/p5), RuhKalkani (Buff/m4/r2/p3). **Büyü UYGULAMA/cast mantığı YOK** — Faz 4 combat'a bırakıldı; KamManaManager.TrySpendMana(cost) ve HexCoordinate.DistanceTo zaten hazır, bağlanacak.
- **Faz 2.3:** Temel savaş UI (karakter kartları görsel paneli)
- **Faz 3 (KISMİ — yetenek dikey dilimi yazıldı 2026-06-21, Unity'de test edilecek):** Birim katmanı eklendi → `Unit` (Core: hex+team+HP+shield, TakeDamage/Heal/AddShield, OnStatsChanged/OnDied), `UnitManager` (kayıt defteri, GetUnitAt/GetFirstEnemy), `AbilityCaster` (1/2/3 ile arm, TryCastAt: menzil+mana+etki uygula), `AbilityTestHUD` (IMGUI gösterim). `CharacterClassData`'ya `_abilities` listesi + `Abilities` property. `MapInputHandler`'a `_caster` eklendi (yetenek hazırsa tık=hedefleme, değilse hareket — geri uyumlu). SceneSetupTool menü: **Faz 3 - Yetenek Test Kurulumu** (kukla düşman Q5R4 kırmızı kapsül 12HP + UnitManager/AbilityCaster/AbilityTestHUD GameManager'a + Kam'a 3 yetenek atar). TAM KURULUM'a EKLENMEDİ (ayrı menü, risk düşük). **Hâlâ yok:** TurnManager, sıra/initiative, düşman AI, hedef seçim görselleştirme.
- **Faz 4:** Combat sistemi (CombatResolver, DamageCalculator)
- **Faz 5:** Game loop (win/lose, Kıyamet bitiş ekranı)

---

## Hedef Oyun Döngüsü (overworld → savaş) — 2026-06-21 kararlaştırıldı

For The King benzeri iki-katmanlı yapı:
1. **Ana harita (overworld):** mevcut hex harita = keşif. Bazı hex'ler "görev alanı".
2. Görev hex'ine tıkla → onay → **savaş** durumuna geç.
3. **Savaş:** grid savaş TileMap'iyle yeniden üretilir, o görevin düşmanları spawn olur.
4. **Deployment:** öz UI ile, öz HARCAYARAK parti kartlarını deployment hex'lerine yerleştir.
5. **"Savaşı Başlat"** → tur tabanlı çarpışma → win/lose → overworld'e dön.

**KARAR 1 (harita):** TEK SAHNE, durum-tabanlı geçiş. Combat'ta HexGridManager savaş TileMap'iyle yeniden üretilir; parti/öz hafızada kalır. Ayrı Unity scene KULLANILMIYOR.
**KARAR 2 (deployment):** Öz harcayarak kart yerleştirme. Öz çift amaçlı (savaşa sürme + kart geliştirme). Karta deploy maliyeti eklenecek (`CharacterClassData._deployCost`).
**Düşman ana haritada DEĞİL** — sadece görev onaylanınca savaş haritasında spawn. (Faz 3 test kuklası geçici sandbox; combat map gelince taşınacak.)

**Planlanan fazlar:** ~~Faz A~~ ✅ → Faz B (deployment + öz ile yerleştirme + Unit↔CharacterCard birleştirme) → Faz C (düşman roster spawn + TurnManager + savaş + win/lose).

**Faz A TAMAMLANDI (2026-06-21, commit `239f662` push'landı — Unity'de test edilecek):**
- `GameStateManager` (Core): `GameState{Overworld,ConfirmMission,Combat}` + `OnStateChanged`; `RequestMission/ConfirmMission/CancelMission/ReturnToOverworld`. EnterCombat: overworld map+player coord sakla → `grid.SetTileMap(combatMap)` → `fog.RevealAll()` → player GO gizle. Return: overworld map geri yükle → `fog.ResetFog()` → player göster+`Initialize`.
- `MissionData` SO (Data): displayName, description, combatMap (TileMapSO). Düşman roster Faz C'de.
- `MissionManager` (Core): coord→MissionData listesi, `GetMissionAt`, overworld hex'lerine sarı küre marker (runtime spawn), state'e göre göster/gizle.
- `HexGridManager.SetTileMap(TileMapSO)`; `FogOfWarManager.RevealAll()/ResetFog()` eklendi.
- `MapInputHandler`: sadece Overworld state'te tık; görev coord'una tık → `RequestMission` (geri uyumlu, null ise eski davranış).
- `OverworldCombatHUD` (UI, IMGUI): ConfirmMission'da Evet/Hayır, Combat'ta "Geri Dön".
- SceneSetupTool menü: **Faz A - Overworld-Savas Gecisi** (CombatTileMap[default kaya] + Mission1[Goblin Pususu @Q5R5] asset oluşturur, manager'ları GameManager'a ekler+wire eder). Menü çalıştırılınca CombatTileMap.asset + Mission1.asset oluşur (henüz commit'lenmedi — kullanıcı menüyü çalıştırıp sahneyi kaydedince gelecek).
- **İyileştirme (commit `5fb7991`):** Savaşa girmek `ActionPointManager.SpendAP(1)` ile **1 AP harcar**. Faz A kurulumu artık **Faz 3 test iskelesini kaldırıyor** (Enemy_Dummy + AbilityCaster + AbilityTestHUD + UnitManager silinir, MapInputHandler._caster çözülür) — ana harita temiz. Yani Faz 3 sandbox'ı ile Faz A overworld'ü artık birlikte durmuyor; ability testi gerekince combat fazında (B/C) düzgün kurulacak.
- NOT: kod committed ama **scene'e etki için kullanıcının Unity'de Faz A menüsünü YENİDEN çalıştırması gerek** (cleanup + AP wiring menü çalışınca olur).
- **Savaşta hareket henüz YOK** — beklenen; savaş haritasında birim deployment (Faz B) + sıra-tabanlı hareket (Faz C) gelince olacak.

## Kritik Teknik Notlar

- URP shader property: `_BaseColor` (not `_Color`); tile materyaller artık URP/Lit (ışıktan etkilenir)
- Hex tile mesh: **3D prism** (TileHeight=0.3f), 31 vertex, üst+6 yan yüz; winding doğrulandı
- DefaultExecutionOrder: HexGridManager(-100), FogOfWarManager(-50), PlayerController(0)
- Camera: **izometrik** — pos=(-8, 15, -9), Euler(30,45,0), ortho size=10
- Player: **Capsule** (turuncu, URP/Lit), scale=(0.45,0.45,0.45), heightOffset=0.8
- Player start: HexCoordinate(Q=3, R=4); heightOffset=0.8 = TileHeight(0.3)+capsule_half(0.45)+gap(0.05)
- SceneSetupTool: "TAM KURULUM" tek tıkla Faz0+Faz1+Faz2+HUD hepsini kurar
- Kullanıcı kendi assetlerini ekleyecek; placeholder'lar: gri prism tile + turuncu kapsül

### Karo FBX Pipeline + Yüzey Yüksekliği Sistemi (2026-06-22, commit 285dd0c)
- **Tasarımcı FBX karosu onboarding:** `TileFbxSetupTool.cs` (Editor) → menü `TacticalRPG → Karo → Kopru Karo...`. FBX'i instantiate edip `Renderer.bounds` ile ÖLÇER, ölçek (köşe-köşe=1.90), eksen (en ince→Y/üst), pivot (alt-orta, y=0) düzeltir, MeshCollider ekler, prefab kaydeder (`Assets/Prefabs/Grid/Tile_KopruKaro.prefab`), palette **sadece "kopru" tipine** atar (diğerleri placeholder kalır). Açık sahnedeki grid'i otomatik yeniler. **Genelleştirilebilir** — lav/dağ FBX'i gelince aynı tool.
- İlk köprü FBX'i 10× büyük + Z-up + pivot tepedeydi; tool düzeltti. Bir de baş-aşağı geldi → prefab YAML'ında child rotasyonu elle `(0.5,-0.5,0.5,0.5)` + pos.y≈0'a çevrilerek düzeltildi (180° dikey flip).
- **Yüzey yüksekliği (engebe/köprü):** `HexCell.SurfaceHeight`; `HexGridManager.ResolveSurfaceHeight` hücre MERKEZİNDEN aşağı ışınla yürüme yüzeyini (güverte, zirve değil) ölçer. `TilePaletteSO.TileEntry.surfaceHeightOverride` (>0 ise elle).
- **Birim konumu artık sabit offset DEĞİL:** `Unit` ve `PlayerController` yüzeye göre oturur. `PlayerController.MoveCoroutine` hareket sırasında HER KARE altına `Physics.RaycastAll` atıp Y'yi yüzeyden alır → **kontur takibi** (kemerde çıkar, köprüler arası çukurda iner). `_heightOffset` artık "karakterin yüzeye göre ofseti" (ayak payı); gerçek karakter gelince bir kez ayarlanır.

**Why:** Oyunun vizyonu: Evrimleşen karakter kartları (3 seviye), Kıyamet Sayacı baskısı, Kam'ın mana/büyü sistemi.  
**How to apply:** Faz numaralarına göre menü çalıştır. Kod arka planda Claude yazar, UI/görsel Efe yapar.
