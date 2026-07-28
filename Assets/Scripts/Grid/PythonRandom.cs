using System;
using System.Collections.Generic;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// **CPython'ın `random.Random`'ının birebir kopyası** (MT19937 + CPython'ın çekim algoritmaları).
    ///
    /// NEDEN VAR: bölüm 1 haritasının 10 sabit seed'i, denge tarafında Python ile üretilip ELLE
    /// doğrulandı (`Docs/Balance/tools/harita_terrain_v2.py`, `harita_seed_secimi.py`). TASK-005'in
    /// kabul kriteri "aynı seed → Python referansıyla AYNI terrain". Unity'nin `UnityEngine.Random`'ı
    /// (xorshift) ya da `System.Random` bunu üretemez — aynı sayı dizisini vermeleri imkânsız.
    /// Bu yüzden RNG'nin kendisi taşındı; böylece seed 89 Unity'de de Python'daki haritayı verir.
    ///
    /// UnityEngine'e BAĞIMLI DEĞİLDİR (bilerek) — hem oyunda hem de Unity'siz bir doğrulama
    /// koşumunda aynı kod çalışsın diye. Oyun mantığı için normal rastgelelik gerekiyorsa bunu
    /// KULLANMA; bu sınıf yalnız Python referansıyla eşleşmesi gereken üretim içindir.
    ///
    /// Kapsam: yalnız `harita_terrain_v2.py`'nin kullandığı üyeler taşındı —
    /// <see cref="Random"/>, <see cref="RandInt"/>, <see cref="RandRange"/>, <see cref="Choice{T}"/>,
    /// <see cref="Shuffle{T}"/>, <see cref="Sample{T}"/>, <see cref="Choices"/>.
    /// </summary>
    public sealed class PythonRandom
    {
        // ── MT19937 ──────────────────────────────────────────────────────────
        private const int  N = 624, M = 397;
        private const uint MATRIX_A = 0x9908b0dfu, UPPER_MASK = 0x80000000u, LOWER_MASK = 0x7fffffffu;

        private readonly uint[] _mt = new uint[N];
        private int _mti = N + 1;

        /// <summary>CPython `random.Random(seed)` ile aynı tohumlama: tamsayı seed'in MUTLAK değeri
        /// 32-bitlik parçalara (little-endian) bölünüp `init_by_array`'e verilir.</summary>
        public PythonRandom(long seed)
        {
            ulong n = seed < 0 ? (ulong)(-seed) : (ulong)seed;
            var key = new List<uint>();
            if (n == 0) key.Add(0u);
            while (n > 0) { key.Add((uint)(n & 0xffffffffu)); n >>= 32; }
            InitByArray(key.ToArray());
        }

        private void InitGenrand(uint s)
        {
            _mt[0] = s;
            for (_mti = 1; _mti < N; _mti++)
                _mt[_mti] = 1812433253u * (_mt[_mti - 1] ^ (_mt[_mti - 1] >> 30)) + (uint)_mti;
        }

        private void InitByArray(uint[] initKey)
        {
            InitGenrand(19650218u);
            int i = 1, j = 0;
            int k = Math.Max(N, initKey.Length);
            for (; k > 0; k--)
            {
                _mt[i] = (_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1664525u)) + initKey[j] + (uint)j;
                i++; j++;
                if (i >= N) { _mt[0] = _mt[N - 1]; i = 1; }
                if (j >= initKey.Length) j = 0;
            }
            for (k = N - 1; k > 0; k--)
            {
                _mt[i] = (_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1566083941u)) - (uint)i;
                i++;
                if (i >= N) { _mt[0] = _mt[N - 1]; i = 1; }
            }
            _mt[0] = 0x80000000u;
        }

        private uint GenrandUInt32()
        {
            uint y;
            if (_mti >= N)
            {
                int kk;
                if (_mti == N + 1) InitGenrand(5489u);
                for (kk = 0; kk < N - M; kk++)
                {
                    y = (_mt[kk] & UPPER_MASK) | (_mt[kk + 1] & LOWER_MASK);
                    _mt[kk] = _mt[kk + M] ^ (y >> 1) ^ ((y & 1u) != 0u ? MATRIX_A : 0u);
                }
                for (; kk < N - 1; kk++)
                {
                    y = (_mt[kk] & UPPER_MASK) | (_mt[kk + 1] & LOWER_MASK);
                    _mt[kk] = _mt[kk + (M - N)] ^ (y >> 1) ^ ((y & 1u) != 0u ? MATRIX_A : 0u);
                }
                y = (_mt[N - 1] & UPPER_MASK) | (_mt[0] & LOWER_MASK);
                _mt[N - 1] = _mt[M - 1] ^ (y >> 1) ^ ((y & 1u) != 0u ? MATRIX_A : 0u);
                _mti = 0;
            }

            y = _mt[_mti++];
            y ^= y >> 11;
            y ^= (y << 7)  & 0x9d2c5680u;
            y ^= (y << 15) & 0xefc60000u;
            y ^= y >> 18;
            return y;
        }

        // ── CPython random API (kullanılan alt küme) ─────────────────────────

        /// <summary>CPython `random()`: iki 32-bit çekimden 53-bit çözünürlüklü [0,1) double.</summary>
        public double Random()
        {
            uint a = GenrandUInt32() >> 5, b = GenrandUInt32() >> 6;
            return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);
        }

        /// <summary>CPython `getrandbits(k)`, k ≤ 32 (bu portun ihtiyacı bu kadar).</summary>
        private uint GetRandBits(int k) => k <= 0 ? 0u : GenrandUInt32() >> (32 - k);

        private static int BitLength(int n)
        {
            int bits = 0;
            while (n > 0) { bits++; n >>= 1; }
            return bits;
        }

        /// <summary>CPython `Random._randbelow`: k-bit çek, n'den küçük olana kadar REDDET.
        /// Modulo kullanmaz — sayı dizisinin Python'la aynı ilerlemesi buna bağlı.</summary>
        private int RandBelow(int n)
        {
            if (n <= 0) return 0;
            int k = BitLength(n);
            uint r = GetRandBits(k);
            while (r >= (uint)n) r = GetRandBits(k);
            return (int)r;
        }

        /// <summary>CPython `randrange(stop)`.</summary>
        public int RandRange(int stop) => RandBelow(stop);

        /// <summary>CPython `randint(a, b)` — b DAHİL.</summary>
        public int RandInt(int a, int b) => a + RandBelow(b - a + 1);

        /// <summary>CPython `choice(seq)`.</summary>
        public T Choice<T>(IReadOnlyList<T> seq) => seq[RandBelow(seq.Count)];

        /// <summary>CPython `shuffle(x)` — yerinde, sondan başa Fisher-Yates.</summary>
        public void Shuffle<T>(IList<T> x)
        {
            for (int i = x.Count - 1; i >= 1; i--)
            {
                int j = RandBelow(i + 1);
                (x[i], x[j]) = (x[j], x[i]);
            }
        }

        /// <summary>CPython `sample(population, k)` — iki dallı algoritma (küçük havuz: liste takası,
        /// büyük havuz: seçilenler kümesi + tekrar çekme). Dal seçimi Python'la aynı eşiği kullanır,
        /// çünkü hangi dala girildiği RNG tüketimini değiştirir.</summary>
        public List<T> Sample<T>(IReadOnlyList<T> population, int k)
        {
            int n = population.Count;
            var result = new List<T>(k);

            int setsize = 21;
            if (k > 5) setsize += (int)Math.Pow(4, Math.Ceiling(Math.Log(k * 3.0, 4)));

            if (n <= setsize)
            {
                var pool = new List<T>(population);
                for (int i = 0; i < k; i++)
                {
                    int j = RandBelow(n - i);
                    result.Add(pool[j]);
                    pool[j] = pool[n - i - 1];
                }
            }
            else
            {
                var selected = new HashSet<int>();
                for (int i = 0; i < k; i++)
                {
                    int j = RandBelow(n);
                    while (selected.Contains(j)) j = RandBelow(n);
                    selected.Add(j);
                    result.Add(population[j]);
                }
            }
            return result;
        }

        /// <summary>CPython `choices(population, weights, k=1)`'in tek çekimlik hâli — kümülatif
        /// ağırlıklar üzerinde `bisect_right(cum, random()*total, 0, n-1)`. Seçilen İNDİS'i döndürür.</summary>
        public int ChoicesIndex(double[] cumulativeWeights)
        {
            int n = cumulativeWeights.Length;
            double total = cumulativeWeights[n - 1] + 0.0;
            double x = Random() * total;
            return BisectRight(cumulativeWeights, x, 0, n - 1);
        }

        /// <summary>Ağırlık listesinden CPython `itertools.accumulate` ile aynı kümülatif diziyi kurar
        /// (soldan sağa double toplama — yuvarlama davranışı da aynı olsun diye).</summary>
        public static double[] Accumulate(double[] weights)
        {
            var cum = new double[weights.Length];
            double acc = 0.0;
            for (int i = 0; i < weights.Length; i++) { acc += weights[i]; cum[i] = acc; }
            return cum;
        }

        private static int BisectRight(double[] a, double x, int lo, int hi)
        {
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (x < a[mid]) hi = mid; else lo = mid + 1;
            }
            return lo;
        }
    }
}
