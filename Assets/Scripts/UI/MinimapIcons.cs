using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.UI
{
    /// <summary>Minihatitada gösterilen işaret türleri.</summary>
    public enum MinimapIconKind
    {
        Market     = 0, // ticaret hanı / boyalı mağaza karosu
        Encounter  = 1, // savaş alanı (baskın kampı / boyalı savaş karosu)
        Dungeon    = 2, // zindan girişi
        Mandatory  = 3, // zorunlu görev (sisten bağımsız görünür)
        Watchtower = 4, // gözetleme kulesi
        Essence    = 5, // öz yatağı
        Player     = 6, // oyuncunun konumu
        Selection  = 7, // seçilen hedef karo (altıgen halka)
        PathDot    = 8, // hedefe giden rotanın ara karoları (dolu altıgen)
        Waypoint   = 9  // YOL BELİRLE ile konan işaret (3B haritada çizgiyle gösterilir)
    }

    /// <summary>
    /// MİNİHARİTA İŞARETLERİ — piksel desenlerinden ÇALIŞMA ZAMANINDA üretilen sprite'lar.
    ///
    /// Neden çizim asset'i değil: projede ikon sanatı yok ve haritanın kendisi zaten iri pikselli.
    /// 11×11 piksel desenler haritanın diliyle konuşuyor, tek bir dosya bağımlılığı getirmiyor ve
    /// desen KODDA okunabiliyor — bir ikonu değiştirmek için resim programı açmak gerekmiyor.
    ///
    /// RENKLENDİRME: dolgu (X) BEYAZ üretilir, <c>Image.color</c> ile boyanır. Kontur (o) koyu
    /// kalır — parlak bir renkle çarpılsa bile koyu kalacağı için işaret her arazi renginin
    /// üstünde okunur. Ara ton (#) gölge/detay içindir.
    /// </summary>
    public static class MinimapIcons
    {
        private const int Size = 11;

        private static readonly Color Outline = new(0.05f, 0.045f, 0.05f, 1f);
        private static readonly Color Accent  = new(0.42f, 0.40f, 0.40f, 1f);
        private static readonly Color Fill    = Color.white;

        private static readonly Dictionary<MinimapIconKind, Sprite> Cache = new();

        // ── Desenler ─────────────────────────────────────────────────────────
        // '.' saydam · 'o' kontur (koyu) · 'X' dolgu (renklenir) · '#' ara ton

        private static readonly string[] MarketPattern =
        {
            ".....o.....",
            "....ooo....",
            "...ooXoo...",
            "..ooXXXoo..",
            ".ooXXXXXoo.",
            "ooooooooooo",
            ".oXXXXXXXo.",
            ".oX##X##Xo.",
            ".oX##X##Xo.",
            ".oXXXXXXXo.",
            "..ooooooo..",
        };

        private static readonly string[] EncounterPattern =   // çapraz kılıçlar
        {
            "oo.......oo",
            "oXo.....oXo",
            ".oXo...oXo.",
            "..oXo.oXo..",
            "...oXoXo...",
            "....oXo....",
            "...oXoXo...",
            "..oXo.oXo..",
            ".oXo...oXo.",
            "oXo.....oXo",
            "oo.......oo",
        };

        private static readonly string[] DungeonPattern =     // mağara kemeri
        {
            "...........",
            "...ooooo...",
            "..oXXXXXo..",
            ".oXXXXXXXo.",
            "oXXo###oXXo",
            "oXo#####oXo",
            "oXo#####oXo",
            "oXo#####oXo",
            "oXo#####oXo",
            "ooo#####ooo",
            "...........",
        };

        private static readonly string[] MandatoryPattern =   // kalkan + ünlem
        {
            ".ooooooooo.",
            ".oXXXXXXXo.",
            ".oXXo#oXXo.",
            ".oXXo#oXXo.",
            ".oXXo#oXXo.",
            ".oXXo#oXXo.",
            ".oXXXXXXXo.",
            "..oXo#oXo..",
            "..oXXXXXo..",
            "...oXXXo...",
            "....ooo....",
        };

        private static readonly string[] WatchtowerPattern =  // gözetleme kulesi
        {
            "..o.....o..",
            "..ooooooo..",
            "..oXXXXXo..",
            "..oX###Xo..",
            "..oXXXXXo..",
            "...oXXXo...",
            "...oXXXo...",
            "...oXXXo...",
            "..oXXXXXo..",
            ".oXXXXXXXo.",
            ".ooooooooo.",
        };

        private static readonly string[] EssencePattern =     // öz taşı
        {
            "...........",
            "...ooooo...",
            "..oXXXXXo..",
            ".oXX###XXo.",
            "oXXXXXXXXXo",
            ".oXXXXXXXo.",
            "..oXXXXXo..",
            "...oXXXo...",
            "....oXo....",
            ".....o.....",
            "...........",
        };

        private static readonly string[] PlayerPattern =      // oyuncu — dolu daire + koyu halka
        {
            "....ooo....",
            "..ooXXXoo..",
            ".oXXXXXXXo.",
            ".oXXXXXXXo.",
            "oXXXXXXXXXo",
            "oXXXXXXXXXo",
            "oXXXXXXXXXo",
            ".oXXXXXXXo.",
            ".oXXXXXXXo.",
            "..ooXXXoo..",
            "....ooo....",
        };

        private static readonly string[] WaypointPattern =    // durak — direğe asılı üçgen flama
        {
            ".ooo.......",
            ".oXXooo....",
            ".oXXXXXoo..",
            ".oXXXXXXXo.",
            ".oXXXXXoo..",
            ".oXXooo....",
            ".oXo.......",
            ".oXo.......",
            ".oXo.......",
            ".oXo.......",
            ".ooo.......",
        };

        /// <summary>İşaretin sprite'ı (ilk istendiğinde üretilir, sonra önbellekten).</summary>
        public static Sprite Get(MinimapIconKind kind)
        {
            if (Cache.TryGetValue(kind, out Sprite s) && s != null) return s;

            // Seçim halkası ve rota noktası PİKSEL DESENİNDEN değil, haritanın kullandığı
            // altıgen formülünden üretilir → karolarla birebir hizalanırlar. Elle çizilmiş
            // 11×11 bir "altıgen" bu hizayı asla tam tutturamazdı.
            s = kind switch
            {
                MinimapIconKind.Selection => BuildHex("Selection", 30, ringThickness: 0.16f),
                MinimapIconKind.PathDot   => BuildHex("PathDot",   18, ringThickness: 0f),
                _                         => Build(PatternOf(kind), kind.ToString())
            };

            Cache[kind] = s;
            return s;
        }

        /// <summary>
        /// Altıgen sprite üretir — <paramref name="ringThickness"/> 0 ise DOLU, &gt;0 ise o
        /// kalınlıkta HALKA (dış yarıçapın oranı olarak). Sivri-tepe altıgen; genişlik/yükseklik
        /// oranı gerçek karonunkiyle (√3/2) aynı tutulur ki ekranda karoya tam otursun.
        /// </summary>
        private static Sprite BuildHex(string name, int height, float ringThickness)
        {
            const float Outer = 1f, Inner = 0.866025404f;
            const float Slope = 0.5f * Outer / Inner;

            int w = Mathf.Max(3, Mathf.RoundToInt(height * (Inner / Outer)));
            var tex = new Texture2D(w, height, TextureFormat.RGBA32, false)
            {
                name = $"MinimapIcon_{name}", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var px = new Color32[w * height];
            var white = (Color32)Color.white;
            var clear = new Color32(0, 0, 0, 0);
            float shrink = 1f - Mathf.Clamp01(ringThickness);

            for (int y = 0; y < height; y++)
                for (int x = 0; x < w; x++)
                {
                    // Piksel merkezini altıgenin birim uzayına taşı.
                    float dx = Mathf.Abs((x + 0.5f) / w * 2f - 1f) * Inner;
                    float dz = Mathf.Abs((y + 0.5f) / height * 2f - 1f) * Outer;

                    bool inside = dx <= Inner && dz <= Outer - Slope * dx;
                    bool hollow = ringThickness > 0.001f &&
                                  dx <= Inner * shrink && dz <= (Outer - Slope * dx) * shrink;

                    px[y * w + x] = inside && !hollow ? white : clear;
                }

            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, w, height), new Vector2(0.5f, 0.5f), height);
        }

        private static string[] PatternOf(MinimapIconKind kind) => kind switch
        {
            MinimapIconKind.Market     => MarketPattern,
            MinimapIconKind.Encounter  => EncounterPattern,
            MinimapIconKind.Dungeon    => DungeonPattern,
            MinimapIconKind.Mandatory  => MandatoryPattern,
            MinimapIconKind.Watchtower => WatchtowerPattern,
            MinimapIconKind.Essence    => EssencePattern,
            MinimapIconKind.Waypoint   => WaypointPattern,
            _                          => PlayerPattern
        };

        private static Sprite Build(string[] pattern, string name)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name       = $"MinimapIcon_{name}",
                // Harita zaten iri pikselli — ikon da öyle olmalı, bulanık bir leke değil.
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp
            };

            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                // Desen YUKARIDAN AŞAĞI yazılır, doku ise ALTTAN yukarı indekslenir → satır ters çevrilir.
                string row = pattern[Size - 1 - y];
                for (int x = 0; x < Size; x++)
                {
                    char c = x < row.Length ? row[x] : '.';
                    px[y * Size + x] = c switch
                    {
                        'o' => (Color32)Outline,
                        'X' => (Color32)Fill,
                        '#' => (Color32)Accent,
                        _   => new Color32(0, 0, 0, 0)
                    };
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false);

            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
