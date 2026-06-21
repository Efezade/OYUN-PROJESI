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

        [Header("Görevler")]
        [SerializeField] private List<MissionPlacement> _missions = new();

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

        public MissionData GetMissionAt(HexCoordinate coord)
        {
            foreach (var m in _missions)
                if (m.mission != null && m.coord == coord) return m.mission;
            return null;
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
