using System.Collections.Generic;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// GÖRÜŞ HATTI — "aradaki karo atışı kesiyor mu?" sorusunun TEK yeri.
    ///
    /// Neden ayrı sınıf: bu soruyu üç yer soruyor (TurnManager saldırıyı doğrularken,
    /// CombatHighlighter kırmızı hedefleri çizerken, düşman AI'ı hedef seçerken). Üçü ayrı ayrı
    /// hesaplasaydı kaçınılmaz olarak ayrışırlardı: HUD "vurabilirsin" der, tıklayınca vurmazdı.
    ///
    /// KURAL (2026-08-12 tasarımı): DUVAR keser, SİPER kesmez.
    ///   • Arena duvarı (<see cref="CombatRole.Wall"/>) — dağ/kayalık/uçurum sırtları
    ///   • Kam'ın koyduğu arazi karoları — Taş Duvar, Boşluk, Çığ molozu
    ///   • Tahtada karo olmayan boşluk (arena dışı)
    /// Siper (<see cref="CombatRole.Cover"/>: dev kaya, sık çalı) geçilemez ama üstünden/yanından
    /// atış yapılabilir — taktik fark tam olarak budur.
    ///
    /// Uçlar (atıcı ve hedef) HER ZAMAN serbesttir; yalnız ARADAKİ karolar bakılır. Aksi halde
    /// duvarın dibindeki bir düşman kendi karosu yüzünden hedeflenemezdi.
    /// </summary>
    public static class LineOfSight
    {
        /// <summary>Bitişik hedefte (mesafe ≤ 1) hat kontrolü yapılmaz — göğüs göğüse vuruş
        /// hiçbir zaman engellenmez.</summary>
        public const int MeleeDistance = 1;

        /// <summary>
        /// <paramref name="from"/> → <paramref name="to"/> arasında görüş açık mı?
        /// <paramref name="buffer"/> çağıranın yeniden kullandığı geçici liste (çöp üretmemek için).
        /// </summary>
        public static bool IsClear(HexGridManager grid, CombatMapGenerator arena,
                                   HexCoordinate from, HexCoordinate to, List<HexCoordinate> buffer)
        {
            if (grid == null || buffer == null) return true;      // sistem yoksa engelleme
            if (from.DistanceTo(to) <= MeleeDistance) return true;

            from.LineTo(to, buffer);
            for (int i = 1; i < buffer.Count - 1; i++)            // uçları atla
                if (Blocks(grid, arena, buffer[i])) return false;

            return true;
        }

        /// <summary>Bu karo görüşü kesiyor mu?</summary>
        public static bool Blocks(HexGridManager grid, CombatMapGenerator arena, HexCoordinate coord)
        {
            if (grid == null) return false;

            // Tahtada karo yok (arena dışı / uçurum boşluğu) → hat oradan geçmez.
            if (!grid.TryGetCell(coord, out HexCell _)) return true;

            // Kam'ın koyduğu arazi karosu (Taş Duvar / Boşluk / Çığ molozu).
            string id = grid.TileMap != null ? grid.TileMap.GetTileId(coord) : null;
            var aug = AugmentCatalog.ByVisual(id);
            if (aug != null) return aug.IsTerrain;

            // Arena üreticisinin duvar sırtları. Siper (Cover) KESMEZ.
            if (arena != null) return arena.RoleAt(coord) == CombatRole.Wall;

            // Arena bilgisi yoksa (elle kurulmuş test tahtası): dağ ailesi keser, çalı/orman kesmez.
            var cat = TileCatalog.Get(id);
            return cat != null && cat.Family == TileFamily.Mountain;
        }
    }
}
