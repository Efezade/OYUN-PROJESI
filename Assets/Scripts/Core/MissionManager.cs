using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Overworld'deki görev alanlarını yönetir: hangi hex hangi göreve karşılık gelir,
    /// üstlerine görsel işaret (marker) koyar, durum değişince marker'ları gösterir/gizler.
    /// HexGridManager'a dokunmaz — görev mantığı tamamen burada (grid sade kalır).
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        [System.Serializable]
        public struct MissionPlacement
        {
            public HexCoordinate coord;
            public MissionData   mission;
        }

        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager   _grid;
        [SerializeField] private GameStateManager _stateManager;

        [System.Serializable]
        public struct TileMission
        {
            [Tooltip("Palet karo id'si (örn. deneme11). Bu id ile boyanmış savaş karoları bu görevi açar.")]
            public string      tileId;
            public MissionData mission;
        }

        [Header("Görevler (sabit koordinatlı — opsiyonel)")]
        [SerializeField] private List<MissionPlacement> _missions = new();

        [Header("Boyalı savaş karoları (palet canEnterCombat)")]
        [Tooltip("Karo id'sine ÖZEL görev eşlemesi — her savaş alanının FARKLI olması için buraya " +
                 "id→görev satırları ekle (her görev kendi savaş haritasını taşır). Eşleşme yoksa " +
                 "_defaultCombatMission kullanılır.")]
        [SerializeField] private List<TileMission> _tileMissions = new();
        [Tooltip("Savaş karosunun id'si için eşleme yoksa kullanılacak varsayılan görev.")]
        [SerializeField] private MissionData _defaultCombatMission;

        [Tooltip("Oyuncu göreve girmek için karoya kaç hex yakın olmalı (0 = sadece üstünde, 1 = bitişik de olur).")]
        [SerializeField, Min(0)] private int _enterRange = 1;

        [Header("Marker Görseli")]
        [SerializeField] private Color _markerColor  = new(1f, 0.85f, 0.1f);
        [SerializeField] private float _markerHeight = 1.3f;
        [SerializeField] private float _markerScale  = 0.35f;

        private readonly List<GameObject> _markers = new();

        private void OnEnable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_stateManager != null) _stateManager.OnStateChanged -= HandleStateChanged;
        }

        private void Start() => SpawnMarkers();

        public int EnterRange => _enterRange;

        public MissionData GetMissionAt(HexCoordinate coord)
        {
            foreach (var m in _missions)
                if (m.mission != null && m.coord == coord) return m.mission;
            return CombatTileMission(coord);
        }

        /// <summary>Verilen konuma _enterRange içindeki ilk görevi döndürür (yoksa null).
        /// Hem "savaşa gir" istemini hem de tıklama-onayını kapıya bağlamak için kullanılır.</summary>
        public MissionData GetEnterableMission(HexCoordinate from)
        {
            foreach (var m in _missions)
                if (m.mission != null && from.DistanceTo(m.coord) <= _enterRange) return m.mission;

            // Boyalı savaş karoları: menzil içindeki ilk savaş alanı.
            if (_grid != null && _grid.Cells != null)
                foreach (var cell in _grid.Cells.Values)
                    if (cell.CanEnterCombat && from.DistanceTo(cell.Coordinate) <= _enterRange)
                    {
                        MissionData md = CombatTileMission(cell.Coordinate);
                        if (md != null) return md;
                    }
            return null;
        }

        // Boyalı savaş karosu (palet canEnterCombat) → görev: önce karo id'sine özel eşleme
        // (_tileMissions — her alanın farklı savaş haritası olması bundan gelir), yoksa varsayılan.
        private MissionData CombatTileMission(HexCoordinate coord)
        {
            if (_grid == null || !_grid.TryGetCell(coord, out HexCell cell) || !cell.CanEnterCombat)
                return null;

            string id = _grid.TileMap != null ? _grid.TileMap.GetTileId(coord) : null;
            if (id != null)
                foreach (var tm in _tileMissions)
                    if (tm.mission != null && tm.tileId == id) return tm.mission;
            return _defaultCombatMission;
        }

        private void SpawnMarkers()
        {
            float hexSize = _grid != null ? _grid.HexSize : 1f;
            var block = new MaterialPropertyBlock();

            foreach (var m in _missions)
            {
                if (m.mission == null) continue;

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"MissionMarker_{m.coord}";
                go.transform.SetParent(transform);
                go.transform.position   = m.coord.ToWorldPosition(hexSize) + Vector3.up * _markerHeight;
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
        }

        private void HandleStateChanged(GameState state)
        {
            bool show = state == GameState.Overworld;
            foreach (var go in _markers)
                if (go != null) go.SetActive(show);
        }
    }
}
