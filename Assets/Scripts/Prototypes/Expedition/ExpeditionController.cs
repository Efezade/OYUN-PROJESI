using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Prototypes.Expedition
{
    public sealed class ExpeditionController : MonoBehaviour
    {
        [SerializeField] private ExpeditionConfig _config;
        [SerializeField] private ExpeditionView _view;
        [SerializeField] private ExpeditionHud _hud;
        private ExpeditionSimulation _simulation;
        private ExpeditionSave _saved;
        private Vector2 _move;
        private Vector2 _lastDirection=Vector2.right;
        private Vector2 _pointer;
        private bool _hasPointer;
        private bool _aiming;
        private int _savedStage=-1;
        private string _capturePath;
        private bool _captureChoice;
        public ExpeditionSimulation Simulation=>_simulation;
        public bool Aiming=>_aiming;
        public bool CanLoad=>_saved!=null;
        public string SaveStatus {get;private set;}="Mabet kaydı bekleniyor.";
        public string SavePath=>Path.Combine(Application.persistentDataPath,"KamExpedition",_config.WeaponJourney?"checkpoint-v3.json":"checkpoint-v1.json");
        private void Start()
        {
            _simulation=new ExpeditionSimulation(_config);_simulation.Changed+=Changed;_hud.Bind(this,_config);
            try{if(File.Exists(SavePath)){var candidate=JsonUtility.FromJson<ExpeditionSave>(File.ReadAllText(SavePath));var probe=new ExpeditionSimulation(_config);if(probe.Load(candidate))_saved=candidate;}}
            catch(Exception e){Debug.LogWarning("Expedition checkpoint not loaded: "+e.Message);}
            _hud.ShowMenu();
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]=="--expedition-capture")_capturePath=args[i+1];
            _captureChoice=Array.IndexOf(args,"--expedition-choice")>=0;
            if(!string.IsNullOrEmpty(_capturePath)){Application.runInBackground=true;StartRun(12345);StartCoroutine(Capture());}
            StartCoroutine(Loop());
        }
        private IEnumerator Loop()
        {
            float accumulator=0;int count=0;
            while(true)
            {
                if(!_hud.MenuVisible&&string.IsNullOrEmpty(_capturePath))
                {
                    accumulator+=Mathf.Min(Time.unscaledDeltaTime,.25f);
                    while(accumulator>=.05f){_simulation.Tick(.05f,_move);accumulator-=.05f;_view.Refresh();if(++count%2==0)_hud.Refresh();}
                }
                else accumulator=0;
                yield return null;
            }
        }
        private void StartRun(int seed){_savedStage=-1;_aiming=false;_simulation.Start(seed);_view.Bind(_simulation,_config);_hud.ShowGame();}
        public void NewRun(){StartRun(UnityEngine.Random.Range(1,999999));}
        public void SameSeed(){StartRun(_simulation.Seed);}
        public void Load(){if(_saved==null)return;_savedStage=-1;_aiming=false;if(_simulation.Load(_saved)){_view.Bind(_simulation,_config);_hud.ShowGame();}}
        public void Retry(){_aiming=false;_simulation.Retry();}
        public void Continue(){_simulation.Continue();}
        public void Choose(int index){_simulation.Choose(index);}
        public void Pause(){_aiming=false;_simulation.TogglePause();}
        public void Menu(){if(_simulation.MovingAllowed)_simulation.TogglePause();_hud.ShowMenu();}
        public void Resume(){if(_simulation.Phase==ExpeditionPhase.Ready)return;_hud.ShowGame();if(_simulation.Paused)_simulation.TogglePause();}
        public void Quit(){Application.Quit();}
        public void AimPull(){if(_simulation.IsJourney){_simulation.Cast(LabSkill.Pull,_simulation.Kam.Position+_simulation.Facing*4);return;}_aiming=!_aiming;_hud.Refresh();}
        public void Burst(){_aiming=false;_simulation.Cast(LabSkill.Burst,_simulation.Kam.Position);}
        public void Dash(){_simulation.Dash(_move.sqrMagnitude>.01f?_move:_lastDirection);}
        private void Changed()
        {
            if(_simulation.Phase==ExpeditionPhase.Camp&&_savedStage!=_simulation.Stage&&string.IsNullOrEmpty(_capturePath))
            {
                var candidate=_simulation.Checkpoint();
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(SavePath));string temp=SavePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(candidate,true));
                    if(File.Exists(SavePath))File.Replace(temp,SavePath,SavePath+".bak");else File.Move(temp,SavePath);
                    _saved=candidate;_savedStage=_simulation.Stage;SaveStatus="Mabet kaydı diske yazıldı. Burada güvenle çıkabilirsin.";
                }
                catch(Exception e){SaveStatus="Kayıt yazılamadı: "+e.Message;Debug.LogWarning(SaveStatus);}
            }
            _view.Refresh();_hud.Refresh();
        }
        private void Update()
        {
            if(_simulation==null||_hud.MenuVisible||!string.IsNullOrEmpty(_capturePath)){_move=Vector2.zero;return;}
            var k=Keyboard.current;var m=Mouse.current;
            if(k==null)return;
            _move=new Vector2((k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0),(k.wKey.isPressed||k.upArrowKey.isPressed?1:0)-(k.sKey.isPressed||k.downArrowKey.isPressed?1:0));
            if(_move.sqrMagnitude>.01f)_lastDirection=_move.normalized;
            if(k.escapeKey.wasPressedThisFrame){if(_aiming)_aiming=false;else Pause();}
            if(_simulation.Phase==ExpeditionPhase.Choosing||_simulation.Phase==ExpeditionPhase.WeaponChoice){if(k.digit1Key.wasPressedThisFrame)Choose(0);else if(k.digit2Key.wasPressedThisFrame)Choose(1);else if(k.digit3Key.wasPressedThisFrame)Choose(2);return;}
            if(k.enterKey.wasPressedThisFrame&&_simulation.Phase==ExpeditionPhase.Camp)Continue();
            bool ui=EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject();
            _hasPointer=m!=null&&_view.Pointer(m.position.ReadValue(),out _pointer);
            if(k.qKey.wasPressedThisFrame&&_hasPointer&&!ui)_simulation.Cast(LabSkill.Pull,_pointer);
            if(k.eKey.wasPressedThisFrame)Burst();if(k.spaceKey.wasPressedThisFrame)Dash();
            if(m!=null)
            {
                if(m.rightButton.wasPressedThisFrame)_aiming=false;
                if(!ui&&m.scroll.ReadValue().y!=0)_view.Zoom(m.scroll.ReadValue().y/120*.6f);
                if(_hasPointer&&!ui&&m.leftButton.wasPressedThisFrame)
                {if(_aiming){if(_simulation.Cast(LabSkill.Pull,_pointer))_aiming=false;}else _simulation.SetFocus(_view.Pick(_pointer));}
            }
        }
        private IEnumerator Capture()
        {
            if(_simulation.IsJourney)
            {
                var args=Environment.GetCommandLineArgs();bool spear=Array.IndexOf(args,"--weapon-spear")>=0;
                if(Array.IndexOf(args,"--weapon-menu")<0){for(int n=0;n<2;n++)if(_simulation.Offers[n]==(spear?1:0)){_simulation.Choose(n);break;}}
                if(Array.IndexOf(args,"--weapon-evolved")>=0)
                {
                    // Explicit visual fixture only: does not touch the player's checkpoint.
                    _simulation.Load(new ExpeditionSave{version=3,seed=12345,nextStage=2,xp=39,picks=4,weapon=spear?1:0,powers=new[]{1,1,1,0,1,0,0,0,0},credited=Array.Empty<int>(),offerState=12345});
                    _view.Bind(_simulation,_config);
                }
            }
            // Deterministic unattended visual check. Never writes a player checkpoint.
            int limit=_captureChoice?5000:_simulation.IsJourney?500:60;
            for(int i=0;i<limit;i++)
            {
                if(_simulation.Phase==ExpeditionPhase.Choosing)break;
                if(_simulation.Phase==ExpeditionPhase.Lost)break;
                if(_simulation.IsJourney)
                {
                    LabUnit captureEnemy=null;float best=float.MaxValue;
                    foreach(var u in _simulation.Units)if(!u.Ally&&u.Alive){float d=Vector2.Distance(u.Position,_simulation.Kam.Position);if(d<best){best=d;captureEnemy=u;}}
                    var move=Vector2.zero;
                    if(captureEnemy!=null){if(best>_simulation.WeaponRange*.8f||!_simulation.Map.HasLine(_simulation.Kam.Position,captureEnemy.Position))move=_simulation.Map.NextStep(_simulation.Kam.Position,captureEnemy.Position)-_simulation.Kam.Position;if(_captureChoice)_simulation.Cast(LabSkill.Pull,captureEnemy.Position);}
                    _simulation.Tick(.05f,move);
                    if(!_captureChoice&&_simulation.Charge>0)break;
                }
                else
                {
                    _simulation.Tick(.05f,Vector2.zero);
                    _simulation.Cast(LabSkill.Pull,_simulation.Kam.Position+Vector2.right*2);
                    _simulation.Cast(LabSkill.Burst,_simulation.Kam.Position);
                }
                if(i%10==0){_view.Refresh();_hud.Refresh();yield return null;}
            }
            _view.Refresh();_hud.Refresh();yield return new WaitForSecondsRealtime(.4f);
            var camera=_view.Camera;var canvas=_hud.GetComponent<Canvas>();var target=new RenderTexture(1440,900,24);target.Create();
            camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();
            File.WriteAllBytes(_capturePath,pixels.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;Destroy(pixels);target.Release();Destroy(target);
            Debug.Log("EXPEDITION_CAPTURE_OK "+_simulation.Phase+" "+_capturePath);Application.Quit();
        }
        private void OnApplicationFocus(bool focus){if(!focus&&_simulation!=null&&_simulation.MovingAllowed&&string.IsNullOrEmpty(_capturePath))Pause();}
        private void OnDestroy(){if(_simulation!=null)_simulation.Changed-=Changed;}
    }
}
