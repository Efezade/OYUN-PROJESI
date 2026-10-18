using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Prototypes.Expedition
{
    public sealed partial class ExpeditionView : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private CombatLabActorView[] _actors;
        [SerializeField] private GameObject _pulsePrefab;
        [SerializeField] private GameObject _spawnPrefab;
        [SerializeField] private LineRenderer _linePrefab;
        [SerializeField] private TextMesh _textPrefab;
        [SerializeField] private Transform _exitMarker;
        [SerializeField] private LineRenderer _route;
        [SerializeField] private GameObject[] _gates;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _hit;
        [SerializeField] private AudioClip _chain;
        [SerializeField] private Color _hitColor=new Color(1,.78f,.3f);
        [SerializeField] private Color _chainColor=new Color(.2f,1,.9f);
        [SerializeField] private float _followSpeed=6;
        [SerializeField] private int _poolSize=64;
        [SerializeField] private Color _chargeColor=new Color(1,.35f,.16f);
        [SerializeField] private Color _protectionColor=new Color(.25f,.75f,1);
        [SerializeField] private float _chargeLineWidth=.12f;
        private readonly Dictionary<int,LineRenderer> _chargeLines=new Dictionary<int,LineRenderer>();
        private readonly Dictionary<int,GameObject> _auras=new Dictionary<int,GameObject>();
        private GameObject _vortex;
        private ExpeditionSimulation _simulation;
        private ExpeditionConfig _config;
        private LabUnit _boundKam;
        private readonly Dictionary<int,CombatLabActorView> _views=new Dictionary<int,CombatLabActorView>();
        private readonly List<Fx> _effects=new List<Fx>();
        private readonly List<GameObject> _warnings=new List<GameObject>();
        private float _soundAt;
        private float _zoom;
        private sealed class Fx {public GameObject Pulse;public LineRenderer Line;public TextMesh Text;public float Life;public Vector3 Position;public int Kind;}
        public Camera Camera=>_camera;
        public void Bind(ExpeditionSimulation simulation,ExpeditionConfig config)
        {
            if(_simulation!=null)_simulation.Effect-=Effect;
            _simulation=simulation;_config=config;_simulation.Effect+=Effect;_zoom=config.CameraSize;
            PrepareWeaponVisuals();
            PrepareEncounterVisuals();
            if(_effects.Count==0)for(int i=0;i<_poolSize;i++)
            {var f=new Fx{Pulse=Instantiate(_pulsePrefab,transform),Line=Instantiate(_linePrefab,transform),Text=Instantiate(_textPrefab,transform)};f.Pulse.SetActive(false);f.Line.gameObject.SetActive(false);f.Text.gameObject.SetActive(false);_effects.Add(f);}
            if(_warnings.Count==0)for(int i=0;i<24;i++){var go=Instantiate(_spawnPrefab,transform);go.SetActive(false);_warnings.Add(go);}
            Refresh();
            if(_vortex==null){_vortex=Instantiate(_pulsePrefab,transform);_vortex.SetActive(false);}
        }
        private Vector3 Point(Vector2 p,float lift=0)=>new Vector3(p.x,_simulation.Map.Height(p)+lift,p.y);
        public bool Pointer(Vector2 screen,out Vector2 p)
        {
            var ray=_camera.ScreenPointToRay(screen);
            if(Physics.Raycast(ray,out var hit,150)){p=new Vector2(hit.point.x,hit.point.z);return true;}
            p=default;return false;
        }
        public int Pick(Vector2 point)
        {
            int id=-1;float best=.9f;
            foreach(var u in _simulation.Units){if(u.Ally||!u.Alive)continue;float d=Vector2.Distance(u.Position,point);if(d<best){best=d;id=u.Id;}}
            return id;
        }
        public void Zoom(float delta){_zoom=Mathf.Clamp(_zoom-delta,7.5f,12.5f);}
        public void Refresh()
        {
            if(_simulation==null||_simulation.Phase==ExpeditionPhase.Ready)return;
            if(_boundKam!=_simulation.Kam)
            {
                foreach(var actor in _views.Values)Destroy(actor.gameObject);_views.Clear();_boundKam=_simulation.Kam;
                ClearEnemyVisuals();
                foreach(var line in _chargeLines.Values)Destroy(line.gameObject);_chargeLines.Clear();
                foreach(var aura in _auras.Values)Destroy(aura);_auras.Clear();
                foreach(var f in _effects){f.Life=0;f.Pulse.SetActive(false);f.Line.gameObject.SetActive(false);f.Text.gameObject.SetActive(false);}
                var center=Point(_boundKam.Position);_camera.transform.position=center+_config.CameraOffset;_camera.transform.LookAt(center);
            }
            foreach(var u in _simulation.Units)
            {
                if(!_views.TryGetValue(u.Id,out var actor)){actor=Instantiate(_actors[(int)u.Role],transform);actor.Bind(u,_camera,_simulation.Map.Height);_views.Add(u.Id,actor);}
                actor.gameObject.SetActive(u.Alive);actor.RefreshCaption(true,_simulation.DisplayName(u));
                var tactic=_simulation.TacticOf(u);
                if((tactic==EnemyTactic.Charger||tactic==EnemyTactic.Boss||tactic==EnemyTactic.Sniper)&&!_chargeLines.ContainsKey(u.Id))
                {var line=Instantiate(_linePrefab,transform);line.positionCount=2;line.startWidth=line.endWidth=_chargeLineWidth;line.startColor=line.endColor=_chargeColor;if(_simulation.IsJourney)TintWeapon(line,_chargeColor);line.gameObject.SetActive(false);_chargeLines.Add(u.Id,line);}
                if(tactic==EnemyTactic.Protector&&!_auras.ContainsKey(u.Id))
                {var aura=Instantiate(_pulsePrefab,transform);var renderer=aura.GetComponent<Renderer>();if(renderer!=null){var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",_protectionColor);renderer.SetPropertyBlock(block);}_auras.Add(u.Id,aura);}
                AddEnemyVisual(u,actor.transform);
            }
        }
        private void LateUpdate()
        {
            if(_simulation==null||_simulation.Phase==ExpeditionPhase.Ready)return;
            var center=Point(_simulation.Kam.Position);
            _camera.transform.position=Vector3.Lerp(_camera.transform.position,center+_config.CameraOffset,1-Mathf.Exp(-_followSpeed*Time.unscaledDeltaTime));
            // Fixed pitch: follow without continually tilting the horizon.
            _camera.transform.rotation=Quaternion.LookRotation(-_config.CameraOffset);
            _camera.orthographicSize=Mathf.Lerp(_camera.orthographicSize,_zoom,Time.unscaledDeltaTime*6);
            foreach(var pair in _views){pair.Value.SuppressWarning(_simulation.SelfPreparation(_simulation.Find(pair.Key)));pair.Value.SetElevation(_simulation.EnemyAirHeight(_simulation.Find(pair.Key)));pair.Value.Refresh(pair.Key==0,true);}
            RefreshWeaponVisuals();
            RefreshEncounterVisuals();
            foreach(var pair in _chargeLines)
            {var u=_simulation.Find(pair.Key);bool active=u!=null&&(_simulation.ChargeActive(u)||_simulation.SniperAiming(u));pair.Value.gameObject.SetActive(active);if(active){pair.Value.SetPosition(0,Point(u.Position,.08f));pair.Value.SetPosition(1,Point(u.Aim,.08f));}}
            foreach(var pair in _auras)
            {var u=_simulation.Find(pair.Key);pair.Value.SetActive(u!=null&&u.Alive);if(u!=null){pair.Value.transform.position=Point(u.Position,.06f);float diameter=2*_simulation.ProtectorRadius;pair.Value.transform.localScale=new Vector3(diameter,.015f,diameter);}}
            if(_vortex!=null){_vortex.SetActive(_simulation.VortexActive);_vortex.transform.position=Point(_simulation.VortexPosition,.07f);float diameter=2*_simulation.VortexRadius;_vortex.transform.localScale=new Vector3(diameter,.02f,diameter);}
            for(int i=0;i<_warnings.Count;i++)
            {
                bool active=i<_simulation.Pending.Count;_warnings[i].SetActive(active);
                if(active){var pending=_simulation.Pending[i];_warnings[i].transform.position=Point(pending.Unit.Position,.05f);float size=.8f+.25f*Mathf.Sin(pending.Remaining*8);_warnings[i].transform.localScale=new Vector3(size,.015f,size);}
            }
            bool travel=_simulation.Phase==ExpeditionPhase.Travel;
            if(_gates!=null)for(int i=0;i<_gates.Length;i++)_gates[i].SetActive(i>=_simulation.Stage&&!(i==_simulation.Stage&&travel));
            _exitMarker.gameObject.SetActive(travel);_route.gameObject.SetActive(travel);
            if(travel){var destination=_simulation.Map.Center(_simulation.Stage+1);_exitMarker.position=Point(destination,1.4f);_route.SetPosition(0,center+Vector3.up*.12f);_route.SetPosition(1,Point(destination,.12f));}
            float dt=_simulation.Paused||_simulation.Phase==ExpeditionPhase.Choosing?0:Time.deltaTime;
            foreach(var f in _effects)
            {
                if(f.Life<=0)continue;f.Life-=dt;
                if(f.Life<=0){f.Pulse.SetActive(false);f.Line.gameObject.SetActive(false);f.Text.gameObject.SetActive(false);continue;}
                float t=1-f.Life/.5f;float scale=Mathf.Lerp(.2f,f.Kind==3?7.2f:4.5f,t);
                f.Pulse.transform.localScale=new Vector3(scale,.015f,scale);f.Text.transform.position=f.Position+Vector3.up*(1.7f+t);f.Text.transform.rotation=_camera.transform.rotation;
            }
        }
        private void Effect(LabFx e)
        {
            Fx free=null;foreach(var f in _effects)if(f.Life<=0){free=f;break;}if(free==null)return;
            free.Life=.5f;free.Kind=e.Kind;free.Position=Point(e.To,.08f);
            free.Pulse.SetActive(e.Kind==1||e.Kind==3||e.Kind==4);free.Pulse.transform.position=free.Position;
            free.Line.gameObject.SetActive(e.Kind==0||e.Kind==2);free.Line.SetPosition(0,Point(e.From,.8f));free.Line.SetPosition(1,Point(e.To,.8f));
            var color=e.Kind==1||e.Kind==2?_chainColor:_hitColor;free.Line.startColor=free.Line.endColor=color;
            free.Text.gameObject.SetActive(e.Amount>0);free.Text.text=e.Amount.ToString();free.Text.color=color;
            DrawWeaponEffect(free,e);
            if(_audio!=null&&Time.unscaledTime>_soundAt){_soundAt=Time.unscaledTime+.07f;var clip=e.Kind==1?_chain:_hit;if(clip!=null)_audio.PlayOneShot(clip,e.Kind==1?.22f:.1f);}
        }
        private void OnDestroy(){if(_simulation!=null)_simulation.Effect-=Effect;}
    }
}
