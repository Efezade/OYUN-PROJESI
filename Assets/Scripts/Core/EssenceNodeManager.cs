using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Overworld karolarına rastgele çok-tipli öz "node"ları yerleştirir (renkli küçük küreler),
    /// durum değişince gösterir/gizler. Oyuncu özlü karodayken CollectAt → 1 AP harcar, özleri
    /// cüzdana ekler, node'u kalıcı kaldırır. (MissionManager'ın öz-toplama aynası; grid sade kalır.)
    /// </summary>
    public class EssenceNodeManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager     _grid;
        [SerializeField] private GameStateManager   _stateManager;
        [SerializeField] private PlayerController    _player;
        [SerializeField] private ActionPointManager  _ap;
        [SerializeField] private EssenceWallet       _wallet;
        [SerializeField] private EssenceConfigSO     _config;

        [Header("Görsel / Maliyet")]
        [SerializeField] private float _nodeHeight    = 0.9f;
        [SerializeField] private float _nodeScale     = 0.16f;
        [SerializeField, Min(1)] private int _collectAPCost = 1;

        private static readonly int TypeCount = Enum.GetValues(typeof(EssenceType)).Length;

        private readonly Dictionary<HexCoordinate, int[]>        _nodes   = new(); // coord → türden miktar
        private readonly Dictionary<HexCoordinate, GameObject>  _visuals = new(); // coord → görsel kök
        private Transform             _container;
        private MaterialPropertyBlock _block;
        private bool _spawned;

        /// <summary>Bir node toplandı/değişti → HUD butonu yenilensin.</summary>
        public event Action OnNodesChanged;

        private void OnEnable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged += HandleState;
            if (_player != null)       _player.OnMoved += HandleMoved;
        }

        private void OnDisable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged -= HandleState;
            if (_player != null)       _player.OnMoved -= HandleMoved;
        }

        private void Start() => SpawnNodes();

        // ── Sorgu / toplama ───────────────────────────────────────────────────

        public bool HasEssenceAt(HexCoordinate c) => _nodes.ContainsKey(c);

        public bool CanCollect(HexCoordinate c) =>
            _stateManager != null && _stateManager.State == GameState.Overworld &&
            _nodes.ContainsKey(c) && (_ap == null || _ap.CurrentAP >= _collectAPCost);

        /// <summary>Karodaki özleri "2 Ates, 1 Su" gibi okunur metne çevirir (HUD).</summary>
        public string Describe(HexCoordinate c)
        {
            if (!_nodes.TryGetValue(c, out int[] amts)) return "";
            var sb = new StringBuilder();
            for (int t = 0; t < amts.Length; t++)
            {
                if (amts[t] <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                string name = _config != null ? _config.NameOf((EssenceType)t) : ((EssenceType)t).ToString();
                sb.Append($"{amts[t]} {name}");
            }
            return sb.ToString();
        }

        /// <summary>Karodaki özleri toplar (1 AP harcar, cüzdana ekler, node'u kaldırır).</summary>
        public bool CollectAt(HexCoordinate c)
        {
            if (!CanCollect(c)) return false;

            int[]  amts = _nodes[c];
            string desc = Describe(c); // silmeden önce yakala (log için)

            if (_ap != null) _ap.SpendAP(_collectAPCost);
            if (_wallet != null)
                for (int t = 0; t < amts.Length; t++)
                    if (amts[t] > 0) _wallet.Gain((EssenceType)t, amts[t]);

            if (_visuals.TryGetValue(c, out GameObject go) && go != null) Destroy(go);
            _visuals.Remove(c);
            _nodes.Remove(c);

            Debug.Log($"[Essence] {c} toplandi: {desc} (1 AP).");
            OnNodesChanged?.Invoke();
            return true;
        }

        // ── Spawn ─────────────────────────────────────────────────────────────

        private void SpawnNodes()
        {
            if (_spawned || _grid == null || _config == null || _grid.Cells == null) return;
            _spawned = true;
            EnsureContainer();

            foreach (var kv in _grid.Cells)
            {
                HexCell cell = kv.Value;
                if (!cell.IsWalkable)                       continue;
                if (cell.CellType == CellType.Watchtower)   continue; // özel yapı — öz yok
                if (_player != null && cell.Coordinate == _player.CurrentCoord) continue;
                if (UnityEngine.Random.value > _config.TileChance) continue;

                int   count = UnityEngine.Random.Range(_config.MinPerTile, _config.MaxPerTile + 1);
                int[] amts  = new int[TypeCount];
                for (int n = 0; n < count; n++)
                    amts[(int)_config.RandomWeightedType()]++;

                _nodes[cell.Coordinate] = amts;
                SpawnVisual(cell, amts);
            }

            RefreshVisibility();
        }

        private void SpawnVisual(HexCell cell, int[] amts)
        {
            var root = new GameObject($"Essence_{cell.Coordinate}");
            root.transform.SetParent(_container);

            int total = 0;
            foreach (var a in amts) total += a;

            Vector3 basePos = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + _nodeHeight);
            int idx = 0;
            for (int t = 0; t < amts.Length; t++)
            {
                for (int k = 0; k < amts[t]; k++)
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    var col = s.GetComponent<Collider>();
                    if (col != null) Destroy(col); // tıklama zemine geçsin

                    s.transform.SetParent(root.transform);
                    float ang = (idx / (float)Mathf.Max(1, total)) * Mathf.PI * 2f;
                    float r   = total > 1 ? 0.22f : 0f;
                    s.transform.position   = basePos + new Vector3(Mathf.Cos(ang) * r, idx * 0.05f, Mathf.Sin(ang) * r);
                    s.transform.localScale = Vector3.one * _nodeScale;
                    Tint(s.GetComponent<Renderer>(), _config.ColorOf((EssenceType)t));
                    idx++;
                }
            }
            _visuals[cell.Coordinate] = root;
        }

        // ── Görünürlük (overworld + sis durumuna göre) ────────────────────────

        private void HandleState(GameState s)         => RefreshVisibility();
        private void HandleMoved(HexCoordinate coord) => RefreshVisibility();

        // Yalnızca overworld'de ve sisi açılmış (keşfedilmiş) karolarda öz görünür.
        private void RefreshVisibility()
        {
            bool overworld = _stateManager == null || _stateManager.State == GameState.Overworld;
            foreach (var kv in _visuals)
            {
                if (kv.Value == null) continue;
                kv.Value.SetActive(overworld && NotHidden(kv.Key));
            }
        }

        private bool NotHidden(HexCoordinate c) =>
            _grid != null && _grid.TryGetCell(c, out HexCell cell) && cell.FogState != FogState.Hidden;

        // ── Yardımcılar ───────────────────────────────────────────────────────

        private void EnsureContainer()
        {
            if (_container == null)
            {
                _container = new GameObject("EssenceNodes").transform;
                _container.SetParent(transform, false);
            }
        }

        private void Tint(Renderer rend, Color color)
        {
            if (rend == null) return;
            _block ??= new MaterialPropertyBlock();
            rend.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color",     color);
            rend.SetPropertyBlock(_block);
        }
    }
}
