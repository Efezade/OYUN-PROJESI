using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// YIRTIK KORU · GÖZCÜ KUZGUN (2026-10-10). Hikâye §9: "Harita gözcüsü; yakalanırsan av
    /// birlikleri gelir."
    ///
    /// KURAL:
    ///   • Bölgede birkaç kuzgun tüner; her gün başka dala konar. Görüş halkası yerde görünür.
    ///   • Oyuncu bir kuzgunun halkasına adım atarsa GÖRÜLÜR: kuzgun havalanır, biraz geriden bir
    ///     AV BİRLİĞİ çıkar ve oyuncuyu kovalar.
    ///   • Av birliği oyuncunun harcadığı HER AP ile ilerler ve oyuncudan biraz hızlıdır. Kaçarken
    ///     durup öz toplamak, düğüme girmek onlara mesafe kazandırır.
    ///   • Yetişirse PUSU savaşı açılır. Halka Köyü'ne (güvenli bölge) varınca ya da gün dönünce
    ///     iz kaybeder.
    /// Gece görüş daraldığı için kuzgunu geç görürsün: gece burada yürümek tehlikelidir.
    /// </summary>
    public class RavenWatch : RegionMechanicBase
    {
        [Header("Kuzgunlar")]
        [SerializeField, Min(0)] private int _ravenCount  = 3;
        [Tooltip("Görüş yarıçapı (karo). Oyuncu bu halkaya adım atarsa görülür.")]
        [SerializeField, Min(1)] private int _watchRadius = 2;
        [SerializeField] private GameObject _ravenPrefab;
        [SerializeField] private Color _ravenColor = new(0.10f, 0.08f, 0.14f);
        [SerializeField] private Color _ringColor  = new(0.55f, 0.25f, 0.85f, 0.85f);

        [Header("Av birliği")]
        [Tooltip("Görülünce açılan pusu savaşı (MissionData, IsAmbush).")]
        [SerializeField] private MissionData _huntAmbush;
        [Tooltip("Oyuncunun harcadığı 1 AP başına kaç karo ilerler (oyuncu 1 karo = 1 AP).")]
        [SerializeField, Min(0.1f)] private float _huntSpeedPerAP = 1.25f;
        [Tooltip("Av birliği oyuncudan kaç karo geride doğar.")]
        [SerializeField, Min(2)] private int _huntStartDistance = 5;
        [Tooltip("Bu bölgeye giren oyuncunun izi kaybolur (Halka Köyü).")]
        [SerializeField] private RegionSO _safeRegion;
        [SerializeField] private AmbushLauncher _ambush;
        [SerializeField] private GameObject _hunterPrefab;
        [SerializeField] private Color _hunterColor = new(0.55f, 0.12f, 0.18f);

        private sealed class Raven { public HexCoordinate at; public GameObject go; public LineRenderer ring; }
        private readonly List<Raven> _ravens = new();

        private bool          _hunting;
        private HexCoordinate _hunterAt;
        private float         _huntProgress;
        private GameObject    _hunterGo;
        private int           _lastAP = -1;
        private Material      _ringMat;

        private static readonly Color Warn = new(1f, 0.55f, 0.35f);

        public bool IsHunting => _hunting;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_ap != null) _ap.OnAPChanged += HandleAPChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_ap != null) _ap.OnAPChanged -= HandleAPChanged;
        }

        protected override void OnMapReady()
        {
            _ravens.Clear();
            EndHunt(null);
            _lastAP = _ap != null ? _ap.CurrentAP : -1;
            if (Active) PlaceRavens();
        }

        protected override void OnNewDay(int day)
        {
            if (_hunting) EndHunt("Gün döndü — av birliği izini kaybetti.");
            PlaceRavens();                                      // kuzgunlar başka dallara konar
        }

        protected override void OnOverworldRestored()
        {
            _lastAP = _ap != null ? _ap.CurrentAP : -1;        // savaş AP'si kovalamaya sayılmasın
            RefreshVisibility();
        }

        protected override void OnPlayerMoved(HexCoordinate at)
        {
            if (_hunting)
            {
                if (IsSafe(at)) { EndHunt("Halka Köyü'ne vardın — av birliği köyün sınırında durdu."); return; }
                if (_hunterAt.DistanceTo(at) <= 1) { Catch(); return; }
            }
            else
            {
                foreach (var r in _ravens)
                    if (r.at.DistanceTo(at) <= _watchRadius) { Spotted(r, at); break; }
            }
            RefreshVisibility();
        }

        // ── Kuzgunlar ────────────────────────────────────────────────────────

        private void PlaceRavens()
        {
            foreach (var r in _ravens) if (r.go != null) Destroy(r.go);
            _ravens.Clear();
            if (!Active) return;

            HexCoordinate player = _player != null ? _player.CurrentCoord : default;
            var pool = RegionCells(c => IsFreeWalkable(c) && c.DistanceTo(player) > _watchRadius + 1);
            foreach (var c in PickSpaced(pool, _ravenCount, _watchRadius * 2 + 1, Rng(11)))
            {
                var r = new Raven { at = c };
                r.go = MakeMarker(_ravenPrefab, PrimitiveType.Sphere, _ravenColor, new Vector3(0.28f, 0.22f, 0.36f));
                r.go.name = $"Kuzgun_{c}";
                r.go.transform.position = SurfaceOf(c) + Vector3.up * 0.55f;
                r.ring = MakeRing(r.go.transform, SurfaceOf(c), _watchRadius);
                _ravens.Add(r);
            }
            RefreshVisibility();
        }

        private LineRenderer MakeRing(Transform parent, Vector3 center, int radius)
        {
            var go = new GameObject("GorusHalkasi");
            go.transform.SetParent(parent, true);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true; lr.loop = true;
            const int n = 40;
            lr.positionCount = n;
            float rad = (radius + 0.5f) * Mathf.Sqrt(3f) * (_grid != null ? _grid.HexSize : 1f);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * rad, 0.12f, Mathf.Sin(a) * rad));
            }
            lr.widthMultiplier = 0.06f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (_ringMat == null)
                _ringMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            lr.sharedMaterial = _ringMat;
            lr.startColor = lr.endColor = _ringColor;
            if (_ringMat.HasProperty("_BaseColor")) _ringMat.SetColor("_BaseColor", _ringColor);
            return lr;
        }

        /// <summary>Kuzgun yalnız şu an GÖRÜLEN karodaysa çizilir (sisin içindekini bilemezsin).</summary>
        private void RefreshVisibility()
        {
            foreach (var r in _ravens)
                if (r.go != null) r.go.SetActive(_fog == null || _fog.IsVisible(r.at));
        }

        // ── Kovalamaca ───────────────────────────────────────────────────────

        private void Spotted(Raven r, HexCoordinate player)
        {
            if (r.go != null) Destroy(r.go);
            _ravens.Remove(r);

            if (!TryFindHuntStart(player, out HexCoordinate start))
            {
                Notify("Gözcü Kuzgun seni gördü ama av birliği yolunu bulamadı.", Warn);
                return;
            }

            _hunting      = true;
            _hunterAt     = start;
            _huntProgress = 0f;
            _hunterGo     = MakeMarker(_hunterPrefab, PrimitiveType.Capsule, _hunterColor, new Vector3(0.35f, 0.45f, 0.35f),
                                       1.4f, 2.5f);
            _hunterGo.name = "AvBirligi";
            PlaceHunter();

            if (_player != null) _player.RequestStop("Gözcü Kuzgun seni gördü");
            string safe = _safeRegion != null ? _safeRegion.DisplayName : "güvenli bir yere";
            Notify($"GÖZCÜ KUZGUN SENİ GÖRDÜ! Av birliği peşinde — {safe} kaç.", Warn, 5f);
        }

        /// <summary>Av birliğinin doğacağı yer: oyuncudan ~N karo uzakta, yürünür, oyuncuya
        /// yürüyerek ulaşabilen karo; güvenli bölgeden uzak taraf tercih edilir.</summary>
        private bool TryFindHuntStart(HexCoordinate player, out HexCoordinate start)
        {
            start = default;
            if (_grid == null || !_grid.TryGetCell(player, out HexCell from)) return false;

            // Oyuncudan yürüyerek BFS → tam N karo uzaktaki halkadan seç.
            var dist = WalkDistances(player, _huntStartDistance + 2);
            var ring = new List<HexCoordinate>();
            foreach (var kv in dist) if (kv.Value == _huntStartDistance) ring.Add(kv.Key);
            if (ring.Count == 0) foreach (var kv in dist) if (kv.Value >= 2) ring.Add(kv.Key);
            if (ring.Count == 0) return false;

            var rnd = Rng(23);
            start = ring[rnd.Next(ring.Count)];
            return true;
        }

        private Dictionary<HexCoordinate, int> WalkDistances(HexCoordinate from, int limit)
        {
            var d = new Dictionary<HexCoordinate, int> { [from] = 0 };
            var q = new Queue<HexCoordinate>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (d[c] >= limit) continue;
                for (int i = 0; i < 6; i++)
                {
                    var n = c.GetNeighbor(i);
                    if (d.ContainsKey(n)) continue;
                    if (!_grid.TryGetCell(n, out HexCell cell) || !cell.IsWalkable) continue;
                    if (IsSafe(n)) continue;                    // av birliği köye girmez
                    d[n] = d[c] + 1;
                    q.Enqueue(n);
                }
            }
            return d;
        }

        private void HandleAPChanged(int current, int max)
        {
            int prev = _lastAP;
            _lastAP = current;
            if (!_hunting || !InOverworld || prev < 0) return;

            // Harcanan AP: dilim dönmediyse fark; döndüyse (AP doldu) bir dilimlik ekle.
            int spent = current < prev ? prev - current : prev - current + Mathf.Max(1, max);
            if (spent <= 0) return;

            _huntProgress += spent * _huntSpeedPerAP;
            while (_huntProgress >= 1f && _hunting)
            {
                _huntProgress -= 1f;
                StepHunter();
            }
        }

        private void StepHunter()
        {
            if (_player == null) return;
            HexCoordinate target = _player.CurrentCoord;
            if (IsSafe(target)) { EndHunt("Halka Köyü'ne vardın — av birliği köyün sınırında durdu."); return; }

            // Oyuncuya doğru bir adım: oyuncudan geriye BFS, en kısa yoldaki komşu.
            var fromPlayer = WalkDistances(target, 40);
            HexCoordinate best = _hunterAt; int bestD = fromPlayer.TryGetValue(_hunterAt, out int cur) ? cur : int.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                var n = _hunterAt.GetNeighbor(i);
                if (fromPlayer.TryGetValue(n, out int d) && d < bestD) { bestD = d; best = n; }
            }
            if (bestD == int.MaxValue) { EndHunt("Av birliği yolunu kaybetti."); return; }

            _hunterAt = best;
            PlaceHunter();
            if (_hunterAt.DistanceTo(target) <= 1) Catch();
        }

        private void PlaceHunter()
        {
            if (_hunterGo != null) _hunterGo.transform.position = SurfaceOf(_hunterAt) + Vector3.up * 0.45f;
        }

        private void Catch()
        {
            EndHunt(null);
            if (_ambush == null || _huntAmbush == null)
            {
                Notify("Av birliği seni yakaladı! (pusu görevi atanmamış — savaş açılmadı)", Warn);
                return;
            }
            _ambush.Launch(_huntAmbush, "AV BİRLİĞİ SENİ YAKALADI!", Warn);
        }

        private void EndHunt(string message)
        {
            _hunting = false;
            if (_hunterGo != null) Destroy(_hunterGo);
            _hunterGo = null;
            if (!string.IsNullOrEmpty(message)) Notify(message, new Color(0.75f, 0.9f, 0.75f));
        }

        private bool IsSafe(HexCoordinate c) => _safeRegion != null && _map != null && _map.RegionAt(c) == _safeRegion;
    }
}
