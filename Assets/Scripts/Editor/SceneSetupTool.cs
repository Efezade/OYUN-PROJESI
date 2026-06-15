using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using TacticalRPG.Grid;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.UI;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// TacticalRPG sahne kurulum araçları.
    ///
    /// Önerilen çalıştırma sırası:
    ///   1. "0 — Sahneyi Tamamen Temizle"
    ///   2. "Faz 0 — Temel Sahne Ogeleri"
    ///   3. "Faz 1.1 — HexGrid ve FogOfWar"
    /// </summary>
    public static class SceneSetupTool
    {
        // Her fazın sahiplendiği kök obje adı
        private const string SceneRootName   = "[TacticalRPG_Scene]";
        private const string SystemsRootName = "[TacticalRPG_Systems]";

        private const string MaterialsPath   = "Assets/Art/Materials";
        private const string PrefabsGridPath = "Assets/Prefabs/Grid";

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: 0 — Tüm sahneyi temizle
        // ─────────────────────────────────────────────────────────────────────

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: TANI — Sahne durumunu logla
        // ─────────────────────────────────────────────────────────────────────

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: Faz 2.1 — Karakter Sistemi + Öz Ekonomisi + Kam Mana
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 2.1 - Karakter Sistemi ve Oz Ekonomisi")]
        public static void SetupPhase21()
        {
            GameObject systemsRoot = GameObject.Find(SystemsRootName);
            GameObject sceneRoot   = GameObject.Find(SceneRootName);
            if (systemsRoot == null)
            {
                EditorUtility.DisplayDialog("Hata", "Önce Faz 1.1–1.5'i çalıştırın!", "Tamam");
                return;
            }

            ActionPointManager apManager = FindComponentAnywhere<ActionPointManager>();
            if (apManager == null)
            {
                EditorUtility.DisplayDialog("Hata", "ActionPointManager bulunamadı! Faz 1.4'ü çalıştırın.", "Tamam");
                return;
            }

            // ── SO Klasörleri ─────────────────────────────────────────────────
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Characters");

            // ── 3 Karakter SO oluştur ─────────────────────────────────────────
            CharacterClassData kamData = GetOrCreateCharacterSO(
                path      : "Assets/Data/Characters/Kam.asset",
                className : "Kam",
                lore      : "Kadim büyü bilgeliği ile donanmış gizemli şaman. Öz ve mana arasındaki köprü.",
                maxHP     : 8,
                attack    : 4,
                defense   : 1,
                moveRange : 3,
                essenceCosts    : new[] { 0, 5, 12 },
                hpMult          : new[] { 1f, 1.25f, 1.6f },
                atkMult         : new[] { 1f, 1.3f,  1.7f },
                defMult         : new[] { 1f, 1.1f,  1.3f },
                hasMana         : true,
                maxMana         : 10);

            CharacterClassData warriorData = GetOrCreateCharacterSO(
                path      : "Assets/Data/Characters/Savascı.asset",
                className : "Savaşçı",
                lore      : "Kılıç ustası. Ön saflarda durur, darbeleri göğüsler.",
                maxHP     : 14,
                attack    : 5,
                defense   : 3,
                moveRange : 3,
                essenceCosts    : new[] { 0, 6, 14 },
                hpMult          : new[] { 1f, 1.35f, 1.75f },
                atkMult         : new[] { 1f, 1.2f,  1.5f  },
                defMult         : new[] { 1f, 1.25f, 1.6f  },
                hasMana         : false,
                maxMana         : 0);

            CharacterClassData rangerData = GetOrCreateCharacterSO(
                path      : "Assets/Data/Characters/Ranger.asset",
                className : "Ranger",
                lore      : "Uzak mesafe uzmanı. Görünmez olur, ince stratejiler kurar.",
                maxHP     : 10,
                attack    : 6,
                defense   : 1,
                moveRange : 4,
                essenceCosts    : new[] { 0, 5, 11 },
                hpMult          : new[] { 1f, 1.2f,  1.55f },
                atkMult         : new[] { 1f, 1.25f, 1.6f  },
                defMult         : new[] { 1f, 1.1f,  1.25f },
                hasMana         : false,
                maxMana         : 0);

            // ── GameManager objesi ────────────────────────────────────────────
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : systemsRoot.transform.Find("GameManager")?.gameObject;

            if (gameManagerGO == null)
            {
                gameManagerGO = new GameObject("GameManager");
                gameManagerGO.transform.SetParent(systemsRoot.transform);
            }

            // ── EssenceManager ────────────────────────────────────────────────
            var existingEM = gameManagerGO.GetComponent<EssenceManager>();
            if (existingEM != null) Object.DestroyImmediate(existingEM);

            EssenceManager essManager = gameManagerGO.AddComponent<EssenceManager>();
            var emSO = new SerializedObject(essManager);
            emSO.FindProperty("_startingEssence").intValue = 0;
            emSO.FindProperty("_maxEssence").intValue      = 99;
            emSO.ApplyModifiedProperties();

            // ── PartyManager ──────────────────────────────────────────────────
            var existingPM = gameManagerGO.GetComponent<PartyManager>();
            if (existingPM != null) Object.DestroyImmediate(existingPM);

            PartyManager partyManager = gameManagerGO.AddComponent<PartyManager>();
            var pmSO = new SerializedObject(partyManager);
            pmSO.FindProperty("_essenceManager").objectReferenceValue = essManager;
            var classList = pmSO.FindProperty("_startingClasses");
            classList.ClearArray();
            classList.arraySize = 3;
            classList.GetArrayElementAtIndex(0).objectReferenceValue = kamData;
            classList.GetArrayElementAtIndex(1).objectReferenceValue = warriorData;
            classList.GetArrayElementAtIndex(2).objectReferenceValue = rangerData;
            pmSO.ApplyModifiedProperties();

            // ── KamManaManager ────────────────────────────────────────────────
            var existingKam = gameManagerGO.GetComponent<KamManaManager>();
            if (existingKam != null) Object.DestroyImmediate(existingKam);

            KamManaManager kamMana = gameManagerGO.AddComponent<KamManaManager>();
            var kamSO = new SerializedObject(kamMana);
            kamSO.FindProperty("_apManager").objectReferenceValue = apManager;
            kamSO.FindProperty("_maxMana").intValue               = 10;
            kamSO.FindProperty("_manaRegenPerSlot").intValue      = 2;
            kamSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TacticalRPG] Faz 2.1 tamamlandı.");
            EditorUtility.DisplayDialog(
                "Faz 2.1 Tamamlandı! — Karakter Sistemi",
                "Oluşturulanlar:\n\n" +
                "  • Kam (8 HP, 10 mana, büyücü)\n" +
                "  • Savaşçı (14 HP, 3 zırh, tank)\n" +
                "  • Ranger (10 HP, 6 atak, hız4)\n" +
                "  • EssenceManager — öz ekonomisi\n" +
                "  • PartyManager — parti yönetimi\n" +
                "  • KamManaManager — 10 mana, +2/dilim regen\n\n" +
                "Assets/Data/Characters/ klasöründe 3 SO var.\n" +
                "Console'da 'EssenceManager.Gain(5)' ile öz test edebilirsin.",
                "Tamam");
        }

        private static CharacterClassData GetOrCreateCharacterSO(
            string path, string className, string lore,
            int maxHP, int attack, int defense, int moveRange,
            int[] essenceCosts, float[] hpMult, float[] atkMult, float[] defMult,
            bool hasMana, int maxMana)
        {
            CharacterClassData so = AssetDatabase.LoadAssetAtPath<CharacterClassData>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<CharacterClassData>();
                AssetDatabase.CreateAsset(so, path);
            }

            var s = new SerializedObject(so);
            s.FindProperty("_className").stringValue = className;
            s.FindProperty("_lore").stringValue      = lore;
            s.FindProperty("_maxHP").intValue        = maxHP;
            s.FindProperty("_attack").intValue       = attack;
            s.FindProperty("_defense").intValue      = defense;
            s.FindProperty("_moveRange").intValue    = moveRange;
            s.FindProperty("_hasManaSystem").boolValue = hasMana;
            s.FindProperty("_maxMana").intValue        = maxMana;

            SetIntArray(s,   "_essenceCostPerLevel",   essenceCosts);
            SetFloatArray(s, "_hpMultiplierPerLevel",  hpMult);
            SetFloatArray(s, "_atkMultiplierPerLevel", atkMult);
            SetFloatArray(s, "_defMultiplierPerLevel", defMult);

            s.ApplyModifiedProperties();
            EditorUtility.SetDirty(so);
            return so;
        }

        private static void SetIntArray(SerializedObject so, string propName, int[] values)
        {
            var prop = so.FindProperty(propName);
            prop.ClearArray();
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).intValue = values[i];
        }

        private static void SetFloatArray(SerializedObject so, string propName, float[] values)
        {
            var prop = so.FindProperty(propName);
            prop.ClearArray();
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        [MenuItem("TacticalRPG/FIX - Kamerayı URP ile Düzelt")]
        public static void FixCameraURP()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                // Tag'e göre bul
                GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
                if (camGO != null) cam = camGO.GetComponent<Camera>();
            }

            if (cam == null)
            {
                EditorUtility.DisplayDialog("Hata", "Main Camera bulunamadı! Faz 0'ı çalıştır.", "Tamam");
                return;
            }

            // Zaten varsa ekleme
            if (cam.gameObject.GetComponent<UniversalAdditionalCameraData>() != null)
            {
                EditorUtility.DisplayDialog("Zaten Var", "UniversalAdditionalCameraData zaten mevcut.", "Tamam");
                return;
            }

            var urpData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderShadows        = false;
            urpData.requiresColorTexture = false;
            urpData.requiresDepthTexture = false;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[TacticalRPG] Kamera URP düzeltmesi uygulandı.");
            EditorUtility.DisplayDialog(
                "Kamera Düzeltildi!",
                "UniversalAdditionalCameraData eklendi.\n\n" +
                "Ctrl+S ile kaydet, sonra Play'e bas.\n" +
                "Artık hex karolar render edilecek.",
                "Tamam");
        }

        [MenuItem("TacticalRPG/TEST - Tum Karolari Beyaz Yap (Play modunda)")]
        public static void ForceAllVisible()
        {
            // Play modunda çalıştır — tüm karoları beyaz yapar, fog devre dışı
            HexGridManager grid = FindComponentAnywhere<HexGridManager>();
            if (grid == null || grid.Cells == null)
            {
                Debug.LogError("[TEST] HexGridManager bulunamadı veya grid boş! Önce Play'e bas.");
                return;
            }

            int count = 0;
            foreach (var cell in grid.Cells.Values)
            {
                if (cell.MeshRenderer != null)
                {
                    // Beyaz inline materyal — shader sorununu da bypass eder
                    cell.MeshRenderer.material.color = Color.white;
                    count++;
                }
            }
            Debug.Log($"[TEST] {count} karonun rengi beyaza çevrildi. Ekranda görüntü var mı?");
        }

        [MenuItem("TacticalRPG/TANI - Sahne Kontrolu (Console a Bak)")]
        public static void DiagnoseScene()
        {
            Debug.Log("========== TacticalRPG TANI BAŞLIYOR ==========");

            // 1. Kök objeler
            GameObject sceneRoot   = GameObject.Find(SceneRootName);
            GameObject systemsRoot = GameObject.Find(SystemsRootName);
            Debug.Log($"[TANI] SceneRoot: {(sceneRoot != null ? "VAR" : "YOK")}");
            Debug.Log($"[TANI] SystemsRoot: {(systemsRoot != null ? "VAR" : "YOK")}");

            // 2. Kamera
            Camera cam = Camera.main;
            if (cam != null)
            {
                var urpComp = cam.gameObject.GetComponent<UniversalAdditionalCameraData>();
                Debug.Log($"[TANI] Camera: pos={cam.transform.position} rot={cam.transform.eulerAngles} " +
                          $"ortho={cam.orthographic} size={cam.orthographicSize} " +
                          $"near={cam.nearClipPlane} far={cam.farClipPlane} " +
                          $"URP={urpComp != null} cullingMask={cam.cullingMask}");
            }
            else Debug.LogError("[TANI] Camera.main YOK!");

            if (systemsRoot == null) { Debug.LogError("[TANI] SystemsRoot YOK — Faz 1.1 çalıştırılmadı!"); return; }

            // 3. HexGridManager
            HexGridManager grid = systemsRoot.GetComponentInChildren<HexGridManager>();
            Debug.Log($"[TANI] HexGridManager: {(grid != null ? "VAR" : "YOK")}");
            if (grid != null)
            {
                var cellCount = grid.Cells?.Count ?? -1;
                Debug.Log($"[TANI] Hücre sayısı: {cellCount}  (beklenen: 100)");

                if (grid.Cells != null && grid.Cells.Count > 0)
                {
                    int nullMR = 0, nullMesh = 0, disabledMR = 0, nullMat = 0;
                    int hidden = 0, explored = 0, visible = 0;

                    foreach (var c in grid.Cells.Values)
                    {
                        var mr = c.MeshRenderer;
                        if (mr == null) { nullMR++; continue; }
                        if (!mr.enabled) disabledMR++;
                        if (mr.sharedMaterial == null) nullMat++;

                        var mf = c.Visual?.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh == null) nullMesh++;

                        switch (c.FogState)
                        {
                            case FogState.Hidden:   hidden++;   break;
                            case FogState.Explored: explored++; break;
                            case FogState.Visible:  visible++;  break;
                        }
                    }
                    Debug.Log($"[TANI] MeshRenderer null:{nullMR}  disabled:{disabledMR}  nullMat:{nullMat}  nullMesh:{nullMesh}");
                    Debug.Log($"[TANI] FogState → Hidden:{hidden}  Explored:{explored}  Visible:{visible}");

                    if (nullMR > 0)
                        Debug.LogError($"[TANI] KRİTİK: {nullMR} karonun MeshRenderer'ı NULL! → 'FIX - Karolari Yenile' çalıştır!");
                    if (nullMesh > 0)
                        Debug.LogError($"[TANI] KRİTİK: {nullMesh} karonun mesh referansı NULL (prefab GUID kırık)! → 'FIX - Karolari Yenile' çalıştır!");
                    if (visible == 0)
                        Debug.LogWarning("[TANI] SORUN: Visible karo yok! RevealArea çalışmadı.");
                }

                // Hex_0_0 detay incelemesi
                var firstCoord = new HexCoordinate(0, 0);
                if (grid.TryGetCell(firstCoord, out HexCell firstCell))
                {
                    var mf = firstCell.Visual?.GetComponent<MeshFilter>();
                    var mr = firstCell.MeshRenderer;
                    Debug.Log($"[TANI] Hex(0,0) detay: " +
                              $"Visual={firstCell.Visual != null} " +
                              $"MR={mr != null} " +
                              $"MR.enabled={mr?.enabled} " +
                              $"mesh={(mf?.sharedMesh != null ? mf.sharedMesh.name : "NULL")} " +
                              $"mat={(mr?.sharedMaterial != null ? mr.sharedMaterial.name : "NULL")} " +
                              $"layer={firstCell.Visual?.layer}");
                }
            }

            // 4. FogOfWarManager
            FogOfWarManager fog = systemsRoot.GetComponentInChildren<FogOfWarManager>();
            Debug.Log($"[TANI] FogOfWarManager: {(fog != null ? "VAR" : "YOK")}");

            // 5. PlayerController
            PlayerController player = systemsRoot.GetComponentInChildren<PlayerController>();
            Debug.Log($"[TANI] PlayerController: {(player != null ? $"VAR — konum:{player.CurrentCoord}" : "YOK")}");

            // 6. MapInputHandler
            MapInputHandler input = FindComponentAnywhere<MapInputHandler>();
            Debug.Log($"[TANI] MapInputHandler: {(input != null ? "VAR" : "YOK")}");

            // 7. HexGrid_Visuals alt obje sayısı
            Transform gridParent = grid != null ? grid.transform.Find("HexGrid_Visuals") : null;
            int childCount = gridParent != null ? gridParent.childCount : -1;
            Debug.Log($"[TANI] HexGrid_Visuals alt obje: {childCount}");

            Debug.Log("========== TANI BİTTİ ==========");
            EditorUtility.DisplayDialog("Tanı tamamlandı",
                "Console sekmesine bak:\n\n" +
                "Kırmızı = kritik sorun\n" +
                "Sarı = uyarı\n\n" +
                "Hex(0,0) detayı kritik — mesh ve material durumunu gösterir.",
                "Tamam");
        }

        [MenuItem("TacticalRPG/FIX - Karolari Yenile (Play modunda)")]
        public static void FixHexTiles()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Hata", "Bu araç yalnızca PLAY modunda çalışır!\nPlay'e bas, sonra tekrar çalıştır.", "Tamam");
                return;
            }

            HexGridManager grid = FindComponentAnywhere<HexGridManager>();
            if (grid == null || grid.Cells == null)
            {
                Debug.LogError("[FIX] HexGridManager veya Cells bulunamadı!");
                return;
            }

            Shader urpShader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Standard");

            // Renk tanımları
            Material hiddenMat   = new Material(urpShader); hiddenMat.color = new Color(0.22f, 0.18f, 0.28f);
            Material exploredMat = new Material(urpShader); exploredMat.color = new Color(0.25f, 0.25f, 0.25f);
            Material visibleMat  = new Material(urpShader); visibleMat.color = Color.white;

            if (hiddenMat.HasProperty("_BaseColor"))
            {
                hiddenMat.SetColor("_BaseColor",   new Color(0.22f, 0.18f, 0.28f));
                exploredMat.SetColor("_BaseColor", new Color(0.25f, 0.25f, 0.25f));
                visibleMat.SetColor("_BaseColor",  Color.white);
            }

            int rebuilt = 0;
            Transform parent = grid.transform.Find("HexGrid_Visuals") ?? grid.transform;

            foreach (var cell in grid.Cells.Values)
            {
                bool needsRebuild = cell.Visual == null
                                 || cell.MeshRenderer == null
                                 || !cell.MeshRenderer.enabled
                                 || cell.Visual.GetComponent<MeshFilter>()?.sharedMesh == null;

                if (!needsRebuild) continue;

                // Eski Visual temizle
                if (cell.Visual != null) Object.Destroy(cell.Visual);

                // Yeni prosedürel hex tile yarat
                var go = new GameObject($"Hex_{cell.Coordinate}");
                go.transform.SetParent(parent);
                go.transform.position = cell.WorldPosition;

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = HexMetrics.CreateHexMesh(0.95f);

                var mr = go.AddComponent<MeshRenderer>();

                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;

                cell.Visual       = go;
                cell.MeshRenderer = mr;

                // Fog durumuna göre materyal uygula
                mr.sharedMaterial = cell.FogState switch
                {
                    FogState.Visible  => visibleMat,
                    FogState.Explored => exploredMat,
                    _                 => hiddenMat,
                };
                rebuilt++;
            }

            int total = grid.Cells.Count;
            Debug.Log($"[FIX] {rebuilt}/{total} karo yeniden oluşturuldu. Ekranı kontrol et!");

            if (rebuilt == 0)
                Debug.Log("[FIX] Yeniden oluşturulacak karo yok — tüm MeshRenderer'lar geçerli. Sorun başka yerde.");
        }

        [MenuItem("TacticalRPG/0 — Sahneyi Tamamen Temizle")]
        public static void CleanupAll()
        {
            bool foundAny = false;
            foundAny |= DestroyRoot(SceneRootName);
            foundAny |= DestroyRoot(SystemsRootName);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            if (foundAny)
                EditorUtility.DisplayDialog("Temizlik Tamam",
                    "Tum TacticalRPG objeleri sahneden silindi.\n\nArtik Faz 0'dan baslayabilirsin.",
                    "Tamam");
            else
                EditorUtility.DisplayDialog("Temizlenecek Sey Yok",
                    "Sahnede TacticalRPG objesi bulunamadi.",
                    "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: Faz 0 — Temel sahne ögeleri
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 0 — Temel Sahne Ogeleri (Kamera, Isik, GameManager)")]
        public static void SetupPhase0()
        {
            // Önceki Faz 0 kurulumunu temizle (Faz 1 dokunulmaz)
            DestroyRoot(SceneRootName);

            GameObject root = new GameObject(SceneRootName);

            // ── Ana Kamera (top-down, ortografik) ────────────────────────────
            // Grid: 10x10, hexSize=1 → yaklaşık x:[0,16.5] z:[0,13.5]
            // Merkez: (7.8f, 0, 6.75f) → kamera tam ortaya bakar
            GameObject cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            cameraGO.transform.SetParent(root.transform);
            cameraGO.transform.position = new Vector3(7.8f, 50f, 6.75f);
            cameraGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Camera cam = cameraGO.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 8f;
            cam.nearClipPlane    = 0.1f;
            cam.farClipPlane     = 150f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.04f, 0.03f, 0.07f);

            // URP için zorunlu — bu olmadan URP materyalleri render edilmez
            var urpData = cameraGO.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderShadows        = false;
            urpData.requiresColorTexture = false;
            urpData.requiresDepthTexture = false;

            cameraGO.AddComponent<AudioListener>();

            // ── Directional Light ─────────────────────────────────────────────
            GameObject lightGO = new GameObject("Directional Light");
            lightGO.transform.SetParent(root.transform);
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Light light = lightGO.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.intensity = 2f;
            light.color     = Color.white;

            // ── GameManager ───────────────────────────────────────────────────
            GameObject gmGO = new GameObject("GameManager");
            gmGO.transform.SetParent(root.transform);
            gmGO.transform.position = Vector3.zero;

            // ── Kaydet ────────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[TacticalRPG] Faz 0 tamamlandi. '{SceneRootName}' olusturuldu.");
            EditorUtility.DisplayDialog(
                "Faz 0 Tamamlandi!",
                $"'{SceneRootName}' altinda olusturuldu:\n\n" +
                "  • Main Camera  — ortografik, Y=100, X rot=90°\n" +
                "  • Directional Light  — intensity=2\n" +
                "  • GameManager  — bos, hazir\n\n" +
                "Simdi Faz 1.1'i calistirabilirsin.",
                "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: Faz 1.1 — HexGrid + FogOfWar
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 1.1 - Hex Haritayi Kur")]
        public static void SetupPhase1()
        {
            // Yalnızca sistem kökünü temizle; Faz 0 kamerasına dokunma
            DestroyRoot(SystemsRootName);

            // Klasörler
            EnsureFolder("Assets/Art");
            EnsureFolder(MaterialsPath);
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabsGridPath);

            // Asset'ler
            Material hiddenMat   = GetOrCreateUnlitMaterial("FogHidden",   new Color(0.22f, 0.18f, 0.28f)); // Orta koyu mor: arka plandan belirgin
            Material exploredMat = GetOrCreateUnlitMaterial("FogExplored", new Color(0.25f, 0.25f, 0.25f));
            Material visibleMat  = GetOrCreateUnlitMaterial("FogVisible",  Color.white);
            GameObject hexCellPrefab = GetOrCreateHexCellPrefab(visibleMat);

            // Kök
            GameObject root = new GameObject(SystemsRootName);

            // ── HexGridManager ────────────────────────────────────────────────
            GameObject gridGO = new GameObject("HexGridManager");
            gridGO.transform.SetParent(root.transform);
            HexGridManager gridManager = gridGO.AddComponent<HexGridManager>();

            GameObject gridVisualsGO = new GameObject("HexGrid_Visuals");
            gridVisualsGO.transform.SetParent(gridGO.transform);

            var gridSO = new SerializedObject(gridManager);
            gridSO.FindProperty("_hexCellPrefab").objectReferenceValue = hexCellPrefab;
            gridSO.FindProperty("_gridParent").objectReferenceValue    = gridVisualsGO.transform;
            gridSO.FindProperty("_width").intValue                     = 10;
            gridSO.FindProperty("_height").intValue                    = 10;
            gridSO.FindProperty("_hexSize").floatValue                 = 1f;
            gridSO.ApplyModifiedProperties();

            // Edit modunda hücreleri hemen üret — Play beklemeye gerek yok
            gridManager.GenerateGrid();
            EditorUtility.SetDirty(gridVisualsGO);

            // ── FogOfWarManager ───────────────────────────────────────────────
            GameObject fogGO = new GameObject("FogOfWarManager");
            fogGO.transform.SetParent(root.transform);
            FogOfWarManager fogManager = fogGO.AddComponent<FogOfWarManager>();

            var fogSO = new SerializedObject(fogManager);
            fogSO.FindProperty("_gridManager").objectReferenceValue      = gridManager;
            fogSO.FindProperty("_hiddenMaterial").objectReferenceValue   = hiddenMat;
            fogSO.FindProperty("_exploredMaterial").objectReferenceValue = exploredMat;
            fogSO.FindProperty("_visibleMaterial").objectReferenceValue  = visibleMat;
            fogSO.ApplyModifiedProperties();

            // Kaydet
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TacticalRPG] Faz 1.1 tamamlandi. '{SystemsRootName}' hazir.");
            EditorUtility.DisplayDialog(
                "Faz 1.1 Tamamlandi!",
                $"'{SystemsRootName}' altinda olusturuldu:\n\n" +
                "  • HexGridManager  — 10x10, hexSize=1\n" +
                "  • FogOfWarManager  — Hidden / Explored / Visible\n" +
                "  • Prefabs/Grid/HexCell.prefab\n" +
                "  • Art/Materials/ (3 materyal)\n\n" +
                "Play'e bas — tum karolar siyah baslar.",
                "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: Faz 1.3 — Pathfinding + Oyuncu + Kuleler
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 1.3 - Pathfinding ve Oyuncu")]
        public static void SetupPhase13()
        {
            // Gerekli sistemleri sahnede bul
            GameObject systemsRoot = GameObject.Find(SystemsRootName);
            if (systemsRoot == null)
            {
                EditorUtility.DisplayDialog("Hata", "Once 'Faz 1.1 - Hex Haritayi Kur' calistirin!", "Tamam");
                return;
            }

            GameObject sceneRoot = GameObject.Find(SceneRootName);
            HexGridManager  gridManager = systemsRoot.GetComponentInChildren<HexGridManager>();
            FogOfWarManager fogManager  = systemsRoot.GetComponentInChildren<FogOfWarManager>();

            if (gridManager == null || fogManager == null)
            {
                EditorUtility.DisplayDialog("Hata", "HexGridManager veya FogOfWarManager bulunamadi!", "Tamam");
                return;
            }

            // ── 1. Prefab'a MeshCollider ekle, grid'i yeniden üret ────────────
            GetOrCreateHexCellPrefab(AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/FogVisible.mat"));
            var gridSO = new SerializedObject(gridManager);

            // Kule (Watchtower) konumları — 10x10 grid içinde 3 nokta
            var watchtowerProp = gridSO.FindProperty("_watchtowerPositions");
            watchtowerProp.ClearArray();
            var wtCoords = new[] { (3, 5), (1, 2), (4, 8) };
            for (int i = 0; i < wtCoords.Length; i++)
            {
                watchtowerProp.arraySize++;
                var elem = watchtowerProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("Q").intValue = wtCoords[i].Item1;
                elem.FindPropertyRelative("R").intValue = wtCoords[i].Item2;
            }
            gridSO.ApplyModifiedProperties();
            gridManager.GenerateGrid(); // MeshCollider'lı yeni prefab ile yeniden üret

            // ── 2. Oyuncu (Player) — basit küp, [TacticalRPG_Systems] altında ─
            Transform existingPlayer = systemsRoot.transform.Find("Player");
            if (existingPlayer != null) Object.DestroyImmediate(existingPlayer.gameObject);

            GameObject playerGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            playerGO.name = "Player";
            playerGO.transform.SetParent(systemsRoot.transform);
            playerGO.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            Object.DestroyImmediate(playerGO.GetComponent<BoxCollider>()); // grid collider ile çakışmasın

            PlayerController playerCtrl = playerGO.AddComponent<PlayerController>();
            var playerSO = new SerializedObject(playerCtrl);
            playerSO.FindProperty("_gridManager").objectReferenceValue  = gridManager;
            playerSO.FindProperty("_fogManager").objectReferenceValue   = fogManager;
            playerSO.FindProperty("_moveSpeed").floatValue              = 8f;
            playerSO.FindProperty("_heightOffset").floatValue           = 0.15f;
            playerSO.FindProperty("_visionRange").intValue              = 4;
            playerSO.FindProperty("_watchtowerRevealRange").intValue    = 5;
            // Başlangıç: grid merkezi ~ (3,4) → world(8.66, 0, 6.0) — kamera (7.8,50,6.75) ile hizalı
            playerSO.FindProperty("_startCoord").FindPropertyRelative("Q").intValue = 3;
            playerSO.FindProperty("_startCoord").FindPropertyRelative("R").intValue = 4;
            playerSO.ApplyModifiedProperties();

            // ── 3. MapInputHandler — GameManager objesine ekle ────────────────
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : null;

            if (gameManagerGO == null)
            {
                gameManagerGO = new GameObject("GameManager");
                gameManagerGO.transform.SetParent(systemsRoot.transform);
            }

            // Varsa eski MapInputHandler temizle
            var existingHandler = gameManagerGO.GetComponent<MapInputHandler>();
            if (existingHandler != null) Object.DestroyImmediate(existingHandler);

            MapInputHandler inputHandler = gameManagerGO.AddComponent<MapInputHandler>();
            var inputSO = new SerializedObject(inputHandler);
            inputSO.FindProperty("_gridManager").objectReferenceValue = gridManager;
            inputSO.FindProperty("_player").objectReferenceValue      = playerCtrl;
            inputSO.FindProperty("_rayDistance").floatValue           = 300f;
            inputSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TacticalRPG] Faz 1.3 tamamlandi.");
            EditorUtility.DisplayDialog(
                "Faz 1.3 Tamamlandi!",
                "Eklenenler:\n\n" +
                "  • HexPathfinder (A* algoritması)\n" +
                "  • Player (küp) — (0,0) konumunda\n" +
                "  • MapInputHandler — sol tık ile hareket\n" +
                "  • 3 Watchtower konumu: (3,5) (1,2) (4,8)\n" +
                "  • MeshCollider — tüm hex karolarda\n\n" +
                "Play'e bas, haritaya sol tıkla — karakter A* ile yürür!\n" +
                "Watchtower karosuna gidince geniş alan açılır.",
                "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: Faz 1.4 — AP + Zaman Motoru
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 1.4 - AP ve Zaman Motoru")]
        public static void SetupPhase14()
        {
            GameObject systemsRoot = GameObject.Find(SystemsRootName);
            if (systemsRoot == null)
            {
                EditorUtility.DisplayDialog("Hata", "Once 'Faz 1.1' ve 'Faz 1.3' calistirin!", "Tamam");
                return;
            }

            PlayerController player = systemsRoot.GetComponentInChildren<PlayerController>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Hata", "PlayerController bulunamadi! Once Faz 1.3 calistirin.", "Tamam");
                return;
            }

            // ── TimeSlotConfig ScriptableObject asset ──────────────────────
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Config");
            const string configPath = "Assets/Data/Config/TimeSlotConfig.asset";
            TimeSlotConfig config = AssetDatabase.LoadAssetAtPath<TimeSlotConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<TimeSlotConfig>();
                AssetDatabase.CreateAsset(config, configPath);
            }

            // ── ActionPointManager — GameManager objesine ekle ─────────────
            GameObject sceneRoot    = GameObject.Find(SceneRootName);
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : systemsRoot.transform.Find("GameManager")?.gameObject;

            if (gameManagerGO == null)
            {
                gameManagerGO = new GameObject("GameManager");
                gameManagerGO.transform.SetParent(systemsRoot.transform);
            }

            var existingAP = gameManagerGO.GetComponent<ActionPointManager>();
            if (existingAP != null) Object.DestroyImmediate(existingAP);

            ActionPointManager apManager = gameManagerGO.AddComponent<ActionPointManager>();
            var apSO = new SerializedObject(apManager);
            apSO.FindProperty("_player").objectReferenceValue  = player;
            apSO.FindProperty("_config").objectReferenceValue  = config;
            apSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TacticalRPG] Faz 1.4 tamamlandi.");
            EditorUtility.DisplayDialog(
                "Faz 1.4 Tamamlandi!",
                "AP + Zaman Motoru aktif:\n\n" +
                "  • ActionPointManager — GameManager'a eklendi\n" +
                "  • TimeSlotConfig SO — Assets/Data/Config/\n" +
                "  • 3 AP = 1 zaman dilimi\n" +
                "  • 6 dilim = 1 gün\n\n" +
                "Console'da 'Gün X | Dilim' loglarını izle.\n" +
                "Faz 1.5 ile Kıyamet Sayacı eklenecek.",
                "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: Faz 1.5 — Map Collapse / Kıyamet Sayacı
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Faz 1.5 - Kiyamet Sayaci (Map Collapse)")]
        public static void SetupPhase15()
        {
            GameObject systemsRoot = GameObject.Find(SystemsRootName);
            if (systemsRoot == null)
            {
                EditorUtility.DisplayDialog("Hata", "Once Faz 1.1, 1.3 ve 1.4 calistirin!", "Tamam");
                return;
            }

            HexGridManager     gridManager = systemsRoot.GetComponentInChildren<HexGridManager>();
            PlayerController   player      = systemsRoot.GetComponentInChildren<PlayerController>();
            ActionPointManager apManager   = FindComponentAnywhere<ActionPointManager>();

            if (gridManager == null || player == null || apManager == null)
            {
                EditorUtility.DisplayDialog("Hata", "HexGridManager, PlayerController veya ActionPointManager bulunamadi!", "Tamam");
                return;
            }

            // ── CollapseConfig ScriptableObject ───────────────────────────
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Config");
            const string configPath = "Assets/Data/Config/CollapseConfig.asset";
            CollapseConfig config = AssetDatabase.LoadAssetAtPath<CollapseConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CollapseConfig>();
                AssetDatabase.CreateAsset(config, configPath);
            }

            // ── Collapse materyali ────────────────────────────────────────
            Material collapsedMat = GetOrCreateUnlitMaterial("TileCollapsed", new Color(0.15f, 0.05f, 0.05f));

            // ── MapCollapseManager — GameManager'a ekle ───────────────────
            GameObject sceneRoot    = GameObject.Find(SceneRootName);
            GameObject gameManagerGO = sceneRoot != null
                ? sceneRoot.transform.Find("GameManager")?.gameObject
                : systemsRoot.transform.Find("GameManager")?.gameObject;

            if (gameManagerGO == null)
            {
                gameManagerGO = new GameObject("GameManager");
                gameManagerGO.transform.SetParent(systemsRoot.transform);
            }

            var existing = gameManagerGO.GetComponent<MapCollapseManager>();
            if (existing != null) Object.DestroyImmediate(existing);

            MapCollapseManager collapseManager = gameManagerGO.AddComponent<MapCollapseManager>();
            var collapseSO = new SerializedObject(collapseManager);
            collapseSO.FindProperty("_gridManager").objectReferenceValue      = gridManager;
            collapseSO.FindProperty("_apManager").objectReferenceValue        = apManager;
            collapseSO.FindProperty("_player").objectReferenceValue           = player;
            collapseSO.FindProperty("_config").objectReferenceValue           = config;
            collapseSO.FindProperty("_collapsedMaterial").objectReferenceValue = collapsedMat;
            collapseSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TacticalRPG] Faz 1.5 tamamlandi.");
            EditorUtility.DisplayDialog(
                "Faz 1.5 Tamamlandi! — Kiyamet Sayaci",
                "Map Collapse aktif:\n\n" +
                "  • MapCollapseManager — GameManager'a eklendi\n" +
                "  • CollapseConfig SO — Assets/Data/Config/\n" +
                "  • Gün 4'ten itibaren her gün sonu karo silinir\n" +
                "  • Hız: 2 karo/gün, +1 ivme (max 10)\n" +
                "  • Oyuncu konumu ve Watchtower'lar korunur\n\n" +
                "Hafta 1 TAMAMLANDI! Hafta 2'ye gecmek icin hazir.",
                "Tamam");
        }

        // ─────────────────────────────────────────────────────────────────────
        // MENÜ: Debug HUD — Gün / AP / Kıyamet etiketi
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("TacticalRPG/Debug HUD - Ekle")]
        public static void SetupDebugHUD()
        {
            ActionPointManager  apManager      = FindComponentAnywhere<ActionPointManager>();
            MapCollapseManager  collapseManager = FindComponentAnywhere<MapCollapseManager>();
            EssenceManager      essManager      = FindComponentAnywhere<EssenceManager>();
            KamManaManager      kamMana         = FindComponentAnywhere<KamManaManager>();

            if (apManager == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "ActionPointManager bulunamadı!\nÖnce Faz 1.4'ü çalıştırın.", "Tamam");
                return;
            }

            // Varsa eski HUD canvas'ını temizle
            GameObject oldCanvas = GameObject.Find("DebugHUD_Canvas");
            if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);

            // ── Canvas ────────────────────────────────────────────────────────
            GameObject canvasGO = new GameObject("DebugHUD_Canvas", typeof(RectTransform));
            GameObject sceneRoot = GameObject.Find(SceneRootName);
            if (sceneRoot != null) canvasGO.transform.SetParent(sceneRoot.transform);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Etiketler (sol üstten aşağı, 48px aralık) ────────────────────
            float yStart  = -20f;
            float yStep   = -48f;
            int   row     = 0;

            TextMeshProUGUI timeLabel = CreateTMPLabel(
                canvasGO.transform, "Label_Time", "Gün 1  ·  Sabah",
                new Vector2(20f, yStart + yStep * row++), new Vector2(400f, 46f),
                Color.white, 28f);

            TextMeshProUGUI apLabel = CreateTMPLabel(
                canvasGO.transform, "Label_AP", "AP  ■■■  3/3",
                new Vector2(20f, yStart + yStep * row++), new Vector2(400f, 44f),
                new Color(1f, 0.85f, 0.2f), 24f);

            TextMeshProUGUI essLabel = CreateTMPLabel(
                canvasGO.transform, "Label_Essence", "Öz  0",
                new Vector2(20f, yStart + yStep * row++), new Vector2(300f, 40f),
                new Color(0.6f, 1f, 0.6f), 22f); // Açık yeşil

            TextMeshProUGUI manaLabel = null;
            if (kamMana != null)
            {
                manaLabel = CreateTMPLabel(
                    canvasGO.transform, "Label_KamMana", "Mana  ◆◆◆◆◆◆◆◆◆◆  10/10",
                    new Vector2(20f, yStart + yStep * row++), new Vector2(500f, 40f),
                    new Color(0.5f, 0.8f, 1f), 20f); // Açık mavi
            }
            else row++; // boşluk koru

            TextMeshProUGUI collapseLabel = CreateTMPLabel(
                canvasGO.transform, "Label_Collapse", "HARITA ÇÖKÜYOR",
                new Vector2(20f, yStart + yStep * row), new Vector2(500f, 44f),
                new Color(1f, 0.25f, 0.15f), 22f);
            collapseLabel.gameObject.SetActive(false);

            // ── DebugHUD bileşeni ─────────────────────────────────────────────
            DebugHUD hud = canvasGO.AddComponent<DebugHUD>();
            var hudSO = new SerializedObject(hud);
            hudSO.FindProperty("_apManager").objectReferenceValue       = apManager;
            hudSO.FindProperty("_collapseManager").objectReferenceValue = collapseManager;
            hudSO.FindProperty("_essenceManager").objectReferenceValue  = essManager;
            hudSO.FindProperty("_kamMana").objectReferenceValue         = kamMana;
            hudSO.FindProperty("_timeLabel").objectReferenceValue       = timeLabel;
            hudSO.FindProperty("_apLabel").objectReferenceValue         = apLabel;
            hudSO.FindProperty("_essenceLabel").objectReferenceValue    = essLabel;
            hudSO.FindProperty("_kamManaLabel").objectReferenceValue    = manaLabel;
            hudSO.FindProperty("_collapseLabel").objectReferenceValue   = collapseLabel;
            hudSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            bool hasEcon = essManager != null;
            Debug.Log($"[TacticalRPG] Debug HUD kuruldu. Öz={hasEcon} KamMana={kamMana != null}");
            EditorUtility.DisplayDialog(
                "Debug HUD Hazır!",
                "Sol üstte şunlar görünecek:\n\n" +
                "  Gün 1  ·  Sabah          (beyaz)\n" +
                "  AP  ■■■  3/3             (altın sarısı)\n" +
                "  Öz  0                    (yeşil)" +
                (kamMana != null ? "\n  Mana  ◆◆◆…  10/10     (mavi)\n" : "\n") +
                "  HARITA ÇÖKÜYOR           (kırmızı, Gün 4'te)\n\n" +
                "Faz 2.1 çalıştırmadıysan Öz/Mana boş görünür — normal.",
                "Tamam");
        }

        // Sol-üst anchor sabitlenmiş basit overload (Faz 2 HUD için)
        private static TextMeshProUGUI CreateTMPLabel(
            Transform parent, string name, string text,
            Vector2 anchoredPos, Vector2 sizeDelta, Color color, float fontSize)
        {
            return CreateTMPLabel(parent, name, text,
                new Vector2(0f,1f), new Vector2(0f,1f), new Vector2(0f,1f),
                anchoredPos, sizeDelta, color, fontSize);
        }

        private static TextMeshProUGUI CreateTMPLabel(
            Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            Color color, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.pivot            = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = sizeDelta;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text               = text;
            tmp.fontSize           = fontSize;
            tmp.color              = color;
            tmp.fontStyle          = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            tmp.overflowMode       = TextOverflowModes.Overflow;

            return tmp;
        }

        private static T FindComponentAnywhere<T>() where T : UnityEngine.Component
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yardımcı metodlar
        // ─────────────────────────────────────────────────────────────────────

        private static bool DestroyRoot(string rootName)
        {
            GameObject go = GameObject.Find(rootName);
            if (go == null) return false;
            Object.DestroyImmediate(go);
            Debug.Log($"[TacticalRPG] '{rootName}' sahneden silindi.");
            return true;
        }

        private static Material GetOrCreateUnlitMaterial(string assetName, Color color)
        {
            string path = $"{MaterialsPath}/{assetName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                // URP Unlit önce dene, yoksa legacy Unlit/Color
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Color")
                             ?? Shader.Find("Hidden/InternalErrorShader");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color",   color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Mesh GetOrCreateHexMesh()
        {
            string meshPath = "Assets/Art/Meshes/HexMesh.asset";
            EnsureFolder("Assets/Art/Meshes");

            // Her seferinde yeniden üret — HexMetrics değişirse güncel kalsın
            AssetDatabase.DeleteAsset(meshPath);
            Mesh mesh = HexMetrics.CreateHexMesh(0.95f);
            AssetDatabase.CreateAsset(mesh, meshPath);
            return mesh;
        }

        private static GameObject GetOrCreateHexCellPrefab(Material defaultMat)
        {
            string path = $"{PrefabsGridPath}/HexCell.prefab";

            Mesh hexMesh = GetOrCreateHexMesh();

            // Her seferinde prosedürel hex mesh ile yeniden oluştur
            GameObject go = new GameObject("HexCell");
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = hexMesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = defaultMat;
            // MeshCollider — Raycast tıklama algılaması için gerekli
            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = hexMesh;

            // PrefabUtility.SaveAsPrefabAsset varsa üzerine yazar
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            if (parent != null)
                AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
