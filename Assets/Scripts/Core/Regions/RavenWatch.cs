using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Mor halkalarda hazır bekleyen goblinler. Takip merkezden başlar ve kaçışta aynı
    /// merkeze döner. Yakalanmada yaklaşma ve yüzleşmenin ardından pusu açılır.
    /// </summary>
    public class RavenWatch : RegionMechanicBase
    {
        [Header("Mor halkalarda bekleyen av birlikleri")]
        [SerializeField, Min(0)] private int _ravenCount  = 3;
        [Tooltip("Görüş yarıçapı (karo). Oyuncu bu halkaya adım atarsa görülür.")]
        [SerializeField, Min(1)] private int _watchRadius = 2;
        [SerializeField] private Color _ringColor  = new(0.55f, 0.25f, 0.85f, 0.85f);

        [Header("Av birliği")]
        [Tooltip("Görülünce açılan pusu savaşı (MissionData, IsAmbush).")]
        [SerializeField] private MissionData _huntAmbush;
        [Tooltip("Oyuncunun harcadığı 1 AP başına kaç karo ilerler (oyuncu 1 karo = 1 AP).")]
        [SerializeField, Min(0.1f)] private float _huntSpeedPerAP = 1.25f;
        [Tooltip("Bu bölgeye giren oyuncunun izi kaybolur (Halka Köyü).")]
        [SerializeField] private RegionSO _safeRegion;
        [SerializeField] private AmbushLauncher _ambush;
        [SerializeField] private GameObject _hunterPrefab;

        [Header("Av birliği modeli ve yürüyüşü")]
        [Tooltip("Savaşta kullanılan goblin sınıfı: aynı model, boy ve Animator kullanılır.")]
        [SerializeField] private CharacterClassData _hunterClass;
        [SerializeField, Min(0.1f)] private float _hunterMoveSpeed = 3.5f;

        [Header("Kaçış fırsatı")]
        [Tooltip("İlk fark edişten sonra takip ve yakalanma başlamadan önce verilen süre (sn).")]
        [SerializeField, Min(0f)] private float _reactionDelay = 1.2f;
        [Tooltip("Takip yürüyüşü ve AP başına ilerleme çarpanı. Dönüş hızını etkilemez.")]
        [SerializeField, Range(0.1f, 1f)] private float _pursuitSpeedMultiplier = 0.65f;
        [Tooltip("Kırmızı yakalanma halkasını küçültür; goblinin daha çok yaklaşması gerekir.")]
        [SerializeField, Range(0.1f, 1f)] private float _captureRadiusMultiplier = 0.6f;

        [Header("İç içe takip halkaları (karo mesafesi)")]
        [SerializeField, Min(0.1f)] private float _battleRadius = 1f;
        [SerializeField, Min(0.1f)] private float _chaseRadius = 3f;
        [SerializeField, Min(0.1f)] private float _escapeRadius = 6f;
        [SerializeField] private Color _battleRingColor = new(1f, 0.25f, 0.18f, 0.95f);
        [SerializeField] private Color _chaseRingColor = new(1f, 0.7f, 0.15f, 0.85f);
        [SerializeField] private Color _escapeRingColor = new(0.3f, 0.85f, 0.55f, 0.7f);
        [SerializeField, Range(24, 128)] private int _huntRingSegments = 64;
        [SerializeField, Min(0.01f)] private float _huntRingWidth = 0.04f;
        [SerializeField] private float _huntRingHeight = 0.08f;

        private sealed class Raven
        {
            public HexCoordinate at;
            public GameObject go;
            public GameObject guard;
            public CharacterAnimationDriver animation;
            public LineRenderer ring;
            public LineRenderer[] huntRings;
            public bool spent;
        }
        private readonly List<Raven> _ravens = new();

        private bool          _hunting;
        private bool          _returning;
        private bool          _catching;
        private Raven         _activeRaven;
        private HexCoordinate _hunterAt;
        private HexCoordinate _hunterHome;
        private float         _huntProgress;
        private float         _alertUntil;
        private GameObject    _hunterGo;
        private Coroutine     _hunterMotion;
        private Material      _ringMat;
        private Material      _huntRingMat;

        private static readonly Color Warn = new(1f, 0.55f, 0.35f);

        public bool IsHunting => _hunting;
        public bool IsReturning => _returning;

        private void OnDestroy()
        {
            if (_ringMat != null) Destroy(_ringMat);
            if (_huntRingMat != null) Destroy(_huntRingMat);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_ap != null) _ap.OnAPSpent += HandleAPSpent;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_ap != null) _ap.OnAPSpent -= HandleAPSpent;
            ClearHunter();
        }

        protected override void OnMapReady()
        {
            _ravens.Clear();
            ClearHunter();
            if (Active) PlaceRavens();
        }

        protected override void OnNewDay(int day)
        {
            // Merkezler sabit kalır; gün değişince takip edilen goblinin evi taşınmaz.
            if (_hunting) ReturnHome("Gün döndü — av birliği izini kaybetti ve geri dönüyor.");
            foreach (var r in _ravens) r.spent = false;
            RefreshVisibility();
        }

        protected override void OnOverworldRestored() => RefreshVisibility();

        protected override void OnPlayerMoved(HexCoordinate at)
        {
            if (_hunting) CheckHuntRange();
            else CheckWatchRanges();
            RefreshVisibility();
        }

        private void CheckWatchRanges()
        {
            if (_catching || _returning || _hunting || _player == null || IsSafe(_player.CurrentCoord)) return;
            if (_ambush != null && _ambush.IsBusy) return;
            float radius = WatchRadius;
            foreach (var r in _ravens)
            {
                if (r.spent || r.guard == null || r.go == null) continue;
                Vector3 delta = _player.transform.position - r.go.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radius * radius) { Spotted(r); break; }
            }
        }

        private void PlaceRavens()
        {
            foreach (var r in _ravens)
            {
                if (r.go != null) Destroy(r.go);
                if (r.guard != null) Destroy(r.guard);
            }
            _ravens.Clear();
            if (!Active) return;
            HexCoordinate player = _player != null ? _player.CurrentCoord : default;
            var pool = RegionCells(c => IsFreeWalkable(c) && c.DistanceTo(player) > _watchRadius + 1);
            foreach (var c in PickSpaced(pool, _ravenCount, _watchRadius * 2 + 1, Rng(11)))
            {
                var r = new Raven { at = c, go = new GameObject($"AvBirligiMerkezi_{c}") };
                r.go.transform.SetParent(MarkerRoot, false);
                r.go.transform.position = SurfaceOf(c);
                r.ring = MakeRing(r.go.transform, SurfaceOf(c), _watchRadius);
                r.guard = CreateHunter(c);
                r.animation = r.guard.GetComponent<CharacterAnimationDriver>();
                r.huntRings = r.guard.GetComponentsInChildren<LineRenderer>(true);
                SetHuntRings(r.huntRings, false);
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

        private void RefreshVisibility()
        {
            foreach (var r in _ravens)
            {
                bool visible = !r.spent && (_fog == null || _fog.IsVisible(r.at));
                if (r.go != null) r.go.SetActive(visible);
                if (r.guard != null) r.guard.SetActive(r == _activeRaven || visible);
            }
        }

        private void Spotted(Raven r)
        {
            _activeRaven = r;
            _hunterGo = r.guard;
            _hunterGo.SetActive(true);
            _hunterAt = r.at;
            _hunterHome = r.at;
            _hunting = true;
            _huntProgress = 1f;
            _alertUntil = Time.time + _reactionDelay;
            SetHuntRings(r.huntRings, true);
            // Fark edilmek Kam'ın kaçış rotasını kesmez; ilk tepki süresinde kaçabilir.
            Notify("AV BİRLİĞİ SENİ GÖRDÜ! Kırmızı halka: yakalanma. Yeşil halkanın dışına kaç.", Warn, 5f);
            CheckHuntRange();
            if (_hunting) StartHunterMotion();
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

        private void HandleAPSpent(int spent)
        {
            if (!_hunting || !InOverworld || spent <= 0) return;
            CheckHuntRange();
            if (!_hunting) return;
            _huntProgress += spent * _huntSpeedPerAP * _pursuitSpeedMultiplier;
            StartHunterMotion();
        }

        private float TileDistance => Mathf.Sqrt(3f) * (_grid != null ? _grid.HexSize : 1f);
        private float WatchRadius => (_watchRadius + 0.5f) * TileDistance;
        private float BattleRadius => _battleRadius * _captureRadiusMultiplier * TileDistance;
        private float ChaseRadius => Mathf.Max(_battleRadius, _chaseRadius) * TileDistance;
        private float EscapeRadius => Mathf.Max(_battleRadius, Mathf.Max(_chaseRadius, _escapeRadius)) * TileDistance;

        private float HunterDistance()
        {
            if (_hunterGo == null || _player == null) return float.PositiveInfinity;
            Vector3 delta = _hunterGo.transform.position - _player.transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        private void Update()
        {
            // Ucuz mesafe kontrolü; yol hesabı yürüyüş coroutine'inde, karo sınırında yapılır.
            if (!InOverworld) return;
            if (_hunting && _hunterGo != null) CheckHuntRange();
            else CheckWatchRanges();
        }

        private void CheckHuntRange()
        {
            if (!_hunting || _player == null || _hunterGo == null) return;
            if (IsSafe(_player.CurrentCoord))
            { ReturnHome("Halka Köyü'ne vardın — av birliği geri dönüyor."); return; }
            float distance = HunterDistance();
            if (distance > EscapeRadius)
            { ReturnHome("Dış halkanın dışına çıktın — av birliği izini kaybetti ve geri dönüyor."); return; }
            if (Time.time >= _alertUntil && distance <= BattleRadius) Catch();
        }

        private void StartHunterMotion()
        {
            if (_hunterMotion == null && _hunterGo != null) _hunterMotion = StartCoroutine(HunterMotion());
        }

        private IEnumerator HunterMotion()
        {
            // Coroutine handle'ı atanmadan aynı karede bitmesin; Update'te yol hesabı yapılmasın.
            yield return null;
            while (_hunterGo != null && (_returning || (_hunting && _huntProgress >= 1f)))
            {
                if (!InOverworld) { yield return null; continue; }
                if (_hunting && Time.time < _alertUntil) { yield return null; continue; }
                if (_returning && _hunterAt.Equals(_hunterHome))
                {
                    _hunterGo.transform.position = SurfaceOf(_hunterHome);
                    SetHuntRings(_activeRaven?.huntRings, false);
                    _returning = false;
                    _activeRaven = null;
                    _hunterGo = null;
                    RefreshVisibility();
                    break;
                }
                HexCoordinate destination = _returning ? _hunterHome : _player.CurrentCoord;
                if (!TryHunterStep(destination, out HexCoordinate next))
                {
                    _hunting = false; _returning = false; _huntProgress = 0f;
                    Notify("Av birliğinin yolu kapandı; takip durdu.", _escapeRingColor);
                    break;
                }
                if (_hunting) _huntProgress -= 1f;
                Vector3 to = SurfaceOf(next);
                while (_hunterGo != null && (_hunterGo.transform.position - to).sqrMagnitude > 0.0001f)
                {
                    if (!InOverworld) { yield return null; continue; }
                    _hunterGo.transform.position = Vector3.MoveTowards(_hunterGo.transform.position, to,
                        Mathf.Max(0.1f, _hunterMoveSpeed * (_returning ? 1f : _pursuitSpeedMultiplier)) * Time.deltaTime);
                    yield return null;
                }
                if (_hunterGo == null) break;
                _hunterGo.transform.position = to;
                _hunterAt = next;
                if (_hunting) CheckHuntRange();
            }
            _hunterMotion = null;
        }

        private bool TryHunterStep(HexCoordinate target, out HexCoordinate best)
        {
            best = _hunterAt;
            if (_grid == null || !_grid.TryGetCell(target, out HexCell targetCell) || !targetCell.IsWalkable) return false;

            // Oyuncuya doğru bir adım: oyuncudan geriye BFS, en kısa yoldaki komşu.
            var fromPlayer = WalkDistances(target, _grid.Cells.Count);
            int bestD = fromPlayer.TryGetValue(_hunterAt, out int cur) ? cur : int.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                var n = _hunterAt.GetNeighbor(i);
                if (fromPlayer.TryGetValue(n, out int d) && d < bestD) { bestD = d; best = n; }
            }
            return bestD != int.MaxValue && !best.Equals(_hunterAt);
        }

        private GameObject CreateHunter(HexCoordinate at)
        {
            GameObject hunter = _hunterPrefab != null ? Instantiate(_hunterPrefab, MarkerRoot) : new GameObject("AvBirligi");
            hunter.transform.SetParent(MarkerRoot, false);
            hunter.name = "AvBirligi";
            hunter.transform.position = SurfaceOf(at);

            CharacterClassData data = _hunterClass;
            // Önceki sahneler kurulum çalıştırılmadan da mevcut pusu roster'ındaki goblini kullanır.
            if (data == null && _huntAmbush != null)
                foreach (var entry in _huntAmbush.EnemyRoster)
                    if (entry.enemyClass != null && entry.enemyClass.UnitModel != null) { data = entry.enemyClass; break; }
            if (_hunterPrefab == null && data != null && data.UnitModel != null)
            {
                hunter.AddComponent<CharacterModelBinder>().Apply(data.UnitModel, data.UnitModelHeight,
                    data.UnitModelEuler, data.UnitModelYOffset, true, data.UnitAnimator);
            }
            if (hunter.GetComponentInChildren<Animator>(true) != null && hunter.GetComponent<CharacterAnimationDriver>() == null)
                hunter.AddComponent<CharacterAnimationDriver>();

            MakeHuntRing(hunter.transform, "SavasHalkasi", BattleRadius, _battleRingColor);
            MakeHuntRing(hunter.transform, "TakipHalkasi", ChaseRadius, _chaseRingColor);
            MakeHuntRing(hunter.transform, "IzKaybetmeHalkasi", EscapeRadius, _escapeRingColor);
            return hunter;
        }

        private void MakeHuntRing(Transform parent, string name, float radius, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = false; ring.loop = true;
            ring.positionCount = Mathf.Max(24, _huntRingSegments);
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i / (float)ring.positionCount * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, _huntRingHeight, Mathf.Sin(angle) * radius));
            }
            ring.widthMultiplier = _huntRingWidth;
            ring.startColor = ring.endColor = color;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            if (_huntRingMat == null)
                _huntRingMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            ring.sharedMaterial = _huntRingMat;
        }

        private static void SetHuntRings(LineRenderer[] rings, bool visible)
        {
            if (rings == null) return;
            foreach (var ring in rings) if (ring != null) ring.enabled = visible;
        }

        private void Catch()
        {
            if (_ambush == null || _huntAmbush == null || _ambush.IsBusy)
            {
                ReturnHome("Av birliği geri dönüyor; pusu şu anda açılamıyor.");
                return;
            }
            _hunting = false;
            _catching = true;
            _huntProgress = 0f;
            if (_hunterMotion != null) StopCoroutine(_hunterMotion);
            _hunterMotion = null;
            SetHuntRings(_activeRaven?.huntRings, false);
            if (!_ambush.LaunchEncounter(_huntAmbush, "AV BİRLİĞİ SENİ YAKALADI!", Warn,
                    _hunterGo, _activeRaven != null ? _activeRaven.animation : null, EncounterFinished))
            {
                _catching = false;
                ReturnHome(null);
            }
        }

        private void EncounterFinished(bool enteredBattle)
        {
            _catching = false;
            if (enteredBattle)
            {
                // Savaştan aynı noktaya çıkınca ikinci pusu açılmasın. Ertesi gün yeniden hazır.
                if (_activeRaven != null) _activeRaven.spent = true;
                ClearHunter();
                RefreshVisibility();
            }
            else if (isActiveAndEnabled && _hunterGo != null) ReturnHome(null);
            else ClearHunter();
        }

        private void ReturnHome(string message)
        {
            _hunting = false;
            _returning = _hunterGo != null;
            _huntProgress = 0f;
            if (!string.IsNullOrEmpty(message)) Notify(message, _escapeRingColor);
            StartHunterMotion();
        }

        private void ClearHunter()
        {
            _hunting = false;
            _returning = false;
            _catching = false;
            _huntProgress = 0f;
            if (_hunterMotion != null) StopCoroutine(_hunterMotion);
            _hunterMotion = null;
            if (_hunterGo != null)
            {
                if (_grid != null && _grid.TryGetCell(_hunterHome, out HexCell cell))
                    _hunterGo.transform.position = cell.WorldPosition + Vector3.up * cell.SurfaceHeight;
                SetHuntRings(_activeRaven?.huntRings, false);
            }
            _hunterGo = null;
            _activeRaven = null;
        }

        private bool IsSafe(HexCoordinate c) => _safeRegion != null && _map != null && _map.RegionAt(c) == _safeRegion;
    }
}
