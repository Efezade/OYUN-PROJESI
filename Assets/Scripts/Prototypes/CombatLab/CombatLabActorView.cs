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
        private static readonly int Moving = Animator.StringToHash("IsMoving");
        public void Bind(LabUnit unit, Camera camera)
        {
            _unit = unit; _camera = camera;
            transform.position = new Vector3(unit.Position.x, _height, unit.Position.y);
            if (unit.Role == LabRole.Boss) _body.localScale *= 1.4f;
        }
        public void Refresh(bool selected, bool realtime)
        {
            if (_unit == null) return;
            if (!_unit.Alive) { _warning.gameObject.SetActive(false); gameObject.SetActive(false); return; }
            Vector3 destination = new Vector3(_unit.Position.x, _height, _unit.Position.y);
            Vector3 delta = destination - transform.position;
            if (_animator != null) _animator.SetBool(Moving, delta.sqrMagnitude > .003f);
            transform.position = Vector3.Lerp(transform.position, destination, 1 - Mathf.Exp(-_followSpeed * Time.deltaTime));
            if (delta.sqrMagnitude > .005f) _body.rotation = Quaternion.Slerp(_body.rotation, Quaternion.LookRotation(delta.normalized), Time.deltaTime * 15f);
            _selection.SetActive(selected);
            _mark.SetActive(_unit.Mark > 0);
            _healthFill.localScale = new Vector3(_healthWidth * Mathf.Clamp01((float)_unit.Health / _unit.MaxHealth), .07f, .07f);
            _caption.transform.position = transform.position + _labelOffset;
            _caption.transform.rotation = _camera.transform.rotation;
            _warning.gameObject.SetActive(realtime && _unit.Windup > 0);
            if (_unit.Windup > 0)
            {
                _warning.position = new Vector3(_unit.Aim.x, _height + .03f, _unit.Aim.y);
                float diameter = _unit.Role == LabRole.Boss ? 3.4f : 1.7f;
                _warning.localScale = new Vector3(diameter, .025f, diameter);
            }
        }
        public void RefreshCaption(bool realtime)
        {
            if (_unit == null) return;
            string extra = _unit.Mark > 0 ? " ◆" : "";
            if (!realtime && _unit.Ally) extra += (_unit.Moved ? " ·" : " ↗") + (_unit.Acted ? " ·" : " ⚔");
            _caption.text = _unit.Name + extra + "\n" + _unit.Health + "/" + _unit.MaxHealth;
        }
    }
}
