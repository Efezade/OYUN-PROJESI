using UnityEngine;
using UnityEngine.Rendering;
namespace TacticalRPG.Core
{
    /// <summary>Render-only coherent shake: never accumulates into follow/cinematic camera position.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraImpulse : MonoBehaviour
    {
        [SerializeField,Range(0,1)] private float _strength=.65f;
        private Camera _camera;private float _until,_duration,_amount;private Vector3 _base;private bool _applied;
        private void Awake()=>_camera=GetComponent<Camera>();
        private void OnEnable(){RenderPipelineManager.beginCameraRendering+=Begin;RenderPipelineManager.endCameraRendering+=End;}
        private void OnDisable(){RenderPipelineManager.beginCameraRendering-=Begin;RenderPipelineManager.endCameraRendering-=End;if(_applied&&_camera!=null)_camera.transform.position=_base;_applied=false;}
        public void Kick(float amount,float duration){_amount=Mathf.Min(.22f,Mathf.Max(_amount,amount));_duration=Mathf.Max(.1f,duration);_until=Time.unscaledTime+_duration;}
        private void Begin(ScriptableRenderContext context,Camera camera)
        {
            if(camera!=_camera||_applied||Time.unscaledTime>=_until||QuestRootCinematic.IsPlaying)return;
            float age=1-(_until-Time.unscaledTime)/_duration;float envelope=Mathf.Sin(Mathf.Clamp01(age)*Mathf.PI)*Mathf.Pow(1-age,1.5f);
            float t=Time.unscaledTime;var offset=_camera.transform.right*Mathf.Sin(t*39)+_camera.transform.up*Mathf.Sin(t*47)*.45f;
            _base=_camera.transform.position;_camera.transform.position+=offset*(_amount*_strength*envelope);_applied=true;
        }
        private void End(ScriptableRenderContext context,Camera camera){if(camera!=_camera||!_applied)return;camera.transform.position=_base;_applied=false;if(Time.unscaledTime>=_until)_amount=0;}
    }
}
