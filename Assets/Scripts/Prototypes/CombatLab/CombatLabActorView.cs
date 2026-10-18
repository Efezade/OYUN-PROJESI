using UnityEngine;

namespace TacticalRPG.Prototypes.CombatLab
{
    public sealed class CombatLabActorView : MonoBehaviour
    {
        [SerializeField] private Transform _body;
        [SerializeField] private Transform _healthFill;
        [SerializeField] private TextMesh _caption;
        [SerializeField] private GameObject _selection;
        [SerializeField] private GameObject _mark;
        [SerializeField] private Transform _warning;
        [SerializeField] private Animator _animator;
        [SerializeField] private float _height = .32f;
        [SerializeField] private float _followSpeed = 14f;
        [SerializeField] private float _healthWidth = 1f;
        [SerializeField] private Vector3 _labelOffset = new Vector3(0, 2.1f, 0);
        private LabUnit _unit;
        private Camera _camera;
        private System.Func<Vector2, float> _groundHeight;
        private static readonly int Moving = Animator.StringToHash("IsMoving");
        public void Bind(LabUnit unit, Camera camera, System.Func<Vector2, float> groundHeight = null)
        {
            _unit = unit; _camera = camera; _groundHeight = groundHeight;
            transform.position = new Vector3(unit.Position.x, Ground(unit.Position), unit.Position.y);
            if (unit.Role == LabRole.Boss) _body.localScale *= 1.4f;
        }
        public void Refresh(bool selected, bool realtime)
        {
            if (_unit == null) return;
            if (!_unit.Alive) { _warning.gameObject.SetActive(false); gameObject.SetActive(false); return; }
            Vector3 destination = new Vector3(_unit.Position.x, Ground(_unit.Position), _unit.Position.y);
            Vector3 delta = destination - transform.position;
            if (_animator != null) _animator.SetBool(Moving, delta.sqrMagnitude > .003f);
            transform.position = Vector3.Lerp(transform.position, destination, 1 - Mathf.Exp(-_followSpeed * Time.deltaTime));
            Vector3 facing = new Vector3(delta.x, 0, delta.z);
            if (facing.sqrMagnitude > .005f) _body.rotation = Quaternion.Slerp(_body.rotation, Quaternion.LookRotation(facing.normalized), Time.deltaTime * 15f);
            _selection.SetActive(selected);
            _mark.SetActive(_unit.Mark > 0);
            _healthFill.localScale = new Vector3(_healthWidth * Mathf.Clamp01((float)_unit.Health / _unit.MaxHealth), .07f, .07f);
            _caption.transform.position = transform.position + _labelOffset;
            _caption.transform.rotation = _camera.transform.rotation;
            _warning.gameObject.SetActive(realtime && _unit.Windup > 0);
            if (_unit.Windup > 0)
            {
                _warning.position = new Vector3(_unit.Aim.x, Ground(_unit.Aim) + .03f, _unit.Aim.y);
                float diameter = _unit.Role == LabRole.Boss ? 3.4f : 1.7f;
                _warning.localScale = new Vector3(diameter, .025f, diameter);
            }
        }
        private float Ground(Vector2 p) => _groundHeight == null ? _height : _groundHeight(p) + .02f;
        public void RefreshCaption(bool realtime, string displayName = null)
        {
            if (_unit == null) return;
            string extra = _unit.Mark > 0 ? " ◆" : "";
            if (!realtime && _unit.Ally) extra += (_unit.Moved ? " ·" : " ↗") + (_unit.Acted ? " ·" : " ⚔");
            _caption.text = (displayName ?? _unit.Name) + extra + "\n" + _unit.Health + "/" + _unit.MaxHealth;
        }
    }
}
