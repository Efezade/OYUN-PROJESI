# Karo Arşivi — kullanılmayan karo modelleri

Buradaki FBX'ler **silinmedi, sadece tarama yolundan çıkarıldı.**

## Neden

`Assets/Art/Models/Tiles/` klasörü Tile Painter'ın "🔍 Klasörü Tara → Palete Ekle"
düğmesi tarafından **özyinelemeli** taranır. O klasörde duran her FBX otomatik olarak
palete bir karo girişi açar. Aşağıdaki karolar hiçbir haritada kullanılmıyordu; palet
girişleri temizlendi ama modeller o klasörde kalsaydı bir sonraki taramada geri gelirdi.

Bu klasör tarama yolunun **dışında**, o yüzden geri eklenmezler.

## İçerik

| Model | Eski karo id | Neden arşivde |
|---|---|---|
| `kum_karo.fbx` | `kum` | Hiçbir haritada (Bölüm 1 ya da eski 3×3 dünya) geçmiyordu |
| `lav_karo.fbx` | `lav` | Aynı — hiçbir haritada geçmiyordu |

## Geri getirmek istersen

1. FBX'i `Assets/Art/Models/Tiles/` altına geri taşı (`.meta` ile birlikte — GUID korunur).
2. Tile Painter → "Klasörü Tara" → karo palete yeniden eklenir.
3. `TileFolderImporter.Overrides` tablosunda `kumkaro` / `lavkaro` tanımları hâlâ duruyor,
   yani doğru ad/renk/yürünürlük varsayılanlarıyla gelir (`lav` yürünmez olarak).

_Temizlik tarihi: 2026-08-04. Ayrıntı: `Docs/DECISION_LOG.md`._
