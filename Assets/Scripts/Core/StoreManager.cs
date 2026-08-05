using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Overworld MAĞAZA (store) karolarını yönetir: satın alınabilir katalog + oyuncu yakınlık sorgusu +
    /// mağaza karolarının üstüne altın işaret koyar. HexGridManager'a dokunmaz (grid sade kalır; karo
    /// <see cref="HexCell.IsStore"/> bayrağını palet <c>isStore</c>'dan alır). MissionManager deseninin
    /// mağaza karşılığı. Öz harcama <c>EssenceWallet</c>, etki uygulama <c>PlayerBuffs</c> — burası ikisini
    /// de bilmez; sadece "yakında mı + neyi satıyoruz" sorusunu yanıtlar (tek yönlü bağımlılık).
    /// </summary>
    public class StoreManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager   _grid;
        [SerializeField] private GameStateManager _stateManager;

        [Header("Katalog")]
        [Tooltip("Mağazada satılan öğeler (kalıcı item + geçici pot). Sıra = ekranda görünüm sırası.")]
        [SerializeField] private List<ShopItemSO> _catalog = new();

        [Tooltip("Oyuncu mağazaya kaç hex yakın olmalı (0 = üstünde, 1 = bitişik).")]
        [SerializeField, Min(0)] private int _openRange = 1;

        [Header("İşaret Görseli")]
        [SerializeField] private Color _markerColor  = new(1f, 0.82f, 0.2f);
        [SerializeField] private float _markerHeight = 1.6f;
        [SerializeField] private float _markerScale  = 0.3f;

        private readonly List<GameObject> _markers = new();

        public IReadOnlyList<ShopItemSO> Catalog => _catalog;
        public int OpenRange => _openRange;

        private void OnEnable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged -= HandleStateChanged;
        }

        private void Start() => RebuildMarkersDeferred();

        // ── Market DÜĞÜMLERİ (TASK-006) ─────────────────────────────────────
        // Kullanıcı kararı (2026-07-28): market düğümü ile boyalı "magaza" karosu AYNI dükkânı açar.
        // ChapterNodeManager düğüm karolarını buraya bildirir; boyama yolu aynen çalışmaya devam eder.
        private readonly List<HexCoordinate> _nodeStoreCoords = new();
        private bool _nodeStoresOpen = true;   // gündüz açık / gece kapalı

        /// <summary>Market düğümlerinin karolarını bildir (ChapterNodeManager çağırır).</summary>
        public void SetNodeStores(IEnumerable<HexCoordinate> coords, bool open)
        {
            _nodeStoreCoords.Clear();
            if (coords != null) _nodeStoreCoords.AddRange(coords);
            _nodeStoresOpen = open;
        }

        /// <summary>Gündüz/gece geçişinde market düğümlerinin açık/kapalı durumunu güncelle.</summary>
        public void SetNodeStoresOpen(bool open) => _nodeStoresOpen = open;

        /// <summary>Oyuncu bir mağaza karosunun ya da AÇIK bir market düğümünün
        /// <see cref="_openRange"/> menzilinde mi?</summary>
        public bool IsPlayerNearStore(HexCoordinate from)
        {
            if (_nodeStoresOpen)
                foreach (var c in _nodeStoreCoords)
                    if (from.DistanceTo(c) <= _openRange) return true;

            if (_grid == null || _grid.Cells == null) return false;
            foreach (var cell in _grid.Cells.Values)
                if (cell.IsStore && from.DistanceTo(cell.Coordinate) <= _openRange)
                    return true;
            return false;
        }

        // Karolar ada yüklendikten SONRA oluştuğu için işaret kurulumunu bir kare ertele.
        private void RebuildMarkersDeferred()
        {
            if (!isActiveAndEnabled) return;
            StopAllCoroutines();
            StartCoroutine(RebuildNextFrame());
        }

        private IEnumerator RebuildNextFrame()
        {
            yield return null; // grid karoları kursun
            RebuildMarkers();
        }

        private void RebuildMarkers()
        {
            foreach (var m in _markers) if (m != null) Destroy(m);
            _markers.Clear();

            if (_grid == null || _grid.Cells == null) return;
            float hexSize = _grid.HexSize;
            var block = new MaterialPropertyBlock();

            foreach (var cell in _grid.Cells.Values)
            {
                if (!cell.IsStore) continue;

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"StoreMarker_{cell.Coordinate}";
                go.transform.SetParent(transform);
                go.transform.position   = cell.Coordinate.ToWorldPosition(hexSize) + Vector3.up * _markerHeight;
                go.transform.localScale = Vector3.one * _markerScale;

                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col); // tıklama zemine geçsin

                var mr = go.GetComponent<MeshRenderer>();
                mr.GetPropertyBlock(block);
                block.SetColor("_BaseColor", _markerColor);
                block.SetColor("_Color",     _markerColor);
                mr.SetPropertyBlock(block);

                _markers.Add(go);
            }

            HandleStateChanged(_stateManager != null ? _stateManager.State : GameState.Overworld);
        }

        private void HandleStateChanged(GameState state)
        {
            bool show = state == GameState.Overworld;
            foreach (var go in _markers)
                if (go != null) go.SetActive(show);
        }
    }
}
