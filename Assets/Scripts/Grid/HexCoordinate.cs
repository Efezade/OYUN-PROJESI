using System;
using UnityEngine;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// Axial koordinat sistemi (q, r). Pointy-top hex layout.
    /// Referans: https://www.redblobgames.com/grids/hexagons/
    /// </summary>
    [Serializable]
    public struct HexCoordinate : IEquatable<HexCoordinate>
    {
        public int Q;
        public int R;

        // Cube koordinatında S, Q+R+S=0 kuralından türetilir
        public int S => -Q - R;

        public static readonly HexCoordinate Zero = new(0, 0);

        // Pointy-top altı komşu yönü (axial)
        public static readonly HexCoordinate[] Directions =
        {
            new( 1,  0), new( 1, -1), new( 0, -1),
            new(-1,  0), new(-1,  1), new( 0,  1)
        };

        public HexCoordinate(int q, int r)
        {
            Q = q;
            R = r;
        }

        // ── Offset (satır/sütun) ↔ axial dönüşümü ────────────────────────────
        // Tahta DİKDÖRTGEN çizilir: <see cref="HexGridManager.GenerateGrid"/> hücreleri
        // "odd-r offset" düzeninde kurar (tek satırlar yarım karo sağa kayar) ve axial'e çevirir.
        // Üretici/algoritmalar ise (sütun, satır) indisli DİZİ ile çalışır. İki uzay AYNI DEĞİL:
        // satır r'de axial Q = col - (r >> 1). Dönüşüm atlanırsa üretilen harita tahtaya kayık
        // oturur (sol altta boş kama, sağdaki üretim çöpe gider) — 2026-08-05'te bulunan hata.
        /// <summary>(sütun, satır) offset indisinden axial koordinat.</summary>
        public static HexCoordinate FromOffset(int col, int row) => new(col - (row >> 1), row);

        /// <summary>Axial koordinattan (sütun, satır) offset indisi.</summary>
        public void ToOffset(out int col, out int row)
        {
            row = R;
            col = Q + (R >> 1);
        }

        // Axial koordinatı dünya pozisyonuna çevirir (pointy-top, XZ düzlemi)
        public Vector3 ToWorldPosition(float hexSize)
        {
            float x = hexSize * (Mathf.Sqrt(3f) * Q + Mathf.Sqrt(3f) / 2f * R);
            float z = hexSize * (1.5f * R);
            return new Vector3(x, 0f, z);
        }

        // İki hex arasındaki adım mesafesi
        public int DistanceTo(HexCoordinate other)
        {
            return (Mathf.Abs(Q - other.Q)
                  + Mathf.Abs(R - other.R)
                  + Mathf.Abs(S - other.S)) / 2;
        }

        public HexCoordinate GetNeighbor(int directionIndex)
        {
            HexCoordinate dir = Directions[directionIndex % 6];
            return new HexCoordinate(Q + dir.Q, R + dir.R);
        }

        /// <summary>
        /// İki hex arasındaki DÜZ HAT (iki uç dahil) — görüş hattı kontrolü bunu kullanır.
        /// Cube-lerp + yuvarlama (redblobgames "line drawing"). Uçlara küçük bir epsilon
        /// eklenir: tam kenara denk gelen hatlarda yuvarlama iki komşu arasında yalpalar ve
        /// aynı iki birim için görüş bir tıkta açılıp kapanırdı.
        /// Çağıran listeyi tekrar kullanır → savaşta çöp üretmez.
        /// </summary>
        public void LineTo(HexCoordinate to, System.Collections.Generic.List<HexCoordinate> buffer)
        {
            buffer.Clear();
            int n = DistanceTo(to);
            if (n == 0) { buffer.Add(this); return; }

            const float Eps = 1e-4f;
            float aq = Q + Eps,     ar = R + Eps,     as_ = S - 2f * Eps;
            float bq = to.Q + Eps,  br = to.R + Eps,  bs  = to.S - 2f * Eps;

            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                buffer.Add(CubeRound(Mathf.Lerp(aq, bq, t), Mathf.Lerp(ar, br, t), Mathf.Lerp(as_, bs, t)));
            }
        }

        private static HexCoordinate CubeRound(float q, float r, float s)
        {
            int rq = Mathf.RoundToInt(q), rr = Mathf.RoundToInt(r), rs = Mathf.RoundToInt(s);
            float dq = Mathf.Abs(rq - q), dr = Mathf.Abs(rr - r), ds = Mathf.Abs(rs - s);
            if (dq > dr && dq > ds)  rq = -rr - rs;
            else if (dr > ds)        rr = -rq - rs;
            return new HexCoordinate(rq, rr);
        }

        public bool Equals(HexCoordinate other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoordinate h && Equals(h);
        public override int GetHashCode() => Q * 397 ^ R;
        public static bool operator ==(HexCoordinate a, HexCoordinate b) => a.Equals(b);
        public static bool operator !=(HexCoordinate a, HexCoordinate b) => !a.Equals(b);
        public override string ToString() => $"Hex({Q},{R})";
    }
}
