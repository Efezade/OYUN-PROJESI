using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Editor
{
    // Creates its OWN scene and assets. Never opens or saves xd.unity.
    public static class CombatLabSetup
    {
        public const string ScenePath = "Assets/Scenes/Prototypes/CombatComparison.unity";
        private const string Art = "Assets/Art/Prototypes/CombatLab";
        private const string Data = "Assets/Data/Prototypes/CombatLab.asset";
        private static Font _font;
        private static CombatLabConfig _config;
        private static Material _lineMaterial;

        public static void EnsureAssets()
        {
            Directory.CreateDirectory(Art); Directory.CreateDirectory("Assets/Data/Prototypes"); Directory.CreateDirectory("Assets/Scenes/Prototypes");
            AssetDatabase.Refresh();
            _config = AssetDatabase.LoadAssetAtPath<CombatLabConfig>(Data);
            if (_config == null) { _config = ScriptableObject.CreateInstance<CombatLabConfig>(); AssetDatabase.CreateAsset(_config, Data); }
        }
        [MenuItem("TacticalRPG/Prototipler/A-B Savas Laboratuvarini Ac", false, 300)]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureScene(); UpgradeLabVisuals(); EditorSceneManager.OpenScene(ScenePath); Selection.activeObject = AssetDatabase.LoadAssetAtPath<CombatLabConfig>(Data);
        }
        public static void EnsureScene()
        {
            EnsureAssets(); if (File.Exists(ScenePath)) return;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _lineMaterial = Material("Beam", _config.AccentColor, true);
            var prior = SceneManager.GetActiveScene();
            bool additive = prior.IsValid() && !string.IsNullOrEmpty(prior.path);
            if (!additive && prior.isDirty) throw new InvalidOperationException("Kaydedilmemiş sahne korunuyor. Önce sahneni kaydet.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            try
            {
                var cameraGo = new GameObject("Lab Camera", typeof(Camera), typeof(AudioListener));
                var camera = cameraGo.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 8.8f;
                camera.transform.position = new Vector3(0, 19, -14); camera.transform.LookAt(new Vector3(0, 0, 0));
                camera.backgroundColor = new Color(.07f, .11f, .14f); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                var light = new GameObject("Lab Sun", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.35f; light.transform.rotation = Quaternion.Euler(48, -30, 0); light.shadows = LightShadows.Soft;
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.66f, .73f, .8f);
                var arena = new GameObject("Shared Arena"); var simulation = new CombatLabSimulation(_config);
                var hexMesh = HexMesh(); var matA = Material("Ground_A", _config.GroundA); var matB = Material("Ground_B", _config.GroundB);
                var stone = Material("Pillar", new Color(.46f, .5f, .47f));
                foreach (var h in simulation.Cells)
                {
                    var tile = new GameObject("Hex_" + h.Q + "_" + h.R, typeof(MeshFilter), typeof(MeshRenderer)); tile.transform.SetParent(arena.transform);
                    var p = h.World(_config.TileRadius); tile.transform.position = new Vector3(p.x, 0, p.y);
                    tile.GetComponent<MeshFilter>().sharedMesh = hexMesh; tile.GetComponent<MeshRenderer>().sharedMaterial = (h.Q + h.R) % 2 == 0 ? matA : matB;
                    if (simulation.Blocked(h))
                    { Primitive("Occluder", PrimitiveType.Cylinder, tile.transform, new Vector3(0, 1.1f, 0), new Vector3(.9f, .8f, .9f), stone); }
                }
                var floor = Primitive("Floor", PrimitiveType.Plane, arena.transform, new Vector3(0, -.06f, 0), new Vector3(5, 1, 5), Material("Floor", new Color(.1f, .16f, .18f)));
                var root = new GameObject("Combat Lab — isolated prototype");
                var controller = root.AddComponent<CombatLabController>(); var view = root.AddComponent<CombatLabView>(); var audio = root.AddComponent<AudioSource>(); audio.playOnAwake = false;
                var actorRoot = new GameObject("Actors"); actorRoot.transform.SetParent(root.transform);
                var prefabs = new CombatLabActorView[6];
                for (int i = 0; i < prefabs.Length; i++) prefabs[i] = ActorPrefab((LabRole)i);
                var marker = FlatPrefab("MoveMarker", new Color(.23f, .54f, .4f), .32f);
                var pulse = FlatPrefab("Pulse", _config.AccentColor, 1f);
                var hover = UnityEngine.Object.Instantiate(FlatPrefab("Hover", new Color(.65f, .84f, .72f), .65f)); hover.transform.SetParent(root.transform); hover.SetActive(false);
                var line = LinePrefab(); var damage = DamagePrefab();
                var hud = BuildHud();
                Set(controller, "_config", _config); Set(controller, "_view", view); Set(controller, "_hud", hud);
                Set(view, "_camera", camera); Set(view, "_actorPrefabs", prefabs); Set(view, "_actorsRoot", actorRoot.transform); Set(view, "_moveMarkerPrefab", marker);
                Set(view, "_pulsePrefab", pulse); Set(view, "_linePrefab", line); Set(view, "_damagePrefab", damage); Set(view, "_hoverRing", hover.transform); Set(view, "_audio", audio);
                Set(view, "_hitClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Original/SFX/axe_1.wav"));
                Set(view, "_chainClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Original/SFX/card_pick_1.wav"));
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            }
            finally { if (additive) { if (prior.IsValid()) SceneManager.SetActiveScene(prior); EditorSceneManager.CloseScene(scene, true); } }
        }
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        { var so = new SerializedObject(target); var p = so.FindProperty(field); p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static Material Material(string name, Color color, bool unlit = false)
        {
            string path = Art + "/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path); if (mat != null) return mat;
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            mat = new Material(shader) { name = name }; mat.SetColor("_BaseColor", color); if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", .2f);
            AssetDatabase.CreateAsset(mat, path); return mat;
        }
        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        private static Mesh HexMesh()
        {
            string path = Art + "/Hex.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
            var vertices = new Vector3[14]; var triangles = new int[54]; float radius = _config.TileRadius * .96f;
            vertices[0] = new Vector3(0, .3f, 0); vertices[7] = Vector3.zero;
            for (int i = 0; i < 6; i++) { float angle = Mathf.Deg2Rad * (30 + i * 60); vertices[1 + i] = new Vector3(Mathf.Cos(angle) * radius, .3f, Mathf.Sin(angle) * radius); vertices[8 + i] = new Vector3(vertices[1 + i].x, 0, vertices[1 + i].z); }
            int index = 0;
            for (int i = 0; i < 6; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % 6;
                triangles[index++] = 0; triangles[index++] = b; triangles[index++] = a;
                triangles[index++] = a; triangles[index++] = b; triangles[index++] = a + 7;
                triangles[index++] = b; triangles[index++] = b + 7; triangles[index++] = a + 7;
            }
            var mesh = new Mesh { name = "Shared Lab Hex", vertices = vertices, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static GameObject FlatPrefab(string name, Color color, float size)
        {
            string path = Art + "/" + name + ".prefab"; var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (prefab != null) return prefab;
            var go = Primitive(name, PrimitiveType.Cylinder, null, Vector3.zero, new Vector3(size, .012f, size), Material(name, color, true));
            prefab = PrefabUtility.SaveAsPrefabAsset(go, path); UnityEngine.Object.DestroyImmediate(go); return prefab;
        }
        private static CombatLabActorView ActorPrefab(LabRole role)
        {
            string path = Art + "/Actor_" + role + ".prefab"; var existing = AssetDatabase.LoadAssetAtPath<CombatLabActorView>(path); if (existing != null) return existing;
            Color color = role == LabRole.Kam ? _config.KamColor : role == LabRole.Guard ? _config.GuardColor : role == LabRole.Archer ? _config.ArcherColor : _config.EnemyColor;
            var root = new GameObject("Lab " + role); var view = root.AddComponent<CombatLabActorView>();
            var body = new GameObject("Body"); body.transform.SetParent(root.transform, false);
            string modelPath = role switch {
                LabRole.Kam => "Assets/Art/Models/Characters/Kam/barbarkarakteri@Walking.fbx",
                LabRole.Guard => "Assets/Art/Models/Characters/Savasci/Warrior.fbx",
                LabRole.Archer => "Assets/Art/Models/Characters/Ranger/Ranger.fbx",
                LabRole.Gunner => "Assets/Art/Models/Characters/GoblinSaman/Orc_Skull.fbx",
                _ => "Assets/Art/Models/Characters/Goblin/Orc.fbx" };
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model != null)
            {
                var copy = UnityEngine.Object.Instantiate(model, body.transform); copy.name = "Existing model";
                foreach (var component in copy.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(component);
                foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var animator in copy.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                var renders = copy.GetComponentsInChildren<Renderer>();
                if (renders.Length > 0)
                {
                    Bounds bounds = renders[0].bounds; foreach (var render in renders) bounds.Encapsulate(render.bounds);
                    float scale = 1.5f / Mathf.Max(bounds.size.y, .01f); copy.transform.localScale *= scale;
                    bounds = renders[0].bounds; foreach (var render in renders) bounds.Encapsulate(render.bounds);
                    copy.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                }
            }
            else Primitive("Test Body", PrimitiveType.Capsule, body.transform, new Vector3(0, .8f, 0), new Vector3(.5f, .7f, .5f), Material("Actor_" + role, color));
            Primitive("Team", PrimitiveType.Cylinder, root.transform, new Vector3(0, .02f, 0), new Vector3(.9f, .025f, .9f), Material("Team_" + role, color, true));
            var selection = Primitive("Selection", PrimitiveType.Cylinder, root.transform, new Vector3(0, .008f, 0), new Vector3(1.16f, .008f, 1.16f), Material("Select", Color.white, true));
            var mark = Primitive("Mark", PrimitiveType.Sphere, root.transform, new Vector3(0, 1.9f, 0), Vector3.one * .16f, Material("Mark", _config.AccentColor, true));
            var healthBack = Primitive("Health Back", PrimitiveType.Cube, root.transform, new Vector3(0, 1.75f, 0), new Vector3(1.05f, .1f, .1f), Material("HealthBack", new Color(.04f, .06f, .08f), true));
            var health = Primitive("Health", PrimitiveType.Cube, root.transform, new Vector3(0, 1.76f, -.05f), new Vector3(1, .07f, .07f), Material("Health_" + role, color, true));
            var caption = new GameObject("Caption", typeof(TextMesh)).GetComponent<TextMesh>(); caption.transform.SetParent(root.transform); caption.font = _font; caption.fontSize = 34; caption.characterSize = .045f; caption.anchor = TextAnchor.MiddleCenter; caption.alignment = TextAlignment.Center; caption.color = Color.white; caption.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            var warning = Primitive("Attack Telegraph", PrimitiveType.Cylinder, root.transform, Vector3.zero, new Vector3(1.7f, .025f, 1.7f), Material("Danger", new Color(.78f, .18f, .13f), true)); warning.SetActive(false);
            Set(view, "_body", body.transform); Set(view, "_healthFill", health.transform); Set(view, "_caption", caption); Set(view, "_selection", selection); Set(view, "_mark", mark); Set(view, "_warning", warning.transform);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root); return prefab.GetComponent<CombatLabActorView>();
        }
        private static LineRenderer LinePrefab()
        {
            string path = Art + "/HitLine.prefab"; var existing = AssetDatabase.LoadAssetAtPath<LineRenderer>(path); if (existing != null) return existing;
            var go = new GameObject("HitLine", typeof(LineRenderer)); var line = go.GetComponent<LineRenderer>(); line.positionCount = 2; line.useWorldSpace = true; line.widthMultiplier = .055f; line.sharedMaterial = _lineMaterial;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path); UnityEngine.Object.DestroyImmediate(go); return prefab.GetComponent<LineRenderer>();
        }
        private static TextMesh DamagePrefab()
        {
            string path = Art + "/Damage.prefab"; var existing = AssetDatabase.LoadAssetAtPath<TextMesh>(path); if (existing != null) return existing;
            var go = new GameObject("Damage", typeof(TextMesh)); var text = go.GetComponent<TextMesh>(); text.font = _font; text.fontSize = 42; text.characterSize = .06f; text.anchor = TextAnchor.MiddleCenter; text.fontStyle = FontStyle.Bold; text.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path); UnityEngine.Object.DestroyImmediate(go); return prefab.GetComponent<TextMesh>();
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = position; rt.sizeDelta = size; return rt;
        }
        private static GameObject Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        { var rt = Rect(name, parent, anchor, position, size); rt.gameObject.AddComponent<Image>().color = _config.PanelColor; return rt.gameObject; }
        private static Text Text(string name, Transform parent, Vector2 position, Vector2 size, string content, int fontSize = 18, TextAnchor align = TextAnchor.UpperLeft)
        {
            var rt = Rect(name, parent, new Vector2(.5f, .5f), position, size); var text = rt.gameObject.AddComponent<Text>(); text.font = _font; text.fontSize = fontSize; text.text = content; text.color = _config.TextColor; text.alignment = align; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        private static Button Button(string name, Transform parent, Vector2 position, Vector2 size, string content, out Text label)
        {
            var rt = Rect(name, parent, new Vector2(.5f, .5f), position, size); rt.gameObject.AddComponent<Image>().color = new Color(.13f, .26f, .29f);
            var button = rt.gameObject.AddComponent<Button>(); var colors = button.colors; colors.highlightedColor = new Color(.6f, 1f, .9f); colors.disabledColor = new Color(.4f, .45f, .48f); button.colors = colors;
            label = Text("Label", rt, Vector2.zero, size - new Vector2(10, 6), content, 17, TextAnchor.MiddleCenter); return button;
        }
        private static CombatLabHud BuildHud()
        {
            var canvasGo = new GameObject("Combat Lab UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1440, 900); scaler.matchWidthOrHeight = .5f;
            var hud = canvasGo.AddComponent<CombatLabHud>();
            var game = Rect("Game", canvasGo.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1440, 900));
            var top = Panel("Top", game, new Vector2(.5f, 1), new Vector2(0, -48), new Vector2(1408, 74));
            var title = Text("Title", top.transform, new Vector2(-480, 0), new Vector2(390, 48), "", 24, TextAnchor.MiddleLeft);
            Text("Subtitle", top.transform, new Vector2(20, 0), new Vector2(450, 50), "AYNI EKİP · AYNI ARENA · AYNI DALGALAR\nÇalışma prototipi — final sanat / denge değil", 16, TextAnchor.MiddleCenter);
            var other = Button("Switch", top.transform, new Vector2(430, 0), new Vector2(160, 46), "Diğer yaklaşım", out _);
            var menuButton = Button("Menu", top.transform, new Vector2(610, 0), new Vector2(140, 46), "Menü", out _);
            var left = Panel("Left", game, new Vector2(0, .5f), new Vector2(135, 25), new Vector2(240, 610));
            var status = Text("Status", left.transform, new Vector2(0, 230), new Vector2(210, 120), "", 19);
            var roster = Text("Roster", left.transform, new Vector2(0, 65), new Vector2(210, 200), "", 16);
            var controls = Text("Controls", left.transform, new Vector2(0, -170), new Vector2(210, 250), "", 15);
            var right = Panel("Right", game, new Vector2(1, .5f), new Vector2(-135, 90), new Vector2(240, 480));
            var comparison = Text("Comparison", right.transform, Vector2.zero, new Vector2(210, 450), "", 16);
            var bottom = Panel("Bottom", game, new Vector2(.5f, 0), new Vector2(0, 84), new Vector2(1408, 144));
            var hint = Text("Hint", bottom.transform, new Vector2(0, 47), new Vector2(1360, 36), "", 18, TextAnchor.MiddleLeft);
            var units = new Button[3]; string[] names = { "1 · Kam", "2 · Muhafız", "3 · Okçu" };
            for (int i = 0; i < 3; i++) units[i] = Button(names[i], bottom.transform, new Vector2(-620 + i * 126, -17), new Vector2(118, 52), names[i], out _);
            var pull = Button("Pull", bottom.transform, new Vector2(-155, -17), new Vector2(190, 52), "", out var pullLabel);
            var burst = Button("Burst", bottom.transform, new Vector2(50, -17), new Vector2(190, 52), "", out var burstLabel);
            var end = Button("End", bottom.transform, new Vector2(245, -17), new Vector2(180, 52), "SPACE · Turu bitir", out _);
            var pause = Button("Pause", bottom.transform, new Vector2(425, -17), new Vector2(150, 52), "Duraklat", out var pauseLabel);
            var restart = Button("Restart", bottom.transform, new Vector2(600, -17), new Vector2(170, 52), "Baştan dene", out _);
            var choices = Panel("Augments", canvasGo.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(840, 300));
            Text("Heading", choices.transform, new Vector2(0, 95), new Vector2(780, 60), "BİR GELİŞTİRME SEÇ\nİki yaklaşımda da aynı seçenekler", 23, TextAnchor.MiddleCenter);
            string[] options = { "ZİNCİR USTASI\nPatlama zincirleri +2 hasar", "AKIŞ\nHer zincir +1 mana\nMana şimdi tamamen dolar", "TOPARLANMA\nEkip +4 azami can\n+10 iyileşme; düşeni kaldırır" };
            var augments = new Button[3]; for (int i = 0; i < 3; i++) augments[i] = Button("Choice " + i, choices.transform, new Vector2(-270 + i * 270, -25), new Vector2(250, 130), options[i], out _);
            var result = Panel("Result", canvasGo.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(830, 600));
            var resultText = Text("Summary", result.transform, new Vector2(0, 65), new Vector2(760, 400), "", 21, TextAnchor.UpperCenter);
            var ratings = new Button[5]; for (int i = 0; i < 5; i++) ratings[i] = Button("Rating " + i, result.transform, new Vector2(-140 + i * 70, -170), new Vector2(60, 44), (i + 1).ToString(), out _);
            var resultOther = Button("Other", result.transform, new Vector2(-170, -245), new Vector2(310, 50), "Diğer yaklaşımı dene", out _);
            var resultRestart = Button("Again", result.transform, new Vector2(170, -245), new Vector2(310, 50), "Aynısını tekrar dene", out _);
            var menu = Panel("Menu", canvasGo.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1050, 630));
            Text("Title", menu.transform, new Vector2(0, 240), new Vector2(950, 80), "KAM / SAVAŞ LABORATUVARI", 34, TextAnchor.MiddleCenter);
            Text("Intro", menu.transform, new Vector2(0, 125), new Vector2(920, 130), "İki yaklaşımı aynı ekiple karşılaştır.\nKam + muhafız + okçu · 3 dalga · 2 geliştirme seçimi\n\nÖz toplama, ekonomi, hikâye ve ana oyun kaydı bu denemede yok.", 21, TextAnchor.MiddleCenter);
            var modes = new Button[2]; modes[0] = Button("Tactical", menu.transform, new Vector2(-245, -50), new Vector2(450, 150), "A / HIZLI TAKTİK\nBirlikleri istediğin sırayla yönet.\nHareket + eylem; zinciri sen kur.", out _);
            modes[1] = Button("Realtime", menu.transform, new Vector2(245, -50), new Vector2(450, 150), "B / GERÇEK ZAMAN\nKam'ı WASD ile yönet.\nBirlikler destekler; Q/E ile zincir kur.", out _);
            Text("Notes", menu.transform, new Vector2(0, -220), new Vector2(930, 110), "İlk tur A→B, ikinci tur B→A önerilir.\nKırmızı alan saldırı hazırlığıdır; tur modunda düşman menziline dikkat et.\nHer dalga sonrası yaşayan ekip +4 can alır. Finalde tekrar oynama isteğini puanla.", 18, TextAnchor.MiddleCenter);
            Set(hud, "_menu", menu); Set(hud, "_game", game.gameObject); Set(hud, "_choices", choices); Set(hud, "_result", result);
            Set(hud, "_title", title); Set(hud, "_status", status); Set(hud, "_roster", roster); Set(hud, "_controls", controls); Set(hud, "_hint", hint); Set(hud, "_comparison", comparison); Set(hud, "_resultText", resultText);
            Set(hud, "_pauseText", pauseLabel); Set(hud, "_pullLabel", pullLabel); Set(hud, "_burstLabel", burstLabel);
            Set(hud, "_modeButtons", modes); Set(hud, "_unitButtons", units); Set(hud, "_augmentButtons", augments); Set(hud, "_ratingButtons", ratings);
            Set(hud, "_pull", pull); Set(hud, "_burst", burst); Set(hud, "_endTurn", end); Set(hud, "_pause", pause); Set(hud, "_restart", restart); Set(hud, "_otherMode", other); Set(hud, "_menuButton", menuButton); Set(hud, "_resultOther", resultOther); Set(hud, "_resultRestart", resultRestart);
            choices.SetActive(false); result.SetActive(false); game.gameObject.SetActive(false); return hud;
        }
        public static void BuildWindows()
        {
            EnsureScene(); UpgradeLabVisuals(); CombatLabValidation.RunChecks();
            Directory.CreateDirectory("Builds/CombatLab");
            var options = new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/CombatLab/KamCombatLab.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("CombatLab Windows build failed: " + report.summary.result);
            Debug.Log("COMBAT_LAB_BUILD_OK " + report.summary.totalSize);
        }
        // Reuses the existing idle/walk controllers and textures in our own prefabs.
        // No source model, original controller or campaign scene is edited.
        private static void UpgradeLabVisuals()
        {
            string[] folders = { "Kam", "Savasci", "Ranger", "Goblin", "GoblinSaman", "Goblin" };
            for (int i = 0; i < folders.Length; i++)
            {
                string path = Art + "/Actor_" + (LabRole)i + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var animator = root.GetComponentInChildren<Animator>(true);
                    var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Art/Models/Characters/" + folders[i] + "/" + folders[i] + "Animator.controller");
                    if (animator != null && controller != null)
                    {
                        animator.enabled = true; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                        Set(root.GetComponent<CombatLabActorView>(), "_animator", animator);
                    }
                    if (i == 1 || i == 2)
                    {
                        string name = i == 1 ? "Warrior" : "Ranger";
                        foreach (var renderer in root.transform.Find("Body").GetComponentsInChildren<Renderer>(true))
                        {
                            var materials = renderer.sharedMaterials;
                            for (int j = 0; j < materials.Length; j++)
                            {
                                string oldName = materials[j] == null ? "" : materials[j].name;
                                string part = oldName.IndexOf("sword", StringComparison.OrdinalIgnoreCase) >= 0 ? "_Sword" : oldName.IndexOf("bow", StringComparison.OrdinalIgnoreCase) >= 0 ? "_Bow" : "";
                                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Models/Characters/" + folders[i] + "/" + name + part + "_Texture.png");
                                if (texture == null) continue;
                                var material = Material("Model_" + name + part, Color.white); material.SetTexture("_BaseMap", texture); EditorUtility.SetDirty(material); materials[j] = material;
                            }
                            renderer.sharedMaterials = materials;
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            bool dirty = false;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() == null)
                    { camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); dirty = true; }
            if (dirty) EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
        }
    }
}
