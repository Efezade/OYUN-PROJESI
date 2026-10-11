using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;
using TacticalRPG.UI;
using Object=UnityEngine.Object;
namespace TacticalRPG.Editor
{
    /// <summary>Idempotent presentation assets and scene wiring. Gameplay configuration is preserved.</summary>
    public static class PresentationSetup
    {
        private const string Root="Assets/Art/Presentation";
        public const string Data="Assets/Data/Presentation";
        [Serializable] private class CueManifest{public CueData[] cues;}
        [Serializable] private class CueData{public string id;public float volume,cooldown;}
        [MenuItem("TacticalRPG/Sunum - Ses, Kok, Sis ve UI Kur")]
        public static void Install()
        {
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Data);AssetDatabase.Refresh();
            foreach(string name in new[]{"Theme","SoundLibrary"})if(AssetDatabase.LoadMainAssetAtPath(Data+"/"+name+".asset")==null && AssetDatabase.LoadMainAssetAtPath(Root+"/"+name+".asset")!=null)AssetDatabase.MoveAsset(Root+"/"+name+".asset",Data+"/"+name+".asset");
            var theme=Asset<PresentationThemeSO>(Data+"/Theme.asset");
            if(theme.panelSprite==null)Set(theme,"_panelSprite",RoundedSprite());
            var audio=Ensure<GameAudio>();Set(audio,"_library",BuildAudio());
            Bind(Ensure<GameSoundDirector>());
            var roots=Find<BayterekQuestRoots>();
            var bark=Material("BayterekBark","TacticalRPG/BayterekBark",new Color(.24f,.145f,.074f));
            if(roots!=null&&roots.Config!=null)
            {
                // Migrate the old defaults once; subsequent setup must preserve authored tweaks.
                if(Mathf.Approximately(roots.Config.Radius,.22f))Set(roots.Config,"_radius",.64f);
                if(Mathf.Approximately(roots.Config.SurfaceLift,.10f))Set(roots.Config,"_surfaceLift",.24f);
                if(roots.Config.BarkMaterial==null||AssetDatabase.GetAssetPath(roots.Config.BarkMaterial)=="Assets/Art/Materials/Biomes/GorevKoku.mat")Set(roots.Config,"_barkMaterial",bark);
            }
            var camera=Camera.main;if(camera!=null){var impulse=EnsureOn<CameraImpulse>(camera.gameObject);Set(Find<CameraZoomSettings>(),"_impulse",impulse);}
            var cinematic=Ensure<QuestRootCinematic>();Bind(cinematic);
            var menu=Find<MenuNavigator>();if(menu!=null)Set(cinematic,"_menuCanvas",menu.GetComponent<Canvas>());
            var dust=Material("RootDust","Universal Render Pipeline/Particles/Unlit",new Color(.7f,.52f,.25f,.65f));
            dust.SetFloat("_Surface",1);dust.SetFloat("_Blend",0);dust.SetFloat("_ZWrite",0);dust.SetFloat("_SrcBlend",5);dust.SetFloat("_DstBlend",10);dust.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");dust.renderQueue=3000;
            dust.SetTexture("_BaseMap",theme.panelSprite.texture);Set(cinematic,"_dustPrefab",RootDust(dust));
            InstallRootRupture();
            var mist=Material("Mist","TacticalRPG/WorldAtmosphere",new Color(.29f,.36f,.36f));
            var voidMat=Material("Void","TacticalRPG/WorldAtmosphere",new Color(.22f,.34f,.32f));voidMat.SetFloat("_Mode",1);
            var fogPrefab=SurfacePrefab("Mist",mist,true);
            var voidPrefab=SurfacePrefab("Void",voidMat,false);
            var fog=Find<FogOfWarManager>();if(fog!=null)
            {Set(fog,"_fogTilePrefab",fogPrefab);Set(fog,"_fogLift",.3f);Set(fog,"_windAmount",0f);Set(fog,"_fadeSpeed",1.8f);Set(fog,"_fogFalloff",1.7f);Set(fog,"_alarmColor",new Color(.65f,.32f,.18f));Set(fog,"_alarmStrength",.42f);}
            var abyss=Ensure<WorldVoidPresentation>();Bind(abyss);Set(abyss,"_voidPrefab",voidPrefab);
            var wave=Find<CollapseWaveEffect>();if(wave!=null)
            {Set(wave,"_waveColor",new Color(.58f,.48f,.33f,.55f));Set(wave,"_ringCount",2);Set(wave,"_ringWidth",.95f);Set(wave,"_segments",160);Set(wave,"_lift",.58f);Set(wave,"_bobHeight",.055f);Set(wave,"_tileFlashColor",new Color(.62f,.44f,.24f));Set(wave,"_tileFlashStrength",.13f);Set(wave,"_strikeColor",new Color(.68f,.48f,.23f,.7f));Set(wave,"_strikeHeight",3.5f);Set(wave,"_strikeWidth",.22f);Set(wave,"_strikeDuration",.85f);}
            var collapse=Find<MapCollapseManager>();if(collapse!=null){Set(collapse,"_shakeDuration",1.05f);Set(collapse,"_shakeMagnitude",.09f);}
            var presentation=Ensure<PresentationTheme>();Set(presentation,"_theme",theme);Set(presentation,"_revision",PresentationTheme.CurrentRevision);
            SceneSetupTool.RebuildPresentationSettings();
            StyleUI(theme);
            // Rebuilding settings preserves GameAudio but creates a fresh DisplaySettings binding.
            Bind(Find<GameSoundDirector>());Bind(Find<QuestRootCinematic>());
            EditorUtility.SetDirty(theme);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
        [MenuItem("TacticalRPG/Zemini Yaran Kokler ve Test Ekonomisi Kur")]
        public static void InstallRootRupture()
        {
            Directory.CreateDirectory(Root);Directory.CreateDirectory("Assets/Data/Config");AssetDatabase.Refresh();
            var roots=Find<BayterekQuestRoots>();if(roots!=null)Set(roots,"_styling",Find<BiomeTerrainStyling>());
            var dayNight=Find<DayNightCycle>();if(dayNight!=null && dayNight.TransitionSeconds<=.01f)Set(dayNight,"_transitionSeconds",2.4f);
            if(dayNight!=null)
            {
                var daylight=new SerializedObject(dayNight);var sun=daylight.FindProperty("_sun").objectReferenceValue as Light;
                if(sun!=null)
                {
                    RenderSettings.sun=sun;
                    // The original empty-scene template light survived FullSetup alongside its sun.
                    // Keep the object editable, but do not let it override night or light the units.
                    foreach(var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                        if(light!=sun&&light.type==LightType.Directional&&light.transform.parent==null&&light.name=="Directional Light")
                        {light.enabled=false;EditorUtility.SetDirty(light);}
                }
            }
            var testing=Asset<TestEconomySO>("Assets/Data/Config/TestEconomy.asset");
            Set(Find<EssenceWallet>(),"_testEconomy",testing);Set(Find<Inventory>(),"_testEconomy",testing);Set(Find<PlayerBuffs>(),"_testEconomy",testing);
            if(roots!=null&&roots.Config!=null)
            {
                // Migrate once; leave authored lighting, UI, fog and subsequent root tuning alone.
                if(roots.Config.EarthMaterial==null)
                {
                    Set(roots.Config,"_earthMaterial",Material("RootEarth","TacticalRPG/RootEarth",Color.white));
                    Set(roots.Config,"_radius",.83f);
                    if(Mathf.Approximately(roots.Config.GrowSeconds,.18f))Set(roots.Config,"_growSecondsPerTile",.32f);
                }
                if(roots.Config.RuptureDustPrefab==null)
                    Set(roots.Config,"_ruptureDustPrefab",RuptureDust(AssetDatabase.LoadAssetAtPath<Material>(Root+"/RootDust.mat")));
            }
            var marker=Find<PresentationTheme>();if(marker!=null)Set(marker,"_revision",PresentationTheme.CurrentRevision);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
        public static T Find<T>() where T:Component=>Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        private static T EnsureOn<T>(GameObject go) where T:Component {var component=go.GetComponent<T>();if(component==null)component=go.AddComponent<T>();return component;}
        private static T Ensure<T>() where T:Component
        {var found=Find<T>();if(found!=null)return found;var go=new GameObject(typeof(T).Name);var parent=GameObject.Find("TacticalRPG");if(parent!=null)go.transform.SetParent(parent.transform);return go.AddComponent<T>();}
        public static void Set(Object target,string name,object value)
        {
            if(target==null)return;var so=new SerializedObject(target);var p=so.FindProperty(name);if(p==null)throw new InvalidOperationException(target.name+" has no "+name);
            if(value is Object obj)p.objectReferenceValue=obj;else if(value is Color c)p.colorValue=c;else if(value is float f)p.floatValue=f;else if(value is int i)p.intValue=i;else if(value is bool b)p.boolValue=b;
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);
        }
        private static void Bind(Component component)
        {
            if(component==null)return;
            foreach(var field in component.GetType().GetFields(BindingFlags.NonPublic|BindingFlags.Instance))
                if(field.IsDefined(typeof(SerializeField))&&typeof(Component).IsAssignableFrom(field.FieldType)&&field.FieldType!=typeof(Canvas)&&!field.Name.EndsWith("Prefab",StringComparison.Ordinal))
                {var found=Object.FindFirstObjectByType(field.FieldType,FindObjectsInactive.Include);if(found!=null)Set(component,field.Name,found);}
        }
        private static T Asset<T>(string path) where T:ScriptableObject
        {var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset==null){asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}return asset;}
        private static Material Material(string name,string shader,Color color)
        {string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find(shader)){name=name,enableInstancing=true};m.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(m,path);}return m;}
        private static SoundLibrarySO BuildAudio()
        {
            const string folder="Assets/Audio/Original";
            var manifest=JsonUtility.FromJson<CueManifest>(File.ReadAllText(folder+"/manifest.json"));
            foreach(var guid in AssetDatabase.FindAssets("t:AudioClip",new[]{folder}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var importer=(AudioImporter)AssetImporter.GetAtPath(path);bool score=path.Contains("/Music/"),ambience=path.Contains("/Ambience/");
                var settings=importer.defaultSampleSettings;var type=score?AudioClipLoadType.Streaming:ambience?AudioClipLoadType.CompressedInMemory:AudioClipLoadType.DecompressOnLoad;
                if(settings.loadType==type&&Mathf.Approximately(settings.quality,.82f)&&importer.loadInBackground)continue;
                settings.loadType=type;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.82f;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings=settings;importer.loadInBackground=true;importer.SaveAndReimport();
            }
            var library=Asset<SoundLibrarySO>(Data+"/SoundLibrary.asset");
            var existingCues=new HashSet<string>();foreach(var cue in library.Cues)existingCues.Add(cue.id);
            var existingThemes=new HashSet<string>();foreach(var theme in library.Themes)existingThemes.Add(theme.id);
            var so=new SerializedObject(library);var cues=so.FindProperty("_cues");
            for(int i=0;i<manifest.cues.Length;i++)
            {
                var data=manifest.cues[i];if(existingCues.Contains(data.id))continue;
                int index=cues.arraySize;cues.arraySize++;var cue=cues.GetArrayElementAtIndex(index);cue.FindPropertyRelative("id").stringValue=data.id;
                cue.FindPropertyRelative("volume").floatValue=data.volume;cue.FindPropertyRelative("cooldown").floatValue=data.cooldown;
                bool ui=data.id.StartsWith("ui_")||data.id=="page_turn"||data.id=="scroll"||data.id=="item_drag"||data.id=="item_drop"||data.id=="card_pick";
                cue.FindPropertyRelative("bus").enumValueIndex=ui?1:0;cue.FindPropertyRelative("pitchVariation").floatValue=ui?.025f:.045f;cue.FindPropertyRelative("voiceLimit").intValue=data.id.StartsWith("root_")?2:3;
                var clips=cue.FindPropertyRelative("variants");clips.arraySize=3;for(int j=0;j<3;j++)clips.GetArrayElementAtIndex(j).objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>(folder+"/SFX/"+data.id+"_"+(j+1)+".wav");
            }
            string[] ids={"menu","yirtik_koru","halka_koyu","fisilti_batakligi","uyuyanlar_vadisi","oyuk_tepeler","bayterek_kalbi","kok_ahdi_korulari","combat","boss"};
            var themes=so.FindProperty("_themes");
            for(int i=0;i<ids.Length;i++){if(existingThemes.Contains(ids[i]))continue;int index=themes.arraySize;themes.arraySize++;var t=themes.GetArrayElementAtIndex(index);t.FindPropertyRelative("id").stringValue=ids[i];t.FindPropertyRelative("music").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>(folder+"/Music/"+ids[i]+".ogg");t.FindPropertyRelative("ambience").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>(folder+"/Ambience/"+(ids[i]=="boss"?"combat":ids[i]=="menu"?"kok_ahdi_korulari":ids[i])+".ogg");}
            so.ApplyModifiedPropertiesWithoutUndo();return library;
        }
        private static GameObject SurfacePrefab(string name,Material material,bool fog)
        {
            string meshPath=Root+"/"+name+".asset",prefabPath=Root+"/"+name+".prefab";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(mesh==null)
            {
                bool newMesh=mesh==null;
                if(newMesh)mesh=new Mesh{name=name};else mesh.Clear();var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
                const int sides=48,rings=7;
                for(int r=0;r<rings;r++)for(int s=0;s<=sides;s++)
                {
                    float t=r/(float)(rings-1),a=s*Mathf.PI*2/sides;
                    float radius=fog?1.001f:.99f;
                    {float sector=Mathf.Repeat(a,Mathf.PI/3)-Mathf.PI/6;radius*=Mathf.Cos(Mathf.PI/6)/Mathf.Cos(sector);}
                    vertices.Add(new Vector3(Mathf.Sin(a)*t*radius,0,Mathf.Cos(a)*t*radius));uv.Add(new Vector2(.5f+Mathf.Sin(a)*t*.5f,.5f+Mathf.Cos(a)*t*.5f));
                    if(r==rings-1||s==sides)continue;int k=r*(sides+1)+s;triangles.AddRange(new[]{k,k+1,k+sides+1,k+1,k+sides+2,k+sides+1});
                }
                mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();if(newMesh)AssetDatabase.CreateAsset(mesh,meshPath);else EditorUtility.SetDirty(mesh);
            }
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);if(prefab!=null)return prefab;
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            prefab=PrefabUtility.SaveAsPrefabAsset(go,prefabPath);Object.DestroyImmediate(go);return prefab;
        }
        private static ParticleSystem RootDust(Material material)
        {
            string path=Root+"/RootAwakening.prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab!=null)return prefab.GetComponent<ParticleSystem>();
            var go=new GameObject("Root soil and sap");go.SetActive(false);var dust=go.AddComponent<ParticleSystem>();
            var main=dust.main;main.playOnAwake=false;main.loop=true;main.duration=2;main.startLifetime=new ParticleSystem.MinMaxCurve(.7f,1.5f);main.startSpeed=new ParticleSystem.MinMaxCurve(.3f,1.1f);main.startSize=new ParticleSystem.MinMaxCurve(.05f,.20f);main.maxParticles=200;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(.31f,.22f,.12f,.5f),new Color(.8f,.61f,.25f,.65f));
            var emission=dust.emission;emission.rateOverTime=35;var shape=dust.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=30;shape.radius=.35f;shape.rotation=new Vector3(-90,0,0);
            var color=dust.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.15f),new GradientAlphaKey(0,1)});color.color=gradient;
            dust.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
            var lightGO=new GameObject("Bayterek awakening glow");lightGO.transform.SetParent(go.transform,false);var light=lightGO.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.65f,.22f);light.range=12;light.intensity=3;light.enabled=false;
            go.SetActive(true);prefab=PrefabUtility.SaveAsPrefabAsset(go,path);Object.DestroyImmediate(go);return prefab.GetComponent<ParticleSystem>();
        }
        private static ParticleSystem RuptureDust(Material material)
        {
            string path=Root+"/RootRuptureBurst.prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab!=null)return prefab.GetComponent<ParticleSystem>();
            var go=new GameObject("Toprak yarilmasi");go.SetActive(false);var dust=go.AddComponent<ParticleSystem>();
            var main=dust.main;main.playOnAwake=false;main.loop=false;main.duration=1;main.startLifetime=new ParticleSystem.MinMaxCurve(.35f,.85f);main.startSpeed=new ParticleSystem.MinMaxCurve(.9f,2.8f);main.startSize=new ParticleSystem.MinMaxCurve(.12f,.36f);main.maxParticles=32;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=.6f;
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(.38f,.28f,.16f,.5f),new Color(.64f,.52f,.32f,.4f));
            var emission=dust.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,20)});
            var shape=dust.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=45;shape.radius=.6f;shape.rotation=new Vector3(-90,0,0);
            var color=dust.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.6f,0),new GradientAlphaKey(.35f,.4f),new GradientAlphaKey(0,1)});color.color=gradient;
            dust.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;go.SetActive(true);prefab=PrefabUtility.SaveAsPrefabAsset(go,path);Object.DestroyImmediate(go);return prefab.GetComponent<ParticleSystem>();
        }
        private static Sprite RoundedSprite()
        {
            string path=Root+"/Panel.png";if(!File.Exists(path))
            {const int n=64;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);for(int y=0;y<n;y++)for(int x=0;x<n;x++){var q=new Vector2(Mathf.Abs(x-31.5f)-20,Mathf.Abs(y-31.5f)-20);float d=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-10;float a=1-Mathf.SmoothStep(-.5f,.5f,d);t.SetPixel(x,y,new Color(1,1,1,a));}t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);}
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spriteBorder=new Vector4(14,14,14,14);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static void StyleUI(PresentationThemeSO theme)
        {
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(canvas.name=="Brightness_Canvas")continue;
                foreach(var image in canvas.GetComponentsInChildren<Image>(true))
                {
                    string name=image.name.ToLowerInvariant();var rect=image.rectTransform;
                    string spriteName=image.sprite!=null?image.sprite.name.ToLowerInvariant():"";
                    bool paper=spriteName.Contains("paper")||spriteName.Contains("background")||spriteName=="panel";
                    bool icon=(name=="icon"||name=="portrait"||name=="bust"||name.Contains("mapimage")||name.StartsWith("pin_")||name=="swatch");
                    if(name=="inkframe"){image.gameObject.SetActive(false);continue;}
                    if(icon&&!paper){if(name=="icon")image.color=theme.accent;continue;}
                    bool panel=paper||name.StartsWith("panel_")||name.Contains("body")||name.Contains("background")||name=="fill"||name.Contains("frame")||name.Contains("slot")||image.GetComponent<Button>()!=null;
                    if(!panel)continue;
                    image.sprite=theme.panelSprite;image.type=Image.Type.Sliced;
                    image.color=name.StartsWith("panel_")?theme.background:image.GetComponent<Button>()!=null?theme.raised:theme.panel;
                    if(!name.StartsWith("panel_")&&rect.rect.width>400&&rect.rect.height>100)
                    {var shadow=EnsureOn<Shadow>(image.gameObject);shadow.effectColor=new Color(0,0,0,.28f);shadow.effectDistance=new Vector2(0,-5);}

                }
                foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true))
                {
                    if(text.name.EndsWith("Hint")){text.text="Kapat: Esc";text.rectTransform.anchoredPosition=new Vector2(-240,28);text.rectTransform.sizeDelta=new Vector2(600,40);}
                    text.raycastTarget=false;text.color=text.name.Contains("Title")||text.name.Contains("Header")?theme.accent:theme.text;
                    text.fontStyle=text.name.Contains("Title")||text.name.Contains("Header")?FontStyles.Bold:FontStyles.Normal;
                    if(text.text.Contains("LEVEL")&&text.text.Contains("EVR"))text.transform.parent.gameObject.SetActive(false);
                    text.characterSpacing=.6f;text.enableAutoSizing=true;text.fontSizeMax=text.fontSize;text.fontSizeMin=Mathf.Min(text.fontSize,16);
                }
                foreach(var selectable in canvas.GetComponentsInChildren<Selectable>(true))
                {
                    if(selectable.GetComponent<UISoundFeedback>()==null)selectable.gameObject.AddComponent<UISoundFeedback>();
                    var colors=selectable.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.18f,1.24f,1.2f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.7f,.83f,.78f);colors.disabledColor=new Color(.45f,.49f,.48f,.5f);colors.fadeDuration=.14f;selectable.colors=colors;
                    if(selectable is Slider slider){var hit=EnsureOn<Image>(slider.gameObject);hit.color=new Color(0,0,0,.001f);hit.raycastTarget=true;if(slider.fillRect!=null)slider.fillRect.GetComponent<Image>().color=theme.teal;if(slider.targetGraphic!=null){slider.targetGraphic.color=theme.accent;var thumb=(Image)slider.targetGraphic;thumb.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");thumb.type=Image.Type.Simple;thumb.rectTransform.sizeDelta=new Vector2(24,-18);}
                    var bg=slider.transform.Find("Background") as RectTransform;if(bg!=null){bg.anchorMin=new Vector2(0,.38f);bg.anchorMax=new Vector2(1,.62f);bg.offsetMin=bg.offsetMax=Vector2.zero;}
                    if(slider.fillRect!=null){var fill=slider.fillRect.parent as RectTransform;fill.anchorMin=new Vector2(0,.38f);fill.anchorMax=new Vector2(1,.62f);fill.offsetMin=new Vector2(8,0);fill.offsetMax=new Vector2(-8,0);}}
                }
            }
            foreach(var tree in Object.FindObjectsByType<UpgradeTreeView>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                Set(tree,"_lockedColor",theme.background);Set(tree,"_availableColor",theme.raised);Set(tree,"_unlockedColor",theme.teal);Set(tree,"_maxedColor",theme.accent);Set(tree,"_inkColor",theme.text);Set(tree,"_selectedColor",theme.accent);
                foreach(var disc in tree.GetComponentsInChildren<Image>(true))if(disc.name=="Disc"||disc.name=="LevelBadge"){disc.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");disc.type=Image.Type.Simple;}
                if(tree.transform.Find("ModernGraph")==null)
                {
                    var children=new List<Transform>();foreach(Transform child in tree.transform)if(child.name.StartsWith("Node_")||child.name=="Branches"||child.name.StartsWith("Group_"))children.Add(child);
                    var graph=new GameObject("ModernGraph",typeof(RectTransform)).GetComponent<RectTransform>();graph.SetParent(tree.transform,false);graph.anchorMin=graph.anchorMax=new Vector2(.5f,.5f);graph.sizeDelta=Vector2.zero;
                    foreach(var child in children)child.SetParent(graph,false);graph.anchoredPosition=new Vector2(25,-16);graph.localScale=new Vector3(.9f,.9f,1);graph.SetAsFirstSibling();
                }
            }
            foreach(var view in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(!(view is BagInventoryView)&&!(view is BagPotionView)&&!(view is CharacterBookView)&&!(view is BookmarkPager))continue;
                var so=new SerializedObject(view);foreach(var pair in new[]{("_ink",theme.text),("_inkSoft",theme.muted),("_paper",theme.panel),("_slotEmpty",theme.raised),("_selectedRow",theme.teal),("_slotDim",theme.background),("_slotOpen",theme.teal),("_slotAvailable",theme.raised),("_slotLocked",theme.background),("_activeColor",theme.teal),("_inactiveColor",theme.raised)}){var p=so.FindProperty(pair.Item1);if(p!=null)p.colorValue=pair.Item2;}
                var sprite=so.FindProperty("_cellSprite");if(sprite!=null)sprite.objectReferenceValue=theme.panelSprite;so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
    public static partial class SceneSetupTool { public static void SetupPresentation()=>PresentationSetup.Install(); }
}
