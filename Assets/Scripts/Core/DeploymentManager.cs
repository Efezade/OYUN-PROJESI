using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Savaş öncesi YERLEŞTİRME fazını yönetir: yerleştirme bölgesini vurgular,
    /// seçili kartı öz harcayarak hex'e Unit olarak spawn eder.
    /// GameStateManager.OnStateChanged'i dinler — Deployment'a girince kurar, çıkınca temizler.
    /// (Event-driven; durum makinesine tek yönlü bağlı.)
    /// </summary>
    public class DeploymentManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private GameStateManager _stateManager;
        [SerializeField] private HexGridManager    _grid;
        [SerializeField] private EssenceManager    _essence;
        [SerializeField] private UnitManager       _unitManager;
        [Tooltip("Komutanı (Kam) otomatik indirmek için parti kaynağı.")]
        [SerializeField] private PartyManager      _party;

        [Header("Yerleştirme Bölgesi")]
        [Tooltip("Savaş haritasının alt kaç satırı yerleştirme bölgesi olsun (R < bu değer).")]
        [SerializeField, Min(1)] private int _deployZoneRows = 2;

        [Header("Görsel")]
        [Tooltip("Opsiyonel — atanmazsa runtime kapsül üretilir.")]
        [SerializeField] private GameObject _unitPrefab;
        [SerializeField] private Color _playerUnitColor = new(0.30f, 0.60f, 1f);
        [SerializeField] private Color _zoneColor       = new(0.25f, 0.85f, 1f);

        public CharacterCard SelectedCard { get; set; }

        private readonly List<HexCoordinate>    _zone          = new();
        private readonly List<GameObject>       _markers       = new();
        private readonly List<Unit>             _deployed      = new();
        private readonly HashSet<CharacterCard> _deployedCards = new();
        private Transform _container;
        private Material  _zoneMat;
        private Unit      _commanderUnit; // otomatik inen Kam (ücretsiz, zorunlu)

        public IReadOnlyList<HexCoordinate> Zone => _zone;
        public int  DeployedCount => _deployed.Count;
        public Unit CommanderUnit => _commanderUnit; // otomatik inen Kam (yoksa null)
        public bool IsCardDeployed(CharacterCard c) => c != null && _deployedCards.Contains(c);

        private void OnEnable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Deployment: BeginDeployment();    break;
                case GameState.Combat:     ClearMarkers();       break; // birimler savaşta kalır
                default:                   TeardownDeployment(); break; // overworld → birimleri temizle
            }
        }

        // ── Kurulum / temizlik ────────────────────────────────────────────────

        private void BeginDeployment()
        {
            TeardownDeployment(); // önceki kalıntı varsa temizle
            BuildZone();
            ShowMarkers();
            SpawnCommander();     // Kam zorunlu + ücretsiz iner
        }

        // Pedleri kaldırır, yerleştirilen birimleri despawn eder, seçimi sıfırlar.
        private void TeardownDeployment()
        {
            ClearMarkers();
            foreach (var u in _deployed)
                if (u != null) Destroy(u.gameObject);

            _deployed.Clear();
            _deployedCards.Clear();
            _commanderUnit = null;
            SelectedCard   = null;
        }

        private void BuildZone()
        {
            _zone.Clear();
            if (_grid == null || _grid.Cells == null) return;

            foreach (var kv in _grid.Cells)
            {
                HexCell cell = kv.Value;
                if (cell.Coordinate.R < _deployZoneRows && cell.IsWalkable)
                    _zone.Add(cell.Coordinate);
            }
        }

        // ── Yerleştirme ───────────────────────────────────────────────────────

        /// <summary>Seçili kartı bu hex'e yerleştirmeyi dener (MapInputHandler çağırır).</summary>
        public bool TryDeployAt(HexCoordinate coord)
        {
            if (_stateManager == null || _stateManager.State != GameState.Deployment) return false;
            if (SelectedCard == null)              return false;
            if (SelectedCard.IsCommander)          return false; // Kam otomatik iner, elle yerleştirilmez
            if (IsCardDeployed(SelectedCard))      return false;
            if (!_zone.Contains(coord))            return false;
            if (_unitManager != null && _unitManager.GetUnitAt(coord) != null) return false;

            int cost = SelectedCard.Data.DeployCost;
            if (_essence == null || !_essence.TrySpend(cost))
            {
                Debug.Log($"[Deployment] Yetersiz öz: {SelectedCard.Data.ClassName} için {cost} gerekli.");
                return false;
            }

            Unit unit = SpawnUnit(coord, SelectedCard);
            _deployed.Add(unit);
            _deployedCards.Add(SelectedCard);
            Debug.Log($"[Deployment] {SelectedCard.Data.ClassName} → {coord} yerleştirildi (öz -{cost}).");

            SelectedCard = null; // bir tıkta bir kart
            return true;
        }

        private Unit SpawnUnit(HexCoordinate coord, CharacterCard card)
        {
            EnsureContainer();

            GameObject go;
            if (_unitPrefab != null)
            {
                go = Instantiate(_unitPrefab, _container);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.SetParent(_container);
                go.transform.localScale = Vector3.one * 0.45f;
                // Her kahraman kendi sınıf rengiyle (UnitColor) gelir → ayırt edilebilir.
                Color tint = card.Data != null ? card.Data.UnitColor : _playerUnitColor;
                TintRenderer(go.GetComponent<Renderer>(), tint);
            }
            go.name = $"Unit_{card.Data.ClassName}_{coord}";

            Unit unit = go.GetComponent<Unit>();
            if (unit == null) unit = go.AddComponent<Unit>();

            card.RestoreFull(); // taze birim olarak in (savaş başı tam HP)
            unit.Configure(_grid, _unitManager, UnitTeam.Player);
            unit.Bind(card);
            unit.PlaceAt(coord);
            return unit;
        }

        // ── Komutan (Kam) — zorunlu + ücretsiz iniş ───────────────────────────

        /// <summary>Partideki komutanı (Kam) öz harcamadan, yerleştirme bölgesine otomatik indirir.</summary>
        private void SpawnCommander()
        {
            if (_party == null) return;

            CharacterCard commander = FindCommanderCard();
            if (commander == null) return;

            if (!TryPickCommanderCell(out HexCoordinate cell))
            {
                Debug.LogWarning("[Deployment] Komutan için boş yerleştirme hücresi bulunamadı.");
                return;
            }

            _commanderUnit = SpawnUnit(cell, commander);
            _deployed.Add(_commanderUnit);
            _deployedCards.Add(commander);
            Debug.Log($"[Deployment] Komutan {commander.Data.ClassName} → {cell} otomatik indi (ücretsiz).");
        }

        private CharacterCard FindCommanderCard()
        {
            foreach (var c in _party.Party)
                if (c != null && c.IsCommander) return c;
            return null;
        }

        // Yerleştirme bölgesinde komutana en uygun boş hücreyi seç (alt-orta tercih edilir).
        private bool TryPickCommanderCell(out HexCoordinate result)
        {
            result = default;
            int  centerQ = _grid != null ? _grid.Width / 2 : 5;
            int  bestScore = int.MaxValue;
            bool found = false;

            foreach (var coord in _zone)
            {
                if (_unitManager != null && _unitManager.GetUnitAt(coord) != null) continue;
                int score = coord.R * 100 + Mathf.Abs(coord.Q - centerQ); // önce en alt satır, sonra merkez sütun
                if (score < bestScore) { bestScore = score; result = coord; found = true; }
            }
            return found;
        }

        // ── Görsel vurgulama ──────────────────────────────────────────────────

        private void ShowMarkers()
        {
            EnsureContainer();
            if (_zoneMat == null) _zoneMat = MakeColorMaterial(_zoneColor);

            foreach (var coord in _zone)
            {
                if (!_grid.TryGetCell(coord, out HexCell cell)) continue;

                var m = GameObject.CreatePrimitive(PrimitiveType.Cube); // ince yassı zemin pedi
                var col = m.GetComponent<Collider>();
                if (col != null) Destroy(col); // tıklama ışını altındaki karoyu görsün
                m.transform.SetParent(_container);
                m.transform.position   = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + 0.06f);
                m.transform.localScale = new Vector3(1.3f, 0.04f, 1.3f);
                m.GetComponent<Renderer>().sharedMaterial = _zoneMat;
                _markers.Add(m);
            }
        }

        private void ClearMarkers()
        {
            foreach (var m in _markers) if (m != null) Destroy(m);
            _markers.Clear();
        }

        private void EnsureContainer()
        {
            if (_container == null)
            {
                _container = new GameObject("DeploymentVisuals").transform;
                _container.SetParent(transform, false);
            }
        }

        private static void TintRenderer(Renderer rend, Color color)
        {
            if (rend == null) return;
            var mat = MakeColorMaterial(color);
            rend.sharedMaterial = mat;
        }

        private static Material MakeColorMaterial(Color color)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            return mat;
        }
    }
}
