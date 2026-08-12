using UnityEngine;
using UnityEditor;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Organik harita karolarının kurulumunu, gerekiyorsa, Unity açılırken BİR KEZ çalıştırır.
    ///
    /// Neden gerekli: üretici artık 70+ karo id'si basıyor. Bu id'ler TilePalette'te yoksa harita
    /// gri placeholder'larla açılırdı — kullanıcının "önce şu menüye tıkla" diye bir adım
    /// hatırlaması gerekirdi ve unutulduğunda hata sessiz olurdu (sadece çirkin görünürdü).
    ///
    /// Maliyeti yok: tek bir palet araması yapar. Kurulum bir kez koştuktan sonra
    /// <see cref="TileVisualFactory.SentinelId"/> palette bulunur ve bir daha asla çalışmaz.
    /// Elle tekrar üretmek için: TacticalRPG ▸ Karo ▸ "…YENIDEN Uret".
    /// </summary>
    [InitializeOnLoad]
    public static class TileSetupBootstrap
    {
        static TileSetupBootstrap() => EditorApplication.delayCall += RunOnce;

        private static void RunOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var palette = AssetDatabase.LoadAssetAtPath<TilePaletteSO>("Assets/Data/Map/TilePalette.asset");
            if (palette == null) return;                                   // proje henüz kurulmamış
            if (palette.GetById(TileVisualFactory.SentinelId) != null) return;   // zaten kurulu

            Debug.Log("[Karo] Organik harita karolari palette'te yok — tek seferlik kurulum baslatiliyor…");
            TileVisualFactory.BuildAll(force: false);
            Debug.Log("[Karo] Kurulum tamam. Haritayi gormek icin Play'e bas ya da " +
                      "TacticalRPG ▸ Bolum ▸ 'Haritayi Simdi Uret'.");
        }
    }
}
