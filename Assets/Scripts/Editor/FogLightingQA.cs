using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TacticalRPG.Core;
using TacticalRPG.Grid;
using TacticalRPG.UI;
using Object=UnityEngine.Object;

namespace TacticalRPG.Editor
{
    [InitializeOnLoad] public static class FogLightingQA
    {
        [Serializable] private class Report { public int checks; public List<string> failures=new(); public List<string> notes=new(); }
        private static Report _report=new();
        private static double _next,_start;
        private static int _phase,_slot,_frame;
        private static Camera _camera; private static FogOfWarManager _fog; private static HexGridManager _grid;
        private static Vector3 _focus; private static float _lastAlpha;
        private static GameObject _concealSample;
        private static string Output=>Path.GetFullPath("FogLightingQAResults");
        static FogLightingQA(){if(SessionState.GetBool("FogLightingQA.Play",false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            if(!File.Exists(".biome-qa-copy"))throw new InvalidOperationException("Isolated QA copy required");
            Directory.CreateDirectory(Output);EditorSceneManager.OpenScene("Assets/Scenes/xd.unity");
            PresentationSetup.InstallRootRupture();
            Check(Find<WorldFogEffect>()!=null,"Camera fog not installed");
            var fog=Find<FogOfWarManager>();PresentationSetup.Set(fog,"_fadeSpeed",.91f);
            PresentationSetup.InstallRootRupture();Check(new SerializedObject(fog).FindProperty("_fadeSpeed").floatValue==.91f,"Repeated setup overwrote fog tuning");
            PresentationSetup.Set(fog,"_fadeSpeed",1.1f);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/Scenes/FogLightingProduction.unity",true);
            PlayerSettings.productName="OYUN_FogLightingQA";
            PresentationSetup.Set(Find<ChapterMapGenerator>(),"_fixedLayoutSeed",6118);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            SessionState.SetBool("FogLightingQA.Play",true);EditorApplication.isPlaying=true;
        }
        private static T Find<T>() where T:Component=>Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        private static void Check(bool okay,string message){_report.checks++;if(!okay){_report.failures.Add(message);Debug.LogError("[FogLightingQA] "+message);}}
        private static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<_next)return;
            try
            {
                if(_start==0){_start=EditorApplication.timeSinceStartup;Wait(7);return;}
                if(EditorApplication.timeSinceStartup-_start>140)throw new TimeoutException("Phase "+_phase);
                if(_phase==0)
                {
                    _camera=Camera.main;_fog=Find<FogOfWarManager>();_grid=Find<HexGridManager>();
                    Find<QuestRootCinematic>().enabled=false;Find<MiniatureEffect>().SetUserEnabled(false);
                    Find<DisplaySettings>().SetBrightness(1);Find<CameraFollow>().enabled=false;
                    Find<PlayerController>().enabled=false;
                    _focus=Find<ChapterMapGenerator>().RegionCores[0].coord.ToWorldPosition(_grid.HexSize);
                    _camera.transform.rotation=Quaternion.Euler(58,-30,0);_camera.transform.position=_focus-_camera.transform.forward*70;_camera.orthographicSize=10;
                    _fog.SetFogEnabled(false);Find<DayNightCycle>().ApplySlot(0,true);
                    Check(Find<WorldFogEffect>().Ready&&_fog.VolumeRendering,"Volume shader is not active");
                    _phase=1;Wait(1);return;
                }
                if(_phase==1)
                {
                    Capture("light_"+_slot);
                    if(++_slot<6){Find<DayNightCycle>().ApplySlot(_slot,true);Wait(.45);return;}
                    Find<DayNightCycle>().ApplySlot(0,true);
                    _fog.SetFogEnabled(true);_fog.SetFullReveal(false);_fog.SetNightMode(false);_fog.ClearPermanentReveals();
                    PresentationSetup.Set(_fog,"_permanentExploration",false);_fog.ResetFog();
                    _fog.UpdateFogAround(_focus+Vector3.left*19,2);_phase=2;Wait(1.3);return;
                }
                if(_phase==2)
                {
                    Capture("fog_hidden");
                    _concealSample=_grid.Cells.Values.Where(c=>c.Visual!=null&&c.Visual.activeInHierarchy&&_fog.VisualOpacity(c.Coordinate)>.99f)
                        .OrderBy(c=>Vector3.Distance(c.WorldPosition,_focus)).Take(15)
                        .OrderByDescending(c=>c.Visual.GetComponentsInChildren<Renderer>().Max(r=>r.bounds.size.y)).First().Visual;
                    _concealSample.SetActive(false);Capture("fog_hidden_without_model");_concealSample.SetActive(true);
                    CaptureTallConcealment();
                    Check(_grid.Cells.Values.Where(c=>c.Visual!=null).All(c=>c.Visual.GetComponentsInChildren<Renderer>(true).All(r=>!r.forceRenderingOff)),"Another landmark renderer still hard culled");
                    var cell=_grid.Cells.Values.OrderBy(c=>Vector3.Distance(c.WorldPosition,_focus)).First();
                    _lastAlpha=_fog.VisualOpacity(cell.Coordinate);
                    Check(_lastAlpha>.98f,"Hidden sample not opaque");
                    Check(cell.Visual.GetComponentsInChildren<Renderer>(true).All(r=>!r.forceRenderingOff),"Hidden geometry still hard culled");
                    _phase=3;return;
                }
                if(_phase==3)
                {
                    var cell=_grid.Cells.Values.OrderBy(c=>Vector3.Distance(c.WorldPosition,_focus)).First();
                    float alpha=_fog.VisualOpacity(cell.Coordinate);
                    Check(alpha<=_lastAlpha+.015f,"Approach fog reversed direction");
                    Check(Mathf.Abs(alpha-_lastAlpha)<.26f,"Approach fog jumped in one sample");
                    Check(cell.Visual.GetComponentsInChildren<Renderer>(true).All(r=>!r.forceRenderingOff),"Model crossed a hard visibility threshold");
                    _lastAlpha=alpha;
                    Capture("approach_"+_frame.ToString("D3"));
                    _fog.UpdateFogAround(_focus+Vector3.left*Mathf.Lerp(19,0,_frame/64f),2);
                    if(++_frame<=64){Wait(.1);return;}
                    _phase=4;Wait(1);return;
                }
                if(_phase==4)
                {
                    Capture("fog_revealed");
                    Check(_grid.Cells.Values.Where(c=>Vector3.Distance(c.WorldPosition,_focus)<2).All(c=>_fog.VisualOpacity(c.Coordinate)<.01f),"Approach never fully revealed nearby ground");
                    var root=_camera.GetComponent<WorldFogEffect>();root.enabled=false;Check(!_fog.VolumeRendering,"Disable did not restore fallback");root.enabled=true;Check(_fog.VolumeRendering,"Re-enable did not restore volume");
                    _fog.RevealAll();_phase=5;Wait(1.3);return;
                }
                if(_phase==5)
                {
                    Check(_grid.Cells.Values.All(c=>_fog.VisualOpacity(c.Coordinate)<.001f),"RevealAll left fog opacity");
                    Capture("tower_clear");
                    _concealSample.SetActive(false);Capture("tower_clear_without_model");_concealSample.SetActive(true);
                    var mission=AssetDatabase.LoadAssetAtPath<TacticalRPG.Data.MissionData>("Assets/Data/Missions/Pusu_UyananDev.asset");
                    Find<GameStateManager>().RequestMission(mission);Find<GameStateManager>().ConfirmMission();_phase=6;Wait(1);return;
                }
                if(_phase==6)
                {
                    Check(!_fog.FogEnabled,"Fog leaked into combat arena");
                    Check(_grid.Cells.Values.All(c=>c.Visual.GetComponentsInChildren<Renderer>(true).All(r=>!r.forceRenderingOff)),"Arena renderers remained hidden");
                    Find<GameStateManager>().ReturnToOverworld();_phase=7;Wait(1);return;
                }
                if(_phase==7)
                {
                    Check(_fog.FogEnabled&&_fog.FogField!=null,"World fog field not restored after combat");
                    Check(Find<WorldFogEffect>().Ready,"Camera pass lost after combat");
                    _report.notes.Add("Six lighting slots on actual game materials, 65 continuous approach captures, geometry residency, tower reveal, disable/re-enable, arena roundtrip and idempotent setup.");Finish();
                }
            }
            catch(Exception e){_report.failures.Add(e.ToString());Debug.LogException(e);Finish();}
        }
        private static void Capture(string name)
        {
            var rt=new RenderTexture(1440,900,24);_camera.targetTexture=rt;_camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();
            File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());RenderTexture.active=null;_camera.targetTexture=null;
            Object.DestroyImmediate(texture);Object.DestroyImmediate(rt);
        }
        private static void CaptureTallConcealment()
        {
            var position=_camera.transform.position;float size=_camera.orthographicSize;
            _camera.transform.position+=Vector3.up*7;_camera.orthographicSize=13;
            var probe=GameObject.CreatePrimitive(PrimitiveType.Cube);probe.name="QA above-cloud roof";
            probe.transform.position=_focus+Vector3.up*8.5f;probe.transform.localScale=new Vector3(2,17,2);
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",Color.red);
            var renderer=probe.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            Capture("fog_tall_17m");probe.SetActive(false);Capture("fog_tall_17m_removed");
            Object.DestroyImmediate(probe);Object.DestroyImmediate(material);
            _camera.transform.position=position;_camera.orthographicSize=size;
        }
        private static void Wait(double seconds)=>_next=EditorApplication.timeSinceStartup+seconds;
        private static void Finish(){File.WriteAllText(Output+"/report.json",JsonUtility.ToJson(_report,true));SessionState.SetBool("FogLightingQA.Play",false);EditorApplication.update-=Tick;EditorApplication.Exit(_report.failures.Count==0?0:1);}
    }
}
