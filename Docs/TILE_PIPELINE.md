# Karo (Tile) Ekleme Boru Hattı — Tile Painter

> Unity tarafı Claude ("Watson") hafızasındaki `reference-tile-pipeline` notunun repo yedeğidir
> (TASK-001, 2026-07-26). Hafıza kaynak olarak kalır; bu dosya git yedeğidir.
> Araçlar: `Assets/Scripts/Editor/TileFolderImporter.cs` + `TilePainterWindow.cs`.

## NEREYE: `Assets/Art/Models/Tiles/`
Unity Project panelinde bu klasöre yeni **FBX** karoları sürükle (ya da diske kopyala). Araç bu klasörü
EditorPrefs'te hatırlar (varsayılan). Klasörde kurulu karolar (id'ler): standartkaro, agackaro1/2/3,
cicekkaro, mantarkaro, kumkaro, sukaro, lavkaro, koprukaro, kulekaro → üretilen prefab'lar
`Assets/Prefabs/Grid/Tile_*.prefab`. (Ek: `magaza` karosu = mantar modeli + `isStore`, mağaza sistemiyle.)

## ADIMLAR
1. `TacticalRPG → Tile Painter - Karo Boyama`
2. "Klasörden Karo Ekle" → *Karo Klasörü* = Tiles (zaten dolu gelir; değilse klasörü sürükle)
3. **"🔍 Klasörü Tara → Palete Ekle"** — her FBX: hex boyutuna ölçeklenir (köşe-köşe 1.90 m) + pivot
   alt-orta + MeshCollider → `Tile_<id>.prefab` kaydı + palete **upsert** (NON-DESTRUCTIVE: boyamayı/mevcut
   karoları silmez)
4. Paletten yeni karoyu seç → **"▶ Boyamayı Başlat"** → Scene'de hex'e **SOL TIK** boya (sağ tık = sıfırla,
   sürükle = fırça)

## İKİ KRİTİK KURAL
- **Footprint kalkanı GEVŞETİLDİ (2026-06-27):** importer FBX'i her boyutta 1.90'a auto-scale eder. Eski
  `MaxFootprint=50` reddi, temiz ama **1000x Blender-ölçekli** (footprint ~1900) FBX'leri yanlışlıkla
  reddediyordu (kullanıcının karoları + köprü hep 1900). Artık `MaxFootprint=100000` → sadece absürt/dejenere
  değer elenir. **v1 felaketi büyük footprint'ten DEĞİL, OFF-CENTER geometriden** kaynaklıydı
  (kayma > footprint); gerçek bozukluk olursa merkez-kayma kontrolü eklenir. Bozuk geometri için Blender:
  **Join + Delete Loose + Ctrl+A + origin'i geometriye al**. Tarama sonrası Console'a bak (✓ eklendi / ATLANDI).
- **Dosya adı normalize (`NormalizeKey`, 2026-06-27):** Türkçe (ğ→g ç→c ö→o ü→u ş→s ı→i) + **_/-/boşluk
  atılır** → `agac_karo_1` / `ağaçkaro1` / `agac karo 1` hepsi → `agackaro1` (tabloyla eşleşir).
  Tablo (`Overrides`): `standartkaro`→id "default", `sukaro`/`lavkaro`→**yürünmez**,
  `agackaro1-3`/`cicekkaro`/`mantarkaro`/`kulekaro`→surfaceHeightOverride, `koprukaro`→"kopru",
  `kumkaro`→"kum". Bilinmeyen ad → genel yürünür + hash-renk; sonra `TilePalette` asset'inden elle ayarla
  ya da Overrides'a eklet.
