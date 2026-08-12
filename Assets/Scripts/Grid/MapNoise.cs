using System;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// Harita üretimi için DETERMİNİSTİK gürültü kütüphanesi (Perlin tabanlı gradyan gürültüsü).
    ///
    /// NEDEN Unity'nin <c>Mathf.PerlinNoise</c>'u değil:
    ///   • Unity'nin implementasyonu sürümler arası garanti edilmez ve TOHUM ALMAZ (offset ile
    ///     taklit edilir) — "aynı seed → aynı harita" sözü riske girer.
    ///   • Bu dosya UnityEngine'e BAĞIMLI DEĞİL: seed havuzu taraması Unity açmadan, oyunda
    ///     çalışacak KODUN AYNISI derlenerek yapılabiliyor (Docs/Balance/tools/seed_taramasi).
    ///
    /// Organik kıta üretiminin üç yapıtaşı burada:
    ///   <see cref="Fbm"/>      — yumuşak, doğal dalgalanma (kıyı çizgisi, nem).
    ///   <see cref="Ridged"/>   — SIRT gürültüsü: yuvarlak lekeler yerine ÇİZGİSEL dağ silsileleri.
    ///   <see cref="Warp"/>     — domain warping: gürültüyü kendi kendine büküp "gürültü gibi değil,
    ///                            coğrafya gibi" görünen girintili çıkıntılı şekiller üretir.
    /// </summary>
    public static class MapNoise
    {
        // ── Hash → gradyan ───────────────────────────────────────────────────
        // 32-bit integer karıştırıcı (Wang/Murmur finalizer türevi). Aynı (x,y,seed) → aynı sonuç,
        // platformdan/derleyiciden bağımsız (sadece uint aritmetiği kullanır).
        private static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= (uint)x * 0x9E3779B1u;
                h ^= (uint)y * 0x85EBCA77u;
                h ^= h >> 15; h *= 0x2C1B3C6Du;
                h ^= h >> 12; h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>Hücre köşesinin birim gradyan vektörü (8 yön — klasik Perlin yaklaşımı).</summary>
        private static void Gradient(int x, int y, int seed, out float gx, out float gy)
        {
            uint h = Hash(x, y, seed) & 7u;
            switch (h)
            {
                case 0: gx =  1f; gy =  0f; return;
                case 1: gx = -1f; gy =  0f; return;
                case 2: gx =  0f; gy =  1f; return;
                case 3: gx =  0f; gy = -1f; return;
                case 4: gx =  0.7071f; gy =  0.7071f; return;
                case 5: gx = -0.7071f; gy =  0.7071f; return;
                case 6: gx =  0.7071f; gy = -0.7071f; return;
                default: gx = -0.7071f; gy = -0.7071f; return;
            }
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Tek oktav gradyan gürültüsü, yaklaşık [-1, 1].</summary>
        public static float Perlin(float x, float y, int seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            int x1 = x0 + 1,             y1 = y0 + 1;
            float fx = x - x0,           fy = y - y0;

            Gradient(x0, y0, seed, out float g00x, out float g00y);
            Gradient(x1, y0, seed, out float g10x, out float g10y);
            Gradient(x0, y1, seed, out float g01x, out float g01y);
            Gradient(x1, y1, seed, out float g11x, out float g11y);

            float n00 = g00x *  fx        + g00y *  fy;
            float n10 = g10x * (fx - 1f)  + g10y *  fy;
            float n01 = g01x *  fx        + g01y * (fy - 1f);
            float n11 = g11x * (fx - 1f)  + g11y * (fy - 1f);

            float u = Fade(fx), v = Fade(fy);
            return Lerp(Lerp(n00, n10, u), Lerp(n01, n11, u), v) * 1.4142f; // ~[-1,1]'e ölçekle
        }

        /// <summary>Fraktal Brown hareketi — birden çok oktav üst üste. Sonuç ~[-1, 1].</summary>
        public static float Fbm(float x, float y, int seed, int octaves = 4,
                                float lacunarity = 2.03f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum  += Perlin(x * freq, y * freq, seed + i * 7919) * amp;
                norm += amp;
                amp  *= gain;
                freq *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>SIRT gürültüsü: |n| tersine çevrilir → keskin çizgisel sırtlar. Sonuç [0, 1].
        /// Dağların yuvarlak leke değil SİLSİLE olmasını sağlayan şey budur.</summary>
        public static float Ridged(float x, float y, int seed, int octaves = 4,
                                   float lacunarity = 2.07f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f, freq = 1f, prev = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Math.Abs(Perlin(x * freq, y * freq, seed + i * 6151));
                n *= n;
                n *= prev;                 // sırtlar üst oktavlarda "keskinleşsin"
                prev = n;
                sum  += n * amp;
                norm += amp;
                amp  *= gain;
                freq *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Domain warping — koordinatı başka bir gürültüyle iter. Kıyı çizgisi ve biyom
        /// sınırları "daire/elips" olmaktan çıkıp burun-koy-yarımada üretmeye başlar.</summary>
        public static void Warp(ref float x, ref float y, int seed, float amount, float freq)
        {
            float wx = Fbm(x * freq + 13.7f, y * freq -  9.1f, seed ^ 0x5F3759D, 3);
            float wy = Fbm(x * freq -  4.3f, y * freq + 21.5f, seed ^ 0x1B873593, 3);
            x += wx * amount;
            y += wy * amount;
        }

        /// <summary>Deterministik 0..1 rastgele değer (gürültü değil — nokta bazlı serpiştirme için).
        /// Aynı hücreye her zaman aynı değeri verir, sırayla çekim yapmadığı için üretim sırasına
        /// bağımlılık yaratmaz.</summary>
        public static float White(int x, int y, int seed) => (Hash(x, y, seed) >> 8) * (1f / 16777216f);
    }
}
