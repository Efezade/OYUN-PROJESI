using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// FISILTI BATAKLIĞI · ALAZ IŞIKLARI (2026-10-10). Hikâye §7: "Gece yol gösteren ışıklar;
    /// kimi hazineye, kimi tuzağa götürür."
    ///
    /// KURAL:
    ///   • Bataklıkta birkaç Alaz süzülür; her gün başka yere göçer. Işık oldukları için SİSİN
    ///     içinden de görünürler (uzaktan merak uyandırsın).
    ///   • Alazın karosuna basan oyuncu ya HAZİNE bulur (bölümün özünden birkaç tane) ya da
    ///     TUZAĞA düşer: bataklıkta yolunu kaybeder, birkaç karo öteye savrulur ve AP yitirir.
    ///   • İkisi önceden ayırt edilemez. Işık kullanılınca söner.
    /// </summary>
    public class WispLights : RegionMechanicBase
    {
        [Header("Alazlar")]
        [SerializeField, Min(0)] private int _count = 4;
        [SerializeField, Min(1)] private int _spacing = 3;
        [SerializeField] private GameObject _wispPrefab;
        [SerializeField] private Color _wispColor = new(0.75f, 0.95f, 0.55f);
        [SerializeField] private float _lightIntensity = 2.5f;
        [SerializeField] private float _bobHeight = 0.18f;

        [Header("Sonuç")]
        [SerializeField, Range(0f, 1f)] private float _treasureChance = 0.55f;
        [Tooltip("Hazine: kaç öz (min-maks).")]
        [SerializeField] private Vector2Int _treasure = new(2, 4);
        [Tooltip("Tuzak: kaç karo öteye savrulur (min-maks).")]
        [SerializeField] private Vector2Int _trapShift = new(3, 5);
        [Tooltip("Tuzak: yitirilen AP.")]
        [SerializeField, Min(0)] private int _trapAPCost = 2;
        [SerializeField] private EssenceWallet   _wallet;
        [Tooltip("Hazinenin öz türü bölümün öz listesinden seçilir.")]
        [SerializeField] private ChapterProgress _progress;

        private sealed class Wisp { public HexCoordinate at; public GameObject go; public float phase; public Vector3 basePos; }
        private readonly List<Wisp> _wisps = new();

        private static readonly Color Good = new(0.75f, 0.95f, 0.55f);
        private static readonly Color Bad  = new(1f, 0.6f, 0.4f);

        protected override void OnMapReady()
        {
            _wisps.Clear();
            if (Active) Scatter();
        }

        protected override void OnNewDay(int day) => Scatter();

        private void Scatter()
        {
            foreach (var w in _wisps) if (w.go != null) Destroy(w.go);
            _wisps.Clear();
            if (!Active) return;

            var pool = RegionCells(IsFreeWalkable);
            var rnd  = Rng(37);
            foreach (var c in PickSpaced(pool, _count, _spacing, rnd))
            {
                var w = new Wisp { at = c, phase = (float)rnd.NextDouble() * 6.28f };
                w.go = MakeMarker(_wispPrefab, PrimitiveType.Sphere, _wispColor, Vector3.one * 0.22f, _lightIntensity, 3f);
                w.go.name = $"Alaz_{c}";
                w.basePos = SurfaceOf(c) + Vector3.up * 0.7f;
                w.go.transform.position = w.basePos;
                _wisps.Add(w);
            }
        }

        // Hafif süzülme — yalnız birkaç nesne, Update'te ucuz iş.
        private void Update()
        {
            if (_wisps.Count == 0 || !InOverworld) return;
            float t = Time.time;
            foreach (var w in _wisps)
                if (w.go != null)
                    w.go.transform.position = w.basePos + Vector3.up * (Mathf.Sin(t * 1.7f + w.phase) * _bobHeight);
        }

        protected override void OnPlayerMoved(HexCoordinate at)
        {
            for (int i = 0; i < _wisps.Count; i++)
            {
                if (!_wisps[i].at.Equals(at)) continue;
                Wisp w = _wisps[i];
                _wisps.RemoveAt(i);
                if (w.go != null) Destroy(w.go);
                Resolve(at, i);
                return;
            }
        }

        private void Resolve(HexCoordinate at, int index)
        {
            var rnd = Rng(53 + index);
            if (rnd.NextDouble() < _treasureChance) GiveTreasure(rnd);
            else StartCoroutine(Trap(at, rnd));
        }

        private void GiveTreasure(System.Random rnd)
        {
            EssenceType[] kinds = _progress != null && _progress.CurrentRules != null
                ? _progress.CurrentRules.ChapterEssences : null;
            EssenceType type = kinds != null && kinds.Length > 0 ? kinds[rnd.Next(kinds.Length)] : EssenceType.Doga;
            int amount = rnd.Next(_treasure.x, Mathf.Max(_treasure.x, _treasure.y) + 1);
            if (_wallet != null) _wallet.Gain(type, amount);
            Notify($"Alaz seni gizli bir öz yatağına götürdü: +{amount} {type}", Good);
        }

        private IEnumerator Trap(HexCoordinate at, System.Random rnd)
        {
            Notify("ALAZ SENİ YANILTTI! Sisin içinde yolunu kaybettin.", Bad, 4.5f);
            if (_player != null) _player.RequestStop("Alaz seni yanılttı");

            float guard = 0f;
            while (_player != null && _player.IsMoving && guard < 3f) { guard += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.5f);
            if (!InOverworld || _player == null || _grid == null) yield break;

            // Savrulma hedefi: önce bölge içinde, yoksa her yerde; tam aralıktaki yürünür karolar.
            int lo = _trapShift.x, hi = Mathf.Max(_trapShift.x, _trapShift.y);
            var options = new List<HexCoordinate>();
            foreach (var kv in _grid.Cells)
            {
                int d = kv.Key.DistanceTo(at);
                if (d < lo || d > hi || !IsFreeWalkable(kv.Key)) continue;
                if (InRegion(kv.Key)) options.Add(kv.Key);
            }
            if (options.Count == 0)
                foreach (var kv in _grid.Cells)
                {
                    int d = kv.Key.DistanceTo(at);
                    if (d >= lo && d <= hi && IsFreeWalkable(kv.Key)) options.Add(kv.Key);
                }
            if (options.Count == 0) yield break;

            HexCoordinate to = options[rnd.Next(options.Count)];
            if (_ap != null)
            {
                _ap.GrantForcedMove();                 // savrulma adım sayılmaz…
                if (_trapAPCost > 0) _ap.SpendAP(_trapAPCost);   // …bedeli yitirilen zaman
            }
            if (_grid.TryGetCell(to, out HexCell cell)) _player.TeleportTo(cell);
        }
    }
}
