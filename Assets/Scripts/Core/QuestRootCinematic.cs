using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.UI;
namespace TacticalRPG.Core
{
    /// <summary>Owns camera/input only for a queued new-quest reveal; releases every lease on interruption.</summary>
    [DefaultExecutionOrder(-90)]
    public sealed class QuestRootCinematic : MonoBehaviour
    {
        [SerializeField] private BayterekQuestRoots _roots;
        [SerializeField] private MandatoryQuestDirector _quests;
        [SerializeField] private ChapterMapGenerator _map;
        [SerializeField] private HexGridManager _grid;
        [SerializeField] private PlayerController _player;
        [SerializeField] private GameStateManager _state;
        [SerializeField] private FogOfWarManager _fog;
        [SerializeField] private Camera _camera;
        [SerializeField] private CameraFollow _follow;
        [SerializeField] private CameraZoomSettings _zoom;
        [SerializeField] private GameAudio _audio;
        [SerializeField] private Canvas _menuCanvas;
        [SerializeField] private ParticleSystem _dustPrefab;
        [SerializeField] private float _establishSize=11,_trackingSize=8.5f,_cameraDistance=38,_lookHeight=3;
        private ParticleSystem _dust;private Light _light;private bool _menuEnabled;
        private void Awake()
        {if(_dustPrefab!=null){_dust=Instantiate(_dustPrefab,transform);_dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);_light=_dust.GetComponentInChildren<Light>(true);}}
        [SerializeField,Min(.2f)] private float _establishSeconds=1.8f;
        [SerializeField,Min(.2f)] private float _returnSeconds=1.1f;
        [SerializeField,Range(55,85)] private float _divinePitch=72;
        private readonly HashSet<HexCoordinate> _requested=new();
        private readonly Queue<HexCoordinate> _queue=new();
        private Coroutine _routine; private HexCoordinate _active;
        private Vector3 _savedPosition; private Quaternion _savedRotation; private float _savedSize,_fade;
        private bool _followEnabled,_zoomEnabled,_movementLocked,_owned,_skip;
        public static bool IsPlaying{get;private set;}
        public int CompletedCount{get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatic()=>IsPlaying=false;
        private void OnEnable()
        {
            if(_quests!=null)_quests.OnQuestUnlocked+=Requested;
            if(_roots!=null)_roots.OnBranchCreated+=Created;
            if(_map!=null)_map.OnMapGenerated+=ResetSequence;
            if(_state!=null)_state.OnStateChanged+=State;
        }
        private void OnDisable()
        {
            if(_quests!=null)_quests.OnQuestUnlocked-=Requested;
            if(_roots!=null)_roots.OnBranchCreated-=Created;
            if(_map!=null)_map.OnMapGenerated-=ResetSequence;
            if(_state!=null)_state.OnStateChanged-=State;
            ResetSequence();
        }
        private void Requested(int index,HexCoordinate goal){if(Time.timeSinceLevelLoad>1)_requested.Add(goal);}
        private void Created(HexCoordinate goal){if(!_requested.Remove(goal))return;_roots.PauseGrowth(goal,true);_queue.Enqueue(goal);}
        private void State(GameState state){if(state!=GameState.Overworld){ResetSequence();_zoom?.Apply();}}
        private void Update()
        {
            if(IsPlaying&&(UnityEngine.Input.GetKeyDown(KeyCode.Escape)||UnityEngine.Input.GetKeyDown(KeyCode.Space)))
            {_skip=true;EscapeKeyClaim.Claim();}
            if(_routine==null&&_queue.Count>0&&_camera!=null&&_player!=null&&!_player.IsMoving&&!MenuState.IsAnyOpen&&(_state==null||_state.State==GameState.Overworld))
            {_active=_queue.Dequeue();_routine=StartCoroutine(Reveal(_active));}
        }
        private IEnumerator Reveal(HexCoordinate goal)
        {
            if(!_roots.TryGetTree(out var tree)){_roots.PauseGrowth(goal,false);yield break;}
            _savedPosition=_camera.transform.position;_savedRotation=_camera.transform.rotation;_savedSize=_camera.orthographicSize;
            _followEnabled=_follow!=null&&_follow.enabled;_zoomEnabled=_zoom!=null&&_zoom.enabled;
            _movementLocked=_player.IsMovementLocked;_owned=true;IsPlaying=true;_skip=false;
            if(_follow!=null)_follow.enabled=false;if(_zoom!=null)_zoom.enabled=false;_player.SetMovementLocked(true);
            _audio?.SetCinematicDucking(true);SoundSignals.Play("root_awaken");
            _menuEnabled=_menuCanvas!=null&&_menuCanvas.enabled;if(_menuCanvas!=null)_menuCanvas.enabled=false;
            if(_dust!=null){_dust.transform.position=tree.ToWorldPosition(_grid.HexSize)+Vector3.up*.6f;_dust.Play();_dust.Emit(65);}if(_light!=null){_light.transform.position=tree.ToWorldPosition(_grid.HexSize)+Vector3.up*4;_light.enabled=true;}
            var corridor=new List<HexCoordinate>(_roots.BranchCells(goal)){tree,goal};_fog?.SetCinematicReveal(corridor);
            Vector3 focus=tree.ToWorldPosition(_grid.HexSize)+Vector3.up*_lookHeight;
            var rotation=Quaternion.Euler(_divinePitch,_savedRotation.eulerAngles.y,0);
            Vector3 destination=focus-rotation*Vector3.forward*_cameraDistance;
            float size=Mathf.Max(_establishSize,_savedSize*1.05f);
            for(float t=0;t<_establishSeconds&&!_skip;t+=Time.unscaledDeltaTime)
            {float f=Mathf.SmoothStep(0,1,t/_establishSeconds);_fade=f;SetPose(Vector3.Lerp(_savedPosition,destination,f),Quaternion.Slerp(_savedRotation,rotation,f),Mathf.Lerp(_savedSize,size,f));yield return null;}
            _roots.PauseGrowth(goal,false);
            float elapsed=0;
            float deadline=Mathf.Max(15,_roots.GrowthSecondsRemaining(goal)+3);
            while(_roots.Growing(goal)&&!_skip&&elapsed<deadline)
            {
                elapsed+=Time.unscaledDeltaTime;
                Vector3 tip=_roots.GrowthTip(goal)+Vector3.up*1.8f;
                if(_dust!=null)_dust.transform.position=tip-Vector3.up*1.5f;if(_light!=null)_light.transform.position=tip+Vector3.up*2;
                // A wide overhead composition retains the tree before settling behind the root tip.
                focus=Vector3.Lerp(focus,tip,1-Mathf.Exp(-Time.unscaledDeltaTime*1.8f));
                SetPose(focus-rotation*Vector3.forward*_cameraDistance,rotation,Mathf.Lerp(size,_trackingSize,Mathf.Clamp01(elapsed/4)));
                _fade=1;yield return null;
            }
            if(!_skip){SoundSignals.Play("quest_new",.65f);yield return new WaitForSecondsRealtime(.55f);}
            var from=_camera.transform.position;var fromRotation=_camera.transform.rotation;float fromSize=_camera.orthographicSize;
            for(float t=0;t<_returnSeconds;t+=Time.unscaledDeltaTime)
            {float f=Mathf.SmoothStep(0,1,t/_returnSeconds);_fade=1-f;SetPose(Vector3.Lerp(from,_savedPosition,f),Quaternion.Slerp(fromRotation,_savedRotation,f),Mathf.Lerp(fromSize,_savedSize,f));yield return null;}
            Release();CompletedCount++;_routine=null;
        }
        private void SetPose(Vector3 position,Quaternion rotation,float size){_camera.transform.SetPositionAndRotation(position,rotation);_camera.orthographicSize=size;}
        private void Release()
        {
            _roots?.PauseGrowth(_active,false);_fog?.SetCinematicReveal(null);_audio?.SetCinematicDucking(false);
            if(_owned)
            {if(_camera!=null)SetPose(_savedPosition,_savedRotation,_savedSize);if(_follow!=null)_follow.enabled=_followEnabled;if(_zoom!=null)_zoom.enabled=_zoomEnabled;if(_player!=null)_player.SetMovementLocked(_movementLocked);}
            if(_owned&&_menuCanvas!=null)_menuCanvas.enabled=_menuEnabled;
            if(_dust!=null)_dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(_light!=null)_light.enabled=false;
            _owned=false;IsPlaying=false;_fade=0;
        }
        private void ResetSequence(){if(_routine!=null)StopCoroutine(_routine);_routine=null;Release();foreach(var goal in _queue)_roots?.PauseGrowth(goal,false);_queue.Clear();_requested.Clear();}
        private void OnGUI()
        {
            if(!IsPlaying)return;var old=GUI.color;GUI.color=new Color(.015f,.022f,.024f,_fade*.9f);
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height*.095f),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,Screen.height*.905f,Screen.width,Screen.height*.095f),Texture2D.whiteTexture);
            GUI.color=new Color(.85f,.77f,.53f,_fade);
            var style=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(Screen.height*.025f),fontStyle=FontStyle.Bold};
            GUI.Label(new Rect(0,Screen.height*.027f,Screen.width,Screen.height*.05f),"BAYTEREK UYANIYOR",style);
            style.fontSize=Mathf.RoundToInt(Screen.height*.016f);GUI.Label(new Rect(0,Screen.height*.935f,Screen.width,Screen.height*.035f),"Kökler yeni ahde uzanıyor     ·     Geç: Esc / Boşluk",style);GUI.color=old;
        }
    }
}
