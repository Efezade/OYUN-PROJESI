using UnityEngine;

namespace BattleTileWorkshop
{
    /// <summary>Integer axial placement and column height without a runtime Update loop.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class HexGroundPlacement : MonoBehaviour
    {
        [Header("Hex coordinate")]
        [SerializeField] private int _q;
        [SerializeField] private int _r;
        [SerializeField, Min(0)] private int _elevation;
        [SerializeField, Min(0.01f)] private float _radius = 1f;
        [SerializeField, Min(0.01f)] private float _elevationStep = 0.6f;
        [SerializeField] private bool _snapOnValidate = true;
        [Header("Prefab references")]
        [SerializeField] private Transform _top;
        [SerializeField] private Transform _sides;
        [SerializeField] private Transform _collision;
        [SerializeField] private Transform _attachments;
        [SerializeField, Min(0.01f)] private float _sourceSlabHeight = 0.3f;

        public int Q => _q;
        public int R => _r;
        public int Elevation => _elevation;
        public float Radius => _radius;
        public float TopHeight => _sourceSlabHeight + _elevation * _elevationStep;
        public Transform Top => _top;

        public void Bind(Transform top, Transform sides, Transform collision, Transform attachments)
        {
            _top = top;
            _sides = sides;
            _collision = collision;
            _attachments = attachments;
            Apply();
        }

        public void Configure(int q, int r, int elevation, float radius = 1f, float elevationStep = 0.6f)
        {
            _q = q;
            _r = r;
            _elevation = Mathf.Max(0, elevation);
            _radius = Mathf.Max(0.01f, radius);
            _elevationStep = Mathf.Max(0.01f, elevationStep);
            Apply();
        }

        private void OnValidate()
        {
            _elevation = Mathf.Max(0, _elevation);
            _radius = Mathf.Max(0.01f, _radius);
            _sourceSlabHeight = Mathf.Max(0.01f, _sourceSlabHeight);
            _elevationStep = Mathf.Max(0.01f, _elevationStep);
            if (_snapOnValidate) Apply();
        }

        [ContextMenu("Apply grid coordinate and elevation")]
        public void Apply()
        {
            transform.localPosition = AxialPosition(_q, _r, _radius);
            if (_top != null)
            {
                _top.localPosition = Vector3.up * (_elevation * _elevationStep);
                _top.localScale = new Vector3(_radius, 1f, _radius);
            }
            Vector3 columnScale = new Vector3(_radius, TopHeight / _sourceSlabHeight, _radius);
            if (_sides != null) _sides.localScale = columnScale;
            if (_collision != null) _collision.localScale = columnScale;
            if (_attachments != null)
            {
                _attachments.localPosition = Vector3.up * TopHeight;
                _attachments.localScale = new Vector3(_radius, 1f, _radius);
            }
        }

        [ContextMenu("Snap current position to nearest hex")]
        public void SnapCurrentPosition()
        {
            Vector3 p = transform.localPosition;
            float q = (Mathf.Sqrt(3f) / 3f * p.x - p.z / 3f) / _radius;
            float r = (2f / 3f * p.z) / _radius;
            float s = -q - r;
            int qi = Mathf.RoundToInt(q);
            int ri = Mathf.RoundToInt(r);
            int si = Mathf.RoundToInt(s);
            float dq = Mathf.Abs(qi - q), dr = Mathf.Abs(ri - r), ds = Mathf.Abs(si - s);
            if (dq > dr && dq > ds) qi = -ri - si;
            else if (dr > ds) ri = -qi - si;
            Configure(qi, ri, _elevation, _radius, _elevationStep);
        }

        public static Vector3 AxialPosition(int q, int r, float radius = 1f)
        {
            return new Vector3(Mathf.Sqrt(3f) * radius * (q + r * 0.5f), 0f, radius * 1.5f * r);
        }

        public static Vector3 Corner(int index, float radius = 1f)
        {
            float angle = (30f + 60f * index) * Mathf.Deg2Rad;
            return new Vector3(radius * Mathf.Cos(angle), 0f, radius * Mathf.Sin(angle));
        }
    }
}
