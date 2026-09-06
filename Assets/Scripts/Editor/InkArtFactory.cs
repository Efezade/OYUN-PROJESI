using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TacticalRPG.Editor
{
    /// <summary>Mürekkep ikon çeşitleri (game UI.pdf'teki el çizimi glifler).</summary>
    public enum InkIcon
    {
        Lock, Sword, Shield, Flame, Drop, Wind, Spiral, Star, Hand, Leaf, Book, Bag, Scroll, Gear
    }

    /// <summary>
    /// KİTAP'taki karakter büstlerinin çeşidi (2026-09-06). Ayrım BAŞLIK + ELDEKİ ALET'ten gelir:
    /// mockup'ın diliyle uyumlu, tek renk mürekkeple çizilen bir silüette yüz ifadesi okunmaz,
    /// siluet okunur. Gerçek splash art gelince bu büstler yerini onlara bırakır.
    /// </summary>
    public enum InkBust
    {
        /// <summary>Miğferli asker + kılıç (Savaşçı).</summary>
        Kilic,
        /// <summary>Boynuzlu başlık + balta (Barbar).</summary>
        Balta,
        /// <summary>Kapüşon + yay (Okçu / Ranger).</summary>
        Yay,
        /// <summary>Sivri şapka + asa (Büyücü).</summary>
        Asa,
        /// <summary>Kapüşon + hale (Rahip).</summary>
        Hale,
        /// <summary>Bandana + hançer (Serseri).</summary>
        Hancer
    }

    /// <summary>
    /// EL ÇİZİMİ MÜREKKEP UI SANATI — prosedürel (2026-09-04, Efe'nin isteği: "UI, game UI.pdf'teki
    /// gibi profesyonel görünsün").
    ///
    /// NEDEN PROSEDÜREL: mockup'taki dil (dalgalı mürekkep çizgi, çift kontur, organik dallar) hazır
    /// Unity sprite'larıyla (Background.psd / Knob.psd) taklit edilemiyordu — o sprite'lar pürüzsüz
    /// ve düzgün, sonuç "programcı çizimi" duruyordu. Elle çizilmiş atlas gelene kadar aradaki fark
    /// KODLA kapatılıyor; bu, projedeki karo/küre fabrikalarının (TileVisualFactory,
    /// EssenceOrbFactory) aynı deseni.
    ///
    /// ÜRETİLEN DOSYA GERÇEK ASSET'TİR: `Assets/Art/UI/Ink/*.png` olarak yazılır ve Sprite olarak
    /// import edilir — bellekte üretilen Texture2D sahne yeniden açılınca KAYBOLURDU (UI'da
    /// "eksik sprite" pembe kutular kalırdı).
    ///
    /// DETERMİNİST: her çizimin sarsıntısı ADINDAN türeyen seed ile üretilir. Aynı ad → aynı çizgi;
    /// kurulum tekrar koşturulduğunda UI "titremez".
    ///
    /// İDEMPOTENT: dosya varsa YENİDEN ÜRETİLMEZ (Efe elle bir PNG'yi değiştirirse TAM KURULUM onu
    /// ezmesin). Yeniden üretmek için dosyayı silmek yeterli.
    /// </summary>
    public static class InkArtFactory
    {
        public const string InkFolder = "Assets/Art/UI/Ink";

        /// <summary>Kâğıt üstündeki koyu mürekkep. Saf siyah DEĞİL — mockup'taki kalem izi gibi
        /// sıcak ve hafif şeffaf, üst üste binen çizgiler koyulaşsın.</summary>
        public static readonly Color Ink = new(0.13f, 0.10f, 0.07f, 1f);

        // ── Genel API ────────────────────────────────────────────────────────

        /// <summary>El çizimi çerçeve (9-slice): dalgalı dikdörtgen kontur + köşe süsleri.
        /// İçi BOŞ (şeffaf) — arkasındaki kâğıt görünür.</summary>
        public static Sprite Frame(string name, int w, int h, int radius, float thickness = 5f,
                                   bool flourish = true)
        {
            // Süslü/süssüz aynı ada düşmesin: aynı ölçüde iki farklı çizim var.
            string path = $"{InkFolder}/{name}{(flourish ? "" : "_plain")}.png";
            Sprite cached = Load(path);
            if (cached != null) return cached;

            var px = NewCanvas(w, h);
            var rnd = new System.Random(Seed(name));

            List<Vector2> outline = RoundedRect(new Rect(thickness, thickness,
                                                         w - thickness * 2f, h - thickness * 2f), radius);
            HandStroke(px, w, h, outline, thickness, rnd, closed: true);

            if (flourish)
            {
                // Köşe kıvrımları: mockup'taki "kâğıdın köşesine atılmış" küçük süsler.
                float m = radius + thickness * 2f;
                CornerFlourish(px, w, h, new Vector2(m, m),             1f,  1f, rnd);
                CornerFlourish(px, w, h, new Vector2(w - m, m),        -1f,  1f, rnd);
                CornerFlourish(px, w, h, new Vector2(m, h - m),         1f, -1f, rnd);
                CornerFlourish(px, w, h, new Vector2(w - m, h - m),    -1f, -1f, rnd);
            }

            int border = Mathf.RoundToInt(radius + thickness * 3f);
            return Save(px, w, h, path, new Vector4(border, border, border, border));
        }

        /// <summary>Düz kâğıt zemin (hafif dokulu krem). 9-slice, kenarı yok.</summary>
        public static Sprite Paper(string name, int w, int h, Color tint)
        {
            string path = $"{InkFolder}/{name}.png";
            Sprite cached = Load(path);
            if (cached != null) return cached;

            var px = NewCanvas(w, h);
            var rnd = new System.Random(Seed(name));

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Çok hafif elyaf dokusu: düz renk "dijital" duruyor, kâğıt hissi vermiyordu.
                    float n = 0.985f + (float)rnd.NextDouble() * 0.03f;
                    px[y * w + x] = new Color(tint.r * n, tint.g * n, tint.b * n, tint.a);
                }

            return Save(px, w, h, path, new Vector4(8, 8, 8, 8));
        }

        /// <summary>El çizimi daire düğüm: çift kontur (dış kalın, iç ince) + içi şeffaf.</summary>
        public static Sprite Node(string name, int diameter, float thickness = 5f)
        {
            string path = $"{InkFolder}/{name}.png";
            Sprite cached = Load(path);
            if (cached != null) return cached;

            var px = NewCanvas(diameter, diameter);
            var rnd = new System.Random(Seed(name));
            var c = new Vector2(diameter * 0.5f, diameter * 0.5f);

            HandStroke(px, diameter, diameter, CirclePath(c, diameter * 0.5f - thickness),
                       thickness, rnd, closed: true);
            HandStroke(px, diameter, diameter, CirclePath(c, diameter * 0.5f - thickness * 2.6f),
                       thickness * 0.45f, rnd, closed: true);

            return Save(px, diameter, diameter, path, Vector4.zero);
        }

        /// <summary>Dolu daire (madalyon zemini) — düğümün ARKASINA konur, durum rengini taşır.</summary>
        public static Sprite Disc(string name, int diameter)
        {
            string path = $"{InkFolder}/{name}.png";
            Sprite cached = Load(path);
            if (cached != null) return cached;

            var px = NewCanvas(diameter, diameter);
            float r = diameter * 0.5f - 1.5f;
            var c = new Vector2(diameter * 0.5f, diameter * 0.5f);
            var rnd = new System.Random(Seed(name));

            // Kenarı hafif düzensiz: mükemmel daire "vektör" duruyor, mürekkep lekesi değil.
            for (int y = 0; y < diameter; y++)
                for (int x = 0; x < diameter; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float ang = Mathf.Atan2(p.y - c.y, p.x - c.x);
                    float wob = 1f + 0.012f * Mathf.Sin(ang * 5f + Seed(name) % 6);
                    float a = Mathf.Clamp01(r * wob - Vector2.Distance(p, c) + 0.5f);
                    if (a > 0f) px[y * diameter + x] = new Color(1f, 1f, 1f, a);
                }

            return Save(px, diameter, diameter, path, Vector4.zero);
        }

        /// <summary>Tek bir el çizimi ikon (beyaz üstüne mürekkep, renk UI'da tint'lenir).</summary>
        public static Sprite Icon(InkIcon kind, int size)
        {
            string name = $"icon_{kind.ToString().ToLowerInvariant()}_{size}";
            string path = $"{InkFolder}/{name}.png";
            Sprite cached = Load(path);
            if (cached != null) return cached;

            var px = NewCanvas(size, size);
            var rnd = new System.Random(Seed(name));
            DrawIcon(px, size, kind, rnd);
            return Save(px, size, size, path, Vector4.zero);
        }

        /// <summary>
        /// KARAKTER BÜSTÜ — omuz + boyun + baş silüeti, üstüne sınıfın başlığı ve aleti.
        /// KİTAP'taki "karakterler" sayfası bunu splash art yerine kullanır (yer tutucu).
        /// </summary>
        public static Sprite Bust(InkBust kind, int size)
        {
            string name = $"bust_{kind.ToString().ToLowerInvariant()}_{size}";
            string path = $"{InkFolder}/{name}.png";
            Sprite cached = Load(path);
            if (cached != null) return cached;

            var px = NewCanvas(size, size);
            var rnd = new System.Random(Seed(name));
            DrawBust(px, size, kind, rnd);
            return Save(px, size, size, path, Vector4.zero);
        }

        /// <summary>
        /// YETENEK AĞACININ GÖVDESİ: verilen kenarları (ebeveyn → çocuk) tek bir dokuya, gövdeden
        /// dallanan organik çizgiler olarak çizer. Mockup'taki ağaç bu: dallar aşağıdaki KÖKTEN
        /// çıkıyor, yukarı doğru inceliyor.
        ///
        /// Tek doku olmasının sebebi: her kenarı ayrı Image yapmak dalları birbirinden kopuk
        /// gösteriyordu (kesişimde çizgiler üst üste biniyor, uçlar havada kalıyordu).
        /// </summary>
        public static Sprite Branches(string name, int w, int h, Vector2 root,
                                      IReadOnlyList<(Vector2 from, Vector2 to)> edges,
                                      IReadOnlyList<Vector2> rootChildren)
        {
            string path = $"{InkFolder}/{name}.png";
            Sprite cached = Load(path);
            if (cached != null) return cached;

            var px = NewCanvas(w, h);
            var rnd = new System.Random(Seed(name));

            // 0) GÖVDENİN DİBİ: kökten aşağı inen kısa, kalın bir sap. Olmadan dallar havada
            //    birleşiyor gibi duruyordu (mockup'ta ağaç gövdeden çıkıyor).
            Stamp(px, w, h, root, 6f);
            for (int i = 0; i <= 60; i++)
            {
                float t = i / 60f;
                float th = Mathf.Lerp(13f, 7f, t);
                var p2 = new Vector2(root.x + Mathf.Sin(t * 2.4f) * 3f, root.y - t * 52f);
                Stamp(px, w, h, p2, th * 0.5f);
            }

            // 1) Kökten ilk kademe düğümlere KALIN, kıvrımlı ana dallar.
            foreach (Vector2 child in rootChildren)
                Branch(px, w, h, root, child, 11.5f, 6f, rnd);

            // 2) Düğümden düğüme daha ince dallar. Uçların birleştiği yerde oluşan köşeleri
            //    düğüm madalyonları (92 px) örtüyor — bu yüzden zincir tek eğri olmak zorunda değil.
            foreach (var e in edges)
                Branch(px, w, h, e.from, e.to, 6.5f, 3.8f, rnd);

            return Save(px, w, h, path, Vector4.zero);
        }

        // ── Çizim çekirdeği ──────────────────────────────────────────────────

        private static Color[] NewCanvas(int w, int h)
        {
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, 0f);
            return px;
        }

        /// <summary>Bir dal: kaynaktan hedefe kıvrımlı, UCA DOĞRU İNCELEN mürekkep çizgisi.</summary>
        private static void Branch(Color[] px, int w, int h, Vector2 a, Vector2 b,
                                   float thickBase, float thickTip, System.Random rnd)
        {
            // Kontrol noktası: iki ucun ortasından dikey kaydırılmış → dal düz DEĞİL, kavisli.
            Vector2 mid = (a + b) * 0.5f;
            Vector2 d   = b - a;
            var normal  = new Vector2(-d.y, d.x).normalized;
            float bend  = d.magnitude * (0.12f + (float)rnd.NextDouble() * 0.10f);
            Vector2 ctrl = mid + normal * bend * (rnd.Next(2) == 0 ? 1f : -1f);

            const int steps = 220;
            float ph1 = (float)rnd.NextDouble() * 6.28f;
            float ph2 = (float)rnd.NextDouble() * 6.28f;

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Bezier(a, ctrl, b, t);

                // Çizgi kalınlığı uca doğru azalır (ağaç dalı hissi).
                float th = Mathf.Lerp(thickBase, thickTip, t);
                float wob = 0.9f * Mathf.Sin(t * 9f + ph1) + 0.5f * Mathf.Sin(t * 21f + ph2);
                Vector2 n = Perp(Bezier(a, ctrl, b, Mathf.Min(1f, t + 0.01f)) - p);

                Stamp(px, w, h, p + n * wob, th * 0.5f);
            }
        }

        /// <summary>El çizimi çizgi: yol boyunca sarsıntılı çift kontur.</summary>
        private static void HandStroke(Color[] px, int w, int h, List<Vector2> path,
                                       float thickness, System.Random rnd, bool closed)
        {
            StrokePass(px, w, h, path, thickness, rnd, closed, 1f);
            // İkinci geçiş: biraz daha ince ve kaymış → kalemin iki kez geçtiği izlenimi.
            StrokePass(px, w, h, path, thickness * 0.55f, rnd, closed, 1.6f);
        }

        private static void StrokePass(Color[] px, int w, int h, List<Vector2> path,
                                       float thickness, System.Random rnd, bool closed, float wobScale)
        {
            if (path == null || path.Count < 2) return;
            float ph1 = (float)rnd.NextDouble() * 6.28f;
            float ph2 = (float)rnd.NextDouble() * 6.28f;
            float amp = Mathf.Max(0.6f, thickness * 0.22f) * wobScale;

            int last = closed ? path.Count : path.Count - 1;
            float travelled = 0f;

            for (int i = 0; i < last; i++)
            {
                Vector2 a = path[i];
                Vector2 b = path[(i + 1) % path.Count];
                float len = Vector2.Distance(a, b);
                int steps = Mathf.Max(2, Mathf.CeilToInt(len * 2f));
                Vector2 n = Perp(b - a);

                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    float u = (travelled + len * t) * 0.06f;
                    float wob = amp * (0.7f * Mathf.Sin(u * 3.1f + ph1) + 0.3f * Mathf.Sin(u * 7.7f + ph2));
                    Stamp(px, w, h, Vector2.Lerp(a, b, t) + n * wob, thickness * 0.5f);
                }
                travelled += len;
            }
        }

        /// <summary>Yumuşak kenarlı mürekkep damgası (yol boyunca üst üste basılır).</summary>
        private static void Stamp(Color[] px, int w, int h, Vector2 c, float r, bool erase = false)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r - 1f));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(c.x + r + 1f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r - 1f));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(c.y + r + 1f));

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a <= 0f) continue;

                    int idx = y * w + x;
                    // Üst üste binen çizgiler koyulaşsın (mürekkep birikmesi); OYMA kipinde ise
                    // alfa silinir → dolu silüetin içine kâğıt rengi detay açılır.
                    float na = erase ? Mathf.Min(px[idx].a, 1f - a) : Mathf.Max(px[idx].a, a);
                    px[idx] = new Color(1f, 1f, 1f, na);
                }
        }

        private static void CornerFlourish(Color[] px, int w, int h, Vector2 at,
                                           float sx, float sy, System.Random rnd)
        {
            var p = new List<Vector2>();
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f;
                // Küçük "S" kıvrımı: köşeden içeri doğru akan bir kalem hareketi.
                float x = at.x + sx * (14f + 46f * t);
                float y = at.y + sy * (10f * Mathf.Sin(t * 3.4f) + 4f);
                p.Add(new Vector2(x, y));
            }
            StrokePass(px, w, h, p, 3.2f, rnd, false, 1f);
        }

        // ── Yol üreticiler ───────────────────────────────────────────────────

        private static List<Vector2> RoundedRect(Rect r, float radius)
        {
            var p = new List<Vector2>();
            radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);

            void Arc(Vector2 c, float from, float to)
            {
                for (int i = 0; i <= 8; i++)
                {
                    float a = Mathf.Lerp(from, to, i / 8f);
                    p.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
            }

            p.Add(new Vector2(r.xMin + radius, r.yMin));
            p.Add(new Vector2(r.xMax - radius, r.yMin));
            Arc(new Vector2(r.xMax - radius, r.yMin + radius), -Mathf.PI * 0.5f, 0f);
            p.Add(new Vector2(r.xMax, r.yMax - radius));
            Arc(new Vector2(r.xMax - radius, r.yMax - radius), 0f, Mathf.PI * 0.5f);
            p.Add(new Vector2(r.xMin + radius, r.yMax));
            Arc(new Vector2(r.xMin + radius, r.yMax - radius), Mathf.PI * 0.5f, Mathf.PI);
            p.Add(new Vector2(r.xMin, r.yMin + radius));
            Arc(new Vector2(r.xMin + radius, r.yMin + radius), Mathf.PI, Mathf.PI * 1.5f);
            return p;
        }

        private static List<Vector2> CirclePath(Vector2 c, float r)
        {
            var p = new List<Vector2>();
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                p.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return p;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        private static Vector2 Perp(Vector2 v)
        {
            Vector2 n = v.sqrMagnitude < 1e-6f ? Vector2.up : v.normalized;
            return new Vector2(-n.y, n.x);
        }

        // ── Büst çizimi (omuz + boyun + baş + başlık + alet) ────────────────

        private static void DrawBust(Color[] px, int size, InkBust kind, System.Random rnd)
        {
            float S = size;
            Vector2 P(float x, float y) => new(x * S, y * S);

            // KAMEA (silüet) YAKLAŞIMI: gövde ve baş DOLU mürekkep, detaylar OYULARAK açılır.
            // İlk deneme ince konturlarla çizilmişti ve "çöp adam" gibi duruyordu; dolu silüet
            // hem çok daha okunur hem de tek renk mürekkeple profesyonel görünüyor.
            bool InTorso(float x, float y)
            {
                if (y < 0.00f || y > 0.47f) return false;
                float t = y / 0.47f;                       // 0 = alt (geniş), 1 = omuz üstü
                float halfW = Mathf.Lerp(0.42f, 0.17f, Mathf.Pow(t, 1.7f));
                return Mathf.Abs(x - 0.5f) <= halfW;
            }
            bool InNeck(float x, float y) => y >= 0.42f && y <= 0.60f && Mathf.Abs(x - 0.5f) <= 0.085f;
            bool InHead(float x, float y) => Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.68f)) <= 0.155f;

            // Boynuz maskesi (Barbar) — dışa/yukarı kıvrılıp incelen koni.
            bool HornMask(float x, float y, float dir)
            {
                for (int i = 0; i <= 14; i++)
                {
                    float t  = i / 14f;
                    float hx = 0.5f + dir * (0.14f + 0.13f * t);
                    float hy = 0.80f + 0.15f * t - 0.06f * t * t;
                    float r  = Mathf.Lerp(0.045f, 0.012f, t);
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(hx, hy)) <= r) return true;
                }
                return false;
            }

            // Başlık: sınıfı ayıran asıl işaret (silüette yüz okunmaz, BAŞLIK okunur).
            bool InHat(float x, float y)
            {
                switch (kind)
                {
                    case InkBust.Kilic:
                        return Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.68f)) <= 0.175f && y >= 0.68f;

                    case InkBust.Balta:
                    {
                        bool dome = Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.70f)) <= 0.17f && y >= 0.70f;
                        return dome || HornMask(x, y, -1f) || HornMask(x, y, +1f);
                    }

                    case InkBust.Yay:
                    case InkBust.Hale:
                        return Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.70f)) <= 0.215f && y >= 0.56f;

                    case InkBust.Asa:
                    {
                        bool brim = y >= 0.80f && y <= 0.845f && Mathf.Abs(x - 0.5f) <= 0.26f;
                        float t   = Mathf.InverseLerp(0.82f, 1.00f, y);
                        bool cone = y >= 0.82f && y <= 1.00f && Mathf.Abs(x - 0.5f) <= Mathf.Lerp(0.20f, 0.005f, t);
                        return brim || cone;
                    }

                    case InkBust.Hancer:
                        return y >= 0.76f && y <= 0.83f
                               && Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.68f)) <= 0.185f;
                }
                return false;
            }

            // 1) SİLÜETİ DOLDUR.
            for (int py = 0; py < size; py++)
                for (int pxi = 0; pxi < size; pxi++)
                {
                    float x = (pxi + 0.5f) / S, y = (py + 0.5f) / S;
                    if (InTorso(x, y) || InNeck(x, y) || InHead(x, y) || InHat(x, y))
                        px[py * size + pxi] = new Color(1f, 1f, 1f, 1f);
                }

            // 2) DETAYLARI OY (alfa silinir) — kâğıt geri sızar, silüet tek blok kalmaz.
            void Carve(float th, params Vector2[] pts)
            {
                foreach (Vector2 pt in Densify(new List<Vector2>(pts)))
                    Stamp(px, size, size, pt, th * S * 0.5f, erase: true);
            }

            Carve(0.018f, P(0.30f, 0.30f), P(0.42f, 0.38f), P(0.58f, 0.38f), P(0.70f, 0.30f));  // yaka

            switch (kind)
            {
                case InkBust.Kilic:                                    // miğfer siperi
                    Carve(0.022f, P(0.50f, 0.80f), P(0.50f, 0.60f));
                    Carve(0.016f, P(0.38f, 0.66f), P(0.62f, 0.66f));
                    break;
                case InkBust.Balta:                                    // başlık kenarı
                    Carve(0.018f, P(0.34f, 0.72f), P(0.66f, 0.72f));
                    break;
                case InkBust.Yay:                                      // kapüşon ağzı
                    Carve(0.020f, P(0.38f, 0.60f), P(0.36f, 0.72f), P(0.44f, 0.80f));
                    Carve(0.020f, P(0.62f, 0.60f), P(0.64f, 0.72f), P(0.56f, 0.80f));
                    break;
                case InkBust.Hale:                                     // kapüşon ağzı + göğüs işareti
                    Carve(0.020f, P(0.38f, 0.60f), P(0.36f, 0.72f), P(0.44f, 0.80f));
                    Carve(0.020f, P(0.62f, 0.60f), P(0.64f, 0.72f), P(0.56f, 0.80f));
                    Carve(0.020f, P(0.50f, 0.30f), P(0.50f, 0.14f));
                    Carve(0.020f, P(0.43f, 0.23f), P(0.57f, 0.23f));
                    break;
                case InkBust.Asa:                                      // şapka bandı
                    Carve(0.018f, P(0.32f, 0.855f), P(0.68f, 0.855f));
                    break;
                case InkBust.Hancer:                                   // bandana düğümü
                    Carve(0.014f, P(0.50f, 0.83f), P(0.50f, 0.76f));
                    break;
            }

            // 3) ALET: silüetin YANINDA kontur olarak (dolu gövdeden ayrışsın).
            void Line(float th, params Vector2[] pts)
                => HandStroke(px, size, size, new List<Vector2>(pts), th * S / 220f, rnd, false);
            void Curve(Vector2 a, Vector2 c, Vector2 b, float th)
            {
                var list = new List<Vector2>();
                for (int i = 0; i <= 22; i++) list.Add(Bezier(a, c, b, i / 22f));
                HandStroke(px, size, size, list, th * S / 220f, rnd, false);
            }

            switch (kind)
            {
                case InkBust.Kilic:                                    // kılıç
                    Line(6f, P(0.84f, 0.06f), P(0.90f, 0.70f));
                    Line(5f, P(0.78f, 0.30f), P(0.95f, 0.26f));
                    break;
                case InkBust.Balta:                                    // balta
                    Line(6f, P(0.86f, 0.04f), P(0.86f, 0.66f));
                    Curve(P(0.86f, 0.66f), P(0.70f, 0.52f), P(0.86f, 0.40f), 5.5f);
                    break;
                case InkBust.Yay:                                      // yay + kiriş
                    Curve(P(0.90f, 0.08f), P(0.70f, 0.42f), P(0.90f, 0.76f), 5.5f);
                    Line(3.6f, P(0.90f, 0.08f), P(0.90f, 0.76f));
                    break;
                case InkBust.Asa:                                      // asa + taş
                    Line(6f, P(0.88f, 0.02f), P(0.88f, 0.74f));
                    HandStroke(px, size, size, CirclePath(P(0.88f, 0.80f), 0.055f * S), 4.5f * S / 220f, rnd, true);
                    break;
                case InkBust.Hale:                                     // hale
                    HandStroke(px, size, size, CirclePath(P(0.50f, 0.955f), 0.115f * S), 4.5f * S / 220f, rnd, true);
                    break;
                case InkBust.Hancer:                                   // hançer
                    Line(5.5f, P(0.86f, 0.18f), P(0.92f, 0.56f));
                    Line(5f,   P(0.81f, 0.22f), P(0.91f, 0.19f));
                    break;
            }
        }

        /// <summary>Yolu piksel adımlarına böler (oyma damgaları arasında boşluk kalmasın).</summary>
        private static List<Vector2> Densify(List<Vector2> path)
        {
            var outp = new List<Vector2>();
            for (int i = 0; i < path.Count - 1; i++)
            {
                float len = Vector2.Distance(path[i], path[i + 1]);
                int steps = Mathf.Max(2, Mathf.CeilToInt(len * 2f));
                for (int s = 0; s <= steps; s++) outp.Add(Vector2.Lerp(path[i], path[i + 1], s / (float)steps));
            }
            return outp;
        }

        // ── İkon çizimleri (basit çizgi grafikleri, hepsi 0..1 kutusunda) ────

        private static void DrawIcon(Color[] px, int size, InkIcon kind, System.Random rnd)
        {
            float S = size;
            Vector2 P(float x, float y) => new(x * S, y * S);
            void Path(bool closed, float th, params Vector2[] pts)
                => HandStroke(px, size, size, new List<Vector2>(pts), th * S / 64f, rnd, closed);
            void Curve(Vector2 a, Vector2 c, Vector2 b, float th)
            {
                var list = new List<Vector2>();
                for (int i = 0; i <= 16; i++) list.Add(Bezier(a, c, b, i / 16f));
                HandStroke(px, size, size, list, th * S / 64f, rnd, false);
            }

            switch (kind)
            {
                case InkIcon.Lock:
                    Path(true, 4f, P(0.26f, 0.14f), P(0.74f, 0.14f), P(0.74f, 0.52f), P(0.26f, 0.52f));
                    Curve(P(0.36f, 0.52f), P(0.50f, 0.92f), P(0.64f, 0.52f), 4f);
                    Path(true, 3f, P(0.46f, 0.28f), P(0.54f, 0.28f), P(0.54f, 0.38f), P(0.46f, 0.38f));
                    break;

                case InkIcon.Sword:
                    Path(false, 4.5f, P(0.30f, 0.20f), P(0.72f, 0.80f));
                    Path(false, 4f,   P(0.24f, 0.42f), P(0.44f, 0.24f));   // çapraz balçak
                    Path(false, 3.5f, P(0.22f, 0.24f), P(0.34f, 0.16f));   // kabza
                    break;

                case InkIcon.Shield:
                    Path(false, 4.5f, P(0.50f, 0.88f), P(0.20f, 0.70f), P(0.22f, 0.36f),
                                       P(0.50f, 0.12f), P(0.78f, 0.36f), P(0.80f, 0.70f), P(0.50f, 0.88f));
                    Path(false, 3f, P(0.50f, 0.74f), P(0.50f, 0.30f));
                    break;

                // ALEV ile DAMLA birbirinden AYRI okunmalı (ilk denemede ikisi de aynı mercek
                // şekli çıkmıştı): alev asimetrik + dalgalı tabanlı ve içinde bir dil taşır,
                // damla ise sivri uçlu + YUVARLAK tabanlıdır.
                case InkIcon.Flame:
                    Curve(P(0.50f, 0.94f), P(0.88f, 0.56f), P(0.64f, 0.16f), 4.5f);   // sağ yalaz
                    Curve(P(0.64f, 0.16f), P(0.50f, 0.28f), P(0.36f, 0.16f), 4.0f);   // dalgalı taban
                    Curve(P(0.36f, 0.16f), P(0.14f, 0.54f), P(0.50f, 0.94f), 4.5f);   // sol yalaz
                    Curve(P(0.50f, 0.62f), P(0.34f, 0.38f), P(0.49f, 0.22f), 3.0f);   // iç dil
                    Curve(P(0.49f, 0.22f), P(0.62f, 0.40f), P(0.50f, 0.62f), 3.0f);
                    break;

                case InkIcon.Drop:
                    Curve(P(0.50f, 0.92f), P(0.78f, 0.52f), P(0.74f, 0.30f), 4.5f);   // sağ kenar
                    Curve(P(0.74f, 0.30f), P(0.50f, 0.02f), P(0.26f, 0.30f), 4.5f);   // yuvarlak taban
                    Curve(P(0.26f, 0.30f), P(0.22f, 0.52f), P(0.50f, 0.92f), 4.5f);   // sol kenar
                    break;

                case InkIcon.Wind:
                    Curve(P(0.14f, 0.66f), P(0.52f, 0.86f), P(0.80f, 0.62f), 4f);
                    Curve(P(0.14f, 0.46f), P(0.60f, 0.62f), P(0.86f, 0.40f), 4f);
                    Curve(P(0.20f, 0.26f), P(0.52f, 0.40f), P(0.72f, 0.20f), 3.5f);
                    break;

                case InkIcon.Spiral:
                {
                    var pts = new List<Vector2>();
                    for (int i = 0; i <= 90; i++)
                    {
                        float t = i / 90f;
                        float a = t * Mathf.PI * 4.2f;
                        float r = 0.06f + t * 0.36f;
                        pts.Add(P(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r));
                    }
                    HandStroke(px, size, size, pts, 4f * S / 64f, rnd, false);
                    break;
                }

                case InkIcon.Star:
                {
                    var pts = new List<Vector2>();
                    for (int i = 0; i < 10; i++)
                    {
                        float a = -Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                        float r = (i % 2 == 0) ? 0.40f : 0.17f;
                        pts.Add(P(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r));
                    }
                    HandStroke(px, size, size, pts, 3.6f * S / 64f, rnd, true);
                    break;
                }

                case InkIcon.Hand:
                    Path(true, 4f, P(0.30f, 0.14f), P(0.70f, 0.14f), P(0.72f, 0.52f),
                                   P(0.60f, 0.66f), P(0.40f, 0.66f), P(0.28f, 0.52f));
                    Path(false, 3.4f, P(0.34f, 0.62f), P(0.34f, 0.86f));
                    Path(false, 3.4f, P(0.45f, 0.64f), P(0.45f, 0.92f));
                    Path(false, 3.4f, P(0.56f, 0.64f), P(0.56f, 0.90f));
                    Path(false, 3.4f, P(0.67f, 0.60f), P(0.70f, 0.82f));
                    break;

                case InkIcon.Leaf:
                    Curve(P(0.20f, 0.18f), P(0.20f, 0.80f), P(0.80f, 0.82f), 4.2f);
                    Curve(P(0.20f, 0.18f), P(0.78f, 0.20f), P(0.80f, 0.82f), 4.2f);
                    Path(false, 3f, P(0.24f, 0.22f), P(0.74f, 0.76f));
                    break;

                case InkIcon.Book:
                    Path(false, 4.2f, P(0.10f, 0.24f), P(0.48f, 0.34f), P(0.48f, 0.84f), P(0.10f, 0.74f), P(0.10f, 0.24f));
                    Path(false, 4.2f, P(0.90f, 0.24f), P(0.52f, 0.34f), P(0.52f, 0.84f), P(0.90f, 0.74f), P(0.90f, 0.24f));
                    Path(false, 2.6f, P(0.16f, 0.44f), P(0.42f, 0.52f));
                    Path(false, 2.6f, P(0.58f, 0.52f), P(0.84f, 0.44f));
                    break;

                case InkIcon.Bag:
                    Path(true, 4.2f, P(0.16f, 0.12f), P(0.84f, 0.12f), P(0.84f, 0.66f), P(0.16f, 0.66f));
                    Curve(P(0.32f, 0.66f), P(0.50f, 0.96f), P(0.68f, 0.66f), 4f);   // sap
                    Path(false, 3f, P(0.16f, 0.40f), P(0.84f, 0.40f));
                    Path(true, 3f, P(0.44f, 0.30f), P(0.56f, 0.30f), P(0.56f, 0.44f), P(0.44f, 0.44f));
                    break;

                case InkIcon.Scroll:
                    Path(false, 4.2f, P(0.14f, 0.26f), P(0.86f, 0.26f), P(0.86f, 0.74f), P(0.14f, 0.74f), P(0.14f, 0.26f));
                    Curve(P(0.14f, 0.74f), P(0.06f, 0.62f), P(0.14f, 0.50f), 3.4f);
                    Curve(P(0.86f, 0.26f), P(0.94f, 0.38f), P(0.86f, 0.50f), 3.4f);
                    Path(false, 2.6f, P(0.28f, 0.56f), P(0.62f, 0.56f));
                    Path(false, 2.6f, P(0.28f, 0.44f), P(0.70f, 0.44f));
                    break;

                case InkIcon.Gear:
                {
                    var outer = new List<Vector2>();
                    for (int i = 0; i < 48; i++)
                    {
                        float t = i / 48f;
                        float a = t * Mathf.PI * 2f;
                        float r = 0.34f + (Mathf.Cos(a * 8f) > 0f ? 0.07f : 0f);
                        outer.Add(P(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r));
                    }
                    HandStroke(px, size, size, outer, 3.6f * S / 64f, rnd, true);
                    HandStroke(px, size, size, CirclePath(P(0.5f, 0.5f), 0.13f * S), 3.4f * S / 64f, rnd, true);
                    break;
                }
            }
        }

        // ── Kaydetme / yükleme ───────────────────────────────────────────────

        private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static int Seed(string name)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in name) h = h * 31 + c;
                return h & 0x7FFFFFFF;
            }
        }

        private static Sprite Save(Color[] px, int w, int h, string path, Vector4 border)
        {
            Directory.CreateDirectory(InkFolder);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType         = TextureImporterType.Sprite;
                ti.spriteImportMode    = SpriteImportMode.Single;
                ti.spriteBorder        = border;
                ti.spritePixelsPerUnit = 100f;
                ti.mipmapEnabled       = false;
                ti.alphaIsTransparency = true;
                ti.filterMode          = FilterMode.Bilinear;
                ti.textureCompression  = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            return Load(path);
        }
    }
}
