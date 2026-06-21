using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Sol tıklama ile hex karosu seçimini algılar ve A* yolu tetikler.
    /// Gizli (Hidden) karolara tıklanamaz.
    /// </summary>
    public class MapInputHandler : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private Camera           _camera;
        [SerializeField] private HexGridManager   _gridManager;
        [SerializeField] private PlayerController _player;
        [Tooltip("Opsiyonel — atanmışsa, yetenek hazırken tıklama hedefleme olur.")]
        [SerializeField] private AbilityCaster    _caster;
        [Tooltip("Opsiyonel — atanmışsa sadece Overworld state'te tıklama işlenir + görev tıklaması.")]
        [SerializeField] private GameStateManager _stateManager;
        [SerializeField] private MissionManager   _missionManager;

        [Header("Raycast")]
        [SerializeField] private LayerMask _clickableLayers = ~0;
        [SerializeField] private float     _rayDistance     = 300f;

        private HexPathfinder _pathfinder;

        private void Awake()
        {
            _pathfinder = new HexPathfinder();
            if (_camera == null) _camera = Camera.main;
        }

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            // Savaş/onay durumunda harita tıklaması işlenmez (akış HUD/diğer sistemlerce yönetilir).
            if (_stateManager != null && _stateManager.State != GameState.Overworld) return;
            if (_player.IsMoving) return;
            if (!TryGetClickedCoord(out HexCoordinate coord)) return;

            // 1) Yetenek hazırsa → hedefleme
            if (_caster != null && _caster.HasArmedAbility) { _caster.TryCastAt(coord); return; }

            // 2) Görev alanına tıklandıysa → onay akışı
            if (_stateManager != null && _missionManager != null)
            {
                MissionData mission = _missionManager.GetMissionAt(coord);
                if (mission != null) { _stateManager.RequestMission(mission); return; }
            }

            // 3) Aksi halde → hareket
            TryMoveTo(coord);
        }

        private bool TryGetClickedCoord(out HexCoordinate coord)
        {
            coord = default;
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _rayDistance, _clickableLayers))
                return false;
            coord = _gridManager.WorldToHex(hit.point);
            return true;
        }

        private void TryMoveTo(HexCoordinate targetCoord)
        {
            if (!_gridManager.TryGetCell(targetCoord, out HexCell target)) return;
            if (!target.IsWalkable)                                         return;
            if (target.FogState == FogState.Hidden)                         return;
            if (!_gridManager.TryGetCell(_player.CurrentCoord, out HexCell start)) return;

            var path = _pathfinder.FindPath(start, target, _gridManager);
            if (path != null)
                _player.MoveAlongPath(path);
        }
    }
}
