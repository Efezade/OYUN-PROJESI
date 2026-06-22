using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Savaş görselleştirmesi: aktif birimin üstüne işaret koyar, oyuncu turunda
    /// ulaşılabilir (yeşil) ve saldırılabilir (kırmızı) hücreleri vurgular.
    /// TurnManager.OnTurnChanged dinler; logic TurnManager'da, bu sınıf yalnızca görsel.
    /// (DeploymentManager ped kalıbının savaş aynası.)
    /// </summary>
    public class CombatHighlighter : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private TurnManager    _turnManager;
        [SerializeField] private HexGridManager _grid;

        [Header("Renkler")]
        [SerializeField] private Color _moveColor   = new(0.30f, 0.90f, 0.40f);
        [SerializeField] private Color _attackColor = new(1f, 0.30f, 0.25f);
        [SerializeField] private Color _activeColor = new(1f, 0.90f, 0.20f);
        [SerializeField] private float _markerHeight = 1.0f;

        private readonly List<GameObject> _pads = new();
        private GameObject _activeMarker;
        private Transform  _container;
        private Material   _moveMat, _attackMat;

        private void OnEnable()
        {
            if (_turnManager != null) _turnManager.OnTurnChanged += Refresh;
        }

        private void OnDisable()
        {
            if (_turnManager != null) _turnManager.OnTurnChanged -= Refresh;
            ClearAll();
        }

        // Aktif birim işareti her kare onu takip etsin (hareket animasyonu sırasında da).
        private void Update()
        {
            Unit cur = _turnManager != null ? _turnManager.CurrentUnit : null;
            if (cur != null && _turnManager.CombatActive)
            {
                EnsureMarker();
                _activeMarker.SetActive(true);
                _activeMarker.transform.position = cur.transform.position + Vector3.up * _markerHeight;
            }
            else if (_activeMarker != null)
            {
                _activeMarker.SetActive(false);
            }
        }

        private void Refresh()
        {
            ClearPads();

            Unit cur = _turnManager != null ? _turnManager.CurrentUnit : null;
            if (cur == null || _grid == null || !_turnManager.IsPlayerTurn) return;

            if (!_turnManager.CurrentHasMoved)
                foreach (var c in _turnManager.ComputeReachable(cur, out _))
                    SpawnPad(c, MoveMat(), 0.05f);

            if (!_turnManager.CurrentHasActed)
                foreach (var c in _turnManager.ComputeAttackable(cur))
                    SpawnPad(c, AttackMat(), 0.08f);
        }

        // ── Görsel yardımcılar ────────────────────────────────────────────────

        private void SpawnPad(HexCoordinate coord, Material mat, float lift)
        {
            if (!_grid.TryGetCell(coord, out HexCell cell)) return;
            EnsureContainer();

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = pad.GetComponent<Collider>();
            if (col != null) Destroy(col); // tıklama ışını altındaki karoyu görsün
            pad.transform.SetParent(_container);
            pad.transform.position   = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + lift);
            pad.transform.localScale = new Vector3(1.25f, 0.04f, 1.25f);
            pad.GetComponent<Renderer>().sharedMaterial = mat;
            _pads.Add(pad);
        }

        private void EnsureMarker()
        {
            if (_activeMarker != null) return;
            _activeMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _activeMarker.name = "ActiveUnitMarker";
            var col = _activeMarker.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _activeMarker.transform.SetParent(transform);
            _activeMarker.transform.localScale = Vector3.one * 0.28f;
            _activeMarker.GetComponent<Renderer>().sharedMaterial = MakeMat(_activeColor);
        }

        private void EnsureContainer()
        {
            if (_container == null)
            {
                _container = new GameObject("CombatHighlights").transform;
                _container.SetParent(transform, false);
            }
        }

        private Material MoveMat()   => _moveMat   ? _moveMat   : (_moveMat   = MakeMat(_moveColor));
        private Material AttackMat() => _attackMat ? _attackMat : (_attackMat = MakeMat(_attackColor));

        private void ClearPads()
        {
            foreach (var p in _pads) if (p != null) Destroy(p);
            _pads.Clear();
        }

        private void ClearAll()
        {
            ClearPads();
            if (_activeMarker != null) Destroy(_activeMarker);
        }

        private static Material MakeMat(Color c)
        {
            var sh  = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh) { color = c };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            return mat;
        }
    }
}
