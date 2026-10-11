using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>Large crowns respect exploration and fade when covering the player.</summary>
    public sealed class BiomeLandmarkVisibility : MonoBehaviour
    {
        [SerializeField] private Renderer[] _canopy;
        [SerializeField, Range(0, 1)] private float _occludedAlpha = .20f;
        [SerializeField, Min(.1f)] private float _fadeSpeed = 3f;
        private FogOfWarManager _fog;
        private PlayerController _player;
        private Camera _camera;
        private Renderer[] _all;
        private MaterialPropertyBlock _block;
        private HexCoordinate _coord;
        private float _alpha = 1f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public void Initialize(HexCoordinate coordinate) => _coord = coordinate;
        private void Awake()
        {
            _fog = FindFirstObjectByType<FogOfWarManager>();
            _player = FindFirstObjectByType<PlayerController>();
            _camera = Camera.main;
            _all = GetComponentsInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
        }
        private void LateUpdate()
        {
            bool known = _fog == null || _fog.CanRenderWorldModel(_coord);
            foreach (var r in _all) if (r != null) r.forceRenderingOff = !known;
            if (!known || _canopy == null || _camera == null || _player == null) return;
            Vector3 target = _player.transform.position + Vector3.up;
            var ray = new Ray(_camera.transform.position, target-_camera.transform.position);
            float length = Vector3.Distance(target, _camera.transform.position);
            bool covers = false;
            foreach (var r in _canopy)
                if (r != null && r.bounds.IntersectRay(ray, out float distance) && distance < length) { covers = true; break; }
            _alpha = Mathf.MoveTowards(_alpha, covers ? _occludedAlpha : 1f, _fadeSpeed*Time.deltaTime);
            foreach (var r in _canopy)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColor, new Color(1, 1, 1, _alpha));
                r.SetPropertyBlock(_block);
            }
        }
    }
}
