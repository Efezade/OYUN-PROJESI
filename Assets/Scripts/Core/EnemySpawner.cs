using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Aktif görevin düşman roster'ını savaş alanına spawn eder.
    /// GameStateManager.OnStateChanged'i dinler — Deployment'a girince düşmanları kurar
    /// (oyuncu yerleştirirken tehdidi görür), overworld'e dönünce temizler.
    /// Düşmanlar KARTLI (CharacterClassData → CharacterCard) → HP/Defense/stat mevcut
    /// hattan akar, hasar simetrik olur. (Event-driven; DeploymentManager'ın aynası.)
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private GameStateManager _stateManager;
        [SerializeField] private HexGridManager    _grid;
        [SerializeField] private UnitManager       _unitManager;

        [Header("Görsel")]
        [Tooltip("Opsiyonel — atanmazsa runtime kırmızı kapsül üretilir.")]
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private Color      _enemyColor = new(0.85f, 0.15f, 0.15f);
        [SerializeField] private float      _unitScale  = 0.45f;

        private readonly List<Unit> _spawned = new();
        private Transform _container;

        public IReadOnlyList<Unit> Spawned => _spawned;

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
                case GameState.Deployment: SpawnRoster();   break;
                case GameState.Combat:                       break; // düşmanlar savaşta kalır
                default:                   ClearEnemies();  break; // overworld → temizle
            }
        }

        // ── Spawn / temizlik ──────────────────────────────────────────────────

        private void SpawnRoster()
        {
            ClearEnemies(); // önceki kalıntı varsa
            MissionData mission = _stateManager != null ? _stateManager.ActiveMission : null;
            if (mission == null || _grid == null) return;

            foreach (var entry in mission.EnemyRoster)
            {
                if (entry.enemyClass == null) continue;
                if (!_grid.IsInBounds(entry.coord))
                {
                    Debug.LogWarning($"[EnemySpawner] {entry.enemyClass.ClassName} için {entry.coord} grid dışı — atlandı.");
                    continue;
                }

                var  card = new CharacterCard(entry.enemyClass, Mathf.Max(1, entry.level));
                Unit unit = SpawnUnit(entry.coord, card);
                _spawned.Add(unit);
            }

            if (_spawned.Count > 0)
                Debug.Log($"[EnemySpawner] {_spawned.Count} düşman spawn edildi ({mission.DisplayName}).");
        }

        private Unit SpawnUnit(HexCoordinate coord, CharacterCard card)
        {
            EnsureContainer();

            GameObject go;
            if (_enemyPrefab != null)
            {
                go = Instantiate(_enemyPrefab, _container);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.SetParent(_container);
                go.transform.localScale = Vector3.one * _unitScale;
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col); // tıklama ışını altındaki karoyu görsün
                TintRenderer(go.GetComponent<Renderer>(), _enemyColor);
            }
            go.name = $"Enemy_{card.Data.ClassName}_{coord}";

            Unit unit = go.GetComponent<Unit>();
            if (unit == null) unit = go.AddComponent<Unit>();

            unit.Configure(_grid, _unitManager, UnitTeam.Enemy);
            unit.Bind(card);
            unit.PlaceAt(coord);
            return unit;
        }

        private void ClearEnemies()
        {
            foreach (var u in _spawned)
                if (u != null) Destroy(u.gameObject);
            _spawned.Clear();
        }

        private void EnsureContainer()
        {
            if (_container == null)
            {
                _container = new GameObject("EnemyUnits").transform;
                _container.SetParent(transform, false);
            }
        }

        private static void TintRenderer(Renderer rend, Color color)
        {
            if (rend == null) return;
            var sh  = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            rend.sharedMaterial = mat;
        }
    }
}
