using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Overworld karolarına EL YAPIMI öz haritasından (EssenceMapSO) öz "node"ları yerleştirir
    /// — rastgele DEĞİL. Her karoda bulunan TÜR başına tek görsel küre (üst üste binmez), karo
    /// yüzeyine yakın durur. Görsel = config'deki tür prefab'ı (atanmamışsa renkli placeholder).
    /// Oyuncu özlü karodayken CollectAt → 1 AP harcar, özleri cüzdana ekler, node'u kaldırır.
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
        [Tooltip("El yapımı öz yerleşimi (EssencePainterWindow ile boyanır).")]
        [SerializeField] private EssenceMapSO        _map;

        [Header("Görsel / Maliyet")]
        [Tooltip("Özün karo yüzeyinin ne kadar üstünde duracağı (yere yakın olsun diye küçük).")]
        [SerializeField] private float _nodeHeight    = 0.12f;
        [SerializeField] private float _nodeScale     = 0.16f;
        [Tooltip("Aynı karoda birden çok tür varsa kürelerin dağılacağı halka yarıçapı (üst üste binmesin).")]
        [SerializeField] private float _ringRadius    = 0.34f;
        [SerializeField, Min(1)] private int _collectAPCost = 1;

        private static readonly int TypeCount = Enum.GetValues(typeof(EssenceType)).Length;

        private readonly Dictionary<HexCoordinate, int[]>        _nodes   = new(); // coord → türden miktar
        private readonly Dictionary<HexCoordinate, GameObject>  _visuals = new(); // coord → görsel kök
        private Transform             _container;
        private MaterialPropertyBlock _block;
        private bool _spawned;

        /// <summary>Painter ve diğer editör araçları için (config + el yapımı harita).</summary>
        public EssenceConfigSO Config => _config;
        public EssenceMapSO    Map    => _map;

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

        // ── Spawn (el yapımı haritadan) ────────────────────────────────────────

        private void SpawnNodes()
        {
            if (_spawned || _grid == null || _config == null || _map == null || _grid.Cells == null) return;
            _spawned = true;
            EnsureContainer();

            foreach (var kv in _map.BuildLookup(TypeCount))
            {
                if (!_grid.TryGetCell(kv.Key, out HexCell cell)) continue; // harita dışı atama → atla
                _nodes[kv.Key] = kv.Value;
                SpawnVisual(cell, kv.Value);
            }

            RefreshVisibility();
        }

        // Karoda bulunan HER TÜR için tek küre; küçük bir halkaya dağılır (üst üste binmez).
        private void SpawnVisual(HexCell cell, int[] amts)
        {
            var root = new GameObject($"Essence_{cell.Coordinate}");
            root.transform.SetParent(_container);

            var present = new List<int>();
            for (int t = 0; t < amts.Length; t++)
                if (amts[t] > 0) present.Add(t);

            int     n       = present.Count;
            Vector3 basePos = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + _nodeHeight);
            float   ringR   = n > 1 ? _ringRadius : 0f;

            for (int i = 0; i < n; i++)
            {
                var     type = (EssenceType)present[i];
                float   ang  = (i / (float)n) * Mathf.PI * 2f;
                Vector3 pos  = basePos + new Vector3(Mathf.Cos(ang) * ringR, 0f, Mathf.Sin(ang) * ringR);
                SpawnOrb(type, pos, root.transform);
            }

            _visuals[cell.Coordinate] = root;
        }

        private void SpawnOrb(EssenceType type, Vector3 pos, Transform parent)
        {
            GameObject prefab = _config.PrefabOf(type);
            GameObject go;

            if (prefab != null)
            {
                go = Instantiate(prefab, pos, prefab.transform.rotation, parent);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(parent);
                go.transform.position   = pos;
                go.transform.localScale = Vector3.one * _nodeScale;
                Tint(go.GetComponent<Renderer>(), _config.ColorOf(type));
            }

            // Tıklama özün içinden zemine geçsin (hareket/seçim engellenmesin).
            foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
            go.name = $"Orb_{type}";
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
