using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.UI;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// SceneSetupTool'un MAĞAZA (store) parçası: öz karşılığı item/pot satılan dükkânı kurar.
    ///   • Palete "magaza" karosu ekler (mantar_karo modeli, isStore ✓, yürünemez) → Tile Painter'dan
    ///     istenen yere boyanır.
    ///   • 5 <see cref="ShopItemSO"/> asset üretir (3 geçici pot + 2 kalıcı item) → Assets/Data/Shop.
    ///   • GameManager'a StoreManager + PlayerBuffs + StoreHUD ekler ve bağlar.
    ///
    /// TAM KURULUM zincirine SetupUIShell'den sonra girer (grid/oyuncu/AP/öz o an hazır).
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string ShopFolder    = "Assets/Data/Shop";
        private const string StoreTilePath = "Assets/Art/Models/Tiles/mantar_karo.fbx";

        [MenuItem("TacticalRPG/Store - Magaza Karosu + Dukkan Kur", false, 23)]
        public static void SetupStore()
        {
            HexGridManager    grid      = FindComponentAnywhere<HexGridManager>();
            GameObject        gm         = FindComponentAnywhere<MapInputHandler>()?.gameObject
                                          ?? GameObject.Find("GameManager");
            if (grid == null || gm == null)
            {
                if (!_silentSetup)
                    EditorUtility.DisplayDialog("Store",
                        "Grid ya da GameManager bulunamadi! Once TAM KURULUM (Faz 0-2) calistir.", "Tamam");
                return;
            }

            // ── 1) Katalog asset'leri ─────────────────────────────────────────
            EnsureFolder(ShopFolder);
            List<ShopItemSO> catalog = new()
            {
                EnsureShopItem("YelAyagi",      "yel_ayagi",     "Yel Ayağı İksiri",
                    "Bir süre haritada daha hızlı yürürsün (x2 hız, 6 adım).",
                    new[] { new EssenceAmount(EssenceType.Su, 3) },
                    ShopEffectKind.MoveSpeed, 100, permanent: false, durationMoves: 6),

                EnsureShopItem("KartalGozu",    "kartal_gozu",   "Kartal Gözü İksiri",
                    "Bir süre tek tıkla daha uzağa yürü (+2 menzil, 6 adım).",
                    new[] { new EssenceAmount(EssenceType.Ates, 3) },
                    ShopEffectKind.MoveRange, 2, permanent: false, durationMoves: 6),

                EnsureShopItem("ZamanKumu",     "zaman_kumu",    "Zaman Kumu",
                    "Anında +5 AP kazan (bu dilim).",
                    new[] { new EssenceAmount(EssenceType.Toprak, 4) },
                    ShopEffectKind.BonusAPNow, 5, permanent: false, durationMoves: 0),

                EnsureShopItem("SaglamCizme",   "saglam_cizme",  "Sağlam Çizmeler",
                    "KALICI: yürüme hızın kalıcı olarak +%25 artar.",
                    new[] { new EssenceAmount(EssenceType.Su, 6), new EssenceAmount(EssenceType.Toprak, 4) },
                    ShopEffectKind.MoveSpeed, 25, permanent: true, durationMoves: 0),

                EnsureShopItem("KahinPusulasi", "kahin_pusulasi", "Kâhin Pusulası",
                    "KALICI: tek tık hareket menzilin kalıcı olarak +1 artar.",
                    new[] { new EssenceAmount(EssenceType.Ates, 6), new EssenceAmount(EssenceType.Toprak, 4) },
                    ShopEffectKind.MoveRange, 1, permanent: true, durationMoves: 0),
            };

            // ── 2) Palete "magaza" karosu ─────────────────────────────────────
            AddStorePaletteEntry(grid);

            // ── 3) Bağımlılıklar ──────────────────────────────────────────────
            GameStateManager  gsm      = FindComponentAnywhere<GameStateManager>();
            WorldGridManager  world    = FindComponentAnywhere<WorldGridManager>();
            PlayerController  player   = FindComponentAnywhere<PlayerController>();
            MapInputHandler   input    = FindComponentAnywhere<MapInputHandler>();
            ActionPointManager ap      = FindComponentAnywhere<ActionPointManager>();
            EssenceWallet     wallet   = FindComponentAnywhere<EssenceWallet>();
            EssenceConfigSO   config   = FindEssenceConfig();

            // ── 4) PlayerBuffs (etki uygulayıcı) ──────────────────────────────
            var oldBuffs = gm.GetComponent<PlayerBuffs>();
            if (oldBuffs != null) Object.DestroyImmediate(oldBuffs);
            PlayerBuffs buffs = gm.AddComponent<PlayerBuffs>();
            var bso = new SerializedObject(buffs);
            bso.FindProperty("_player").objectReferenceValue    = player;
            bso.FindProperty("_input").objectReferenceValue     = input;
            bso.FindProperty("_apManager").objectReferenceValue = ap;
            bso.ApplyModifiedProperties();

            // ── 5) StoreManager (katalog + yakınlık + işaretler) ──────────────
            var oldStore = gm.GetComponent<StoreManager>();
            if (oldStore != null) Object.DestroyImmediate(oldStore);
            StoreManager store = gm.AddComponent<StoreManager>();
            var sso = new SerializedObject(store);
            sso.FindProperty("_grid").objectReferenceValue         = grid;
            sso.FindProperty("_stateManager").objectReferenceValue = gsm;
            sso.FindProperty("_worldGrid").objectReferenceValue    = world;
            SerializedProperty cat = sso.FindProperty("_catalog");
            cat.arraySize = catalog.Count;
            for (int i = 0; i < catalog.Count; i++)
                cat.GetArrayElementAtIndex(i).objectReferenceValue = catalog[i];
            sso.ApplyModifiedProperties();

            // ── 6) StoreHUD (IMGUI dükkân) ────────────────────────────────────
            var oldHud = gm.GetComponent<StoreHUD>();
            if (oldHud != null) Object.DestroyImmediate(oldHud);
            StoreHUD hud = gm.AddComponent<StoreHUD>();
            var hso = new SerializedObject(hud);
            hso.FindProperty("_stateManager").objectReferenceValue = gsm;
            hso.FindProperty("_store").objectReferenceValue        = store;
            hso.FindProperty("_player").objectReferenceValue       = player;
            hso.FindProperty("_wallet").objectReferenceValue       = wallet;
            hso.FindProperty("_buffs").objectReferenceValue        = buffs;
            hso.FindProperty("_config").objectReferenceValue       = config;
            hso.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            if (!_silentSetup)
                EditorUtility.DisplayDialog("Magaza Kuruldu",
                    "Store sistemi kuruldu:\n\n" +
                    "  • Palete 'Mağaza' karosu eklendi (mantar modeli) → Tile Painter'dan boya\n" +
                    "  • 5 öğe: Yel Ayağı / Kartal Gözü / Zaman Kumu (pot) + Sağlam Çizme / Kâhin Pusulası (kalıcı)\n" +
                    "  • Mağazaya yaklaşınca 'Dükkani Ac' istemi → öz harcayıp satın al\n\n" +
                    "Tile Painter'da MAĞAZA karosunu haritaya boya, Play → yanına yürü.", "Tamam");

            Debug.Log("[TacticalRPG] Store sistemi kuruldu (magaza karosu + 5 item + StoreManager/PlayerBuffs/StoreHUD).");
        }

        /// <summary>Palete "magaza" karosunu ekler/günceller (mantar_karo modeli, isStore, yürünemez).</summary>
        private static void AddStorePaletteEntry(HexGridManager grid)
        {
            TilePaletteSO palette = grid.TilePalette;
            if (palette == null) return;

            GameObject storePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StoreTilePath);

            var palSO   = new SerializedObject(palette);
            var tiles   = palSO.FindProperty("tiles");
            const string id = "magaza";

            int idx = -1;
            for (int j = 0; j < tiles.arraySize; j++)
                if (tiles.GetArrayElementAtIndex(j).FindPropertyRelative("id").stringValue == id) { idx = j; break; }
            if (idx < 0) { tiles.arraySize++; idx = tiles.arraySize - 1; }

            var e = tiles.GetArrayElementAtIndex(idx);
            e.FindPropertyRelative("id").stringValue              = id;
            e.FindPropertyRelative("displayName").stringValue     = "Mağaza";
            e.FindPropertyRelative("prefab").objectReferenceValue = storePrefab; // null ise altın tint placeholder
            e.FindPropertyRelative("editorColor").colorValue      = new Color(1f, 0.82f, 0.2f);
            e.FindPropertyRelative("isWalkable").boolValue         = false;       // dükkân = engel; oyuncu bitişikte durur
            e.FindPropertyRelative("canEnterCombat").boolValue     = false;
            e.FindPropertyRelative("isStore").boolValue            = true;
            e.FindPropertyRelative("surfaceHeightOverride").floatValue = 0f;

            palSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(palette);
        }

        /// <summary>Bir ShopItemSO asset'ini oluşturur (yoksa) ve alanlarını yazar; günceli döner.</summary>
        private static ShopItemSO EnsureShopItem(string fileName, string id, string name, string desc,
            EssenceAmount[] cost, ShopEffectKind effect, int magnitude, bool permanent, int durationMoves)
        {
            string path = $"{ShopFolder}/{fileName}.asset";
            ShopItemSO item = AssetDatabase.LoadAssetAtPath<ShopItemSO>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ShopItemSO>();
                AssetDatabase.CreateAsset(item, path);
            }

            var so = new SerializedObject(item);
            so.FindProperty("_id").stringValue          = id;
            so.FindProperty("_displayName").stringValue = name;
            so.FindProperty("_description").stringValue = desc;
            so.FindProperty("_effect").enumValueIndex   = (int)effect;
            so.FindProperty("_magnitude").intValue      = magnitude;
            so.FindProperty("_permanent").boolValue     = permanent;
            so.FindProperty("_durationMoves").intValue  = durationMoves;

            SerializedProperty costProp = so.FindProperty("_cost");
            costProp.arraySize = cost.Length;
            for (int i = 0; i < cost.Length; i++)
            {
                var el = costProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("type").enumValueIndex = (int)cost[i].type;
                el.FindPropertyRelative("amount").intValue     = cost[i].amount;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(item);
            return item;
        }
    }
}
