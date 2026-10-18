using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Prototypes.CombatLab
{
    public sealed class CombatLabView : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private CombatLabActorView[] _actorPrefabs;
        [SerializeField] private Transform _actorsRoot;
        [SerializeField] private GameObject _moveMarkerPrefab;
        [SerializeField] private GameObject _pulsePrefab;
        [SerializeField] private LineRenderer _linePrefab;
        [SerializeField] private TextMesh _damagePrefab;
        [SerializeField] private Transform _hoverRing;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField] private AudioClip _chainClip;
        [SerializeField] private Color _hitColor = new Color(1f, .8f, .4f);
        [SerializeField] private Color _chainColor = new Color(.4f, 1f, .95f);
        [SerializeField] private Color _dangerColor = new Color(1f, .32f, .25f);
        [SerializeField] private int _effectPoolSize = 48;
        [SerializeField] private float _effectSeconds = .5f;
        private readonly Dictionary<int, CombatLabActorView> _actors = new Dictionary<int, CombatLabActorView>();
        private readonly List<GameObject> _moves = new List<GameObject>();
        private readonly List<FxInstance> _effects = new List<FxInstance>();
        private CombatLabSimulation _simulation;
        private CombatLabConfig _config;
        private float _soundAt;
        private sealed class FxInstance
        {
            public GameObject Pulse; public LineRenderer Line; public TextMesh Text;
            public float Life; public Vector3 Position; public int Kind;
        }
        public Camera Camera => _camera;
        public void Bind(CombatLabSimulation simulation, CombatLabConfig config)
        {
            if (_simulation != null) _simulation.Effect -= OnEffect;
            _simulation = simulation; _config = config; simulation.Effect += OnEffect;
            foreach (var actor in _actors.Values) Destroy(actor.gameObject);
            _actors.Clear();
            foreach (var fx in _effects) { fx.Life = 0; fx.Pulse.SetActive(false); fx.Line.gameObject.SetActive(false); fx.Text.gameObject.SetActive(false); }
            if (_moves.Count == 0)
                foreach (var cell in simulation.Cells) { var go = Instantiate(_moveMarkerPrefab, transform); go.transform.position = ToWorld(cell.World(config.TileRadius), .32f); go.SetActive(false); _moves.Add(go); }
            if (_effects.Count == 0)
                for (int i = 0; i < _effectPoolSize; i++)
                {
                    var fx = new FxInstance { Pulse = Instantiate(_pulsePrefab, transform), Line = Instantiate(_linePrefab, transform), Text = Instantiate(_damagePrefab, transform) };
                    fx.Pulse.SetActive(false); fx.Line.gameObject.SetActive(false); fx.Text.gameObject.SetActive(false); _effects.Add(fx);
                }
            RefreshState();
        }
        private static Vector3 ToWorld(Vector2 p, float y) => new Vector3(p.x, y, p.y);
        public bool PointerWorld(Vector2 screen, out Vector2 point)
        {
            Ray ray = _camera.ScreenPointToRay(screen);
            if (new Plane(Vector3.up, new Vector3(0, .3f, 0)).Raycast(ray, out float t)) { var p = ray.GetPoint(t); point = new Vector2(p.x, p.z); return true; }
            point = default; return false;
        }
        public int PickUnit(Vector2 point)
        {
            int id = -1; float best = .8f;
            foreach (var u in _simulation.Units)
            { if (!u.Alive) continue; float d = Vector2.Distance(u.Position, point); if (d < best) { best = d; id = u.Id; } }
            return id;
        }
        public void SetHover(Vector2 point, bool visible)
        {
            _hoverRing.gameObject.SetActive(visible);
            if (visible) _hoverRing.position = ToWorld(_simulation.Nearest(point).World(_config.TileRadius), .34f);
        }
        public void RefreshState()
        {
            if (_simulation == null) return;
            foreach (var u in _simulation.Units)
            {
                if (!_actors.TryGetValue(u.Id, out var actor))
                { actor = Instantiate(_actorPrefabs[(int)u.Role], _actorsRoot); actor.Bind(u, _camera); _actors.Add(u.Id, actor); }
                actor.gameObject.SetActive(u.Alive); actor.RefreshCaption(_simulation.Mode == LabMode.Realtime);
            }
            Dictionary<LabHex, int> available = null;
            var selected = _simulation.Find(_simulation.SelectedId);
            if (_simulation.Mode == LabMode.Tactical && _simulation.Phase == LabPhase.Player && selected != null && selected.Alive && !selected.Moved && !_simulation.Paused)
                available = _simulation.Reachable(selected, _config.MoveHexes);
            for (int i = 0; i < _moves.Count; i++) _moves[i].SetActive(available != null && available.ContainsKey(_simulation.Cells[i]));
        }
        private void Update()
        {
            if (_simulation == null) return;
            foreach (var pair in _actors) pair.Value.Refresh(pair.Key == _simulation.SelectedId, _simulation.Mode == LabMode.Realtime);
            foreach (var fx in _effects)
            {
                if (fx.Life <= 0) continue;
                fx.Life -= Time.deltaTime;
                if (fx.Life <= 0) { fx.Pulse.SetActive(false); fx.Line.gameObject.SetActive(false); fx.Text.gameObject.SetActive(false); continue; }
                float t = 1 - fx.Life / _effectSeconds;
                float max = fx.Kind == 3 ? _config.BurstRadius * 2 : _config.ChainRadius * 2;
                fx.Pulse.transform.localScale = new Vector3(Mathf.Lerp(.15f, max, t), .025f, Mathf.Lerp(.15f, max, t));
                fx.Text.transform.position = fx.Position + Vector3.up * (1.8f + t);
                fx.Text.transform.rotation = _camera.transform.rotation;
            }
        }
        private void OnEffect(LabFx effect)
        {
            FxInstance free = null;
            foreach (var fx in _effects) if (fx.Life <= 0) { free = fx; break; }
            if (free == null) return;
            free.Life = _effectSeconds; free.Position = ToWorld(effect.To, .38f); free.Kind = effect.Kind;
            free.Pulse.SetActive(effect.Kind == 1 || effect.Kind == 3);
            free.Pulse.transform.position = free.Position;
            free.Line.gameObject.SetActive(effect.Kind == 0 || effect.Kind == 2);
            free.Line.SetPosition(0, ToWorld(effect.From, 1.1f)); free.Line.SetPosition(1, ToWorld(effect.To, 1.1f));
            Color color = effect.Kind == 2 || effect.Kind == 1 ? _chainColor : _hitColor;
            free.Line.startColor = free.Line.endColor = color;
            free.Text.gameObject.SetActive(effect.Amount > 0); free.Text.text = effect.Amount.ToString(); free.Text.color = color;
            if (_audio != null && Time.unscaledTime >= _soundAt)
            { _soundAt = Time.unscaledTime + .06f; var clip = effect.Kind == 1 ? _chainClip : _hitClip; if (clip != null) _audio.PlayOneShot(clip, effect.Kind == 1 ? .28f : .13f); }
        }
        private void OnDestroy() { if (_simulation != null) _simulation.Effect -= OnEffect; }
    }
}
