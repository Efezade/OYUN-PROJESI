using UnityEngine;
using UnityEditor;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Savaş tarafının kurulumu (2026-08-12): hasar formülü config'i + PROSEDÜREL ARENA üreticisi.
    ///
    /// Bu iki parça olmadan yeni kod ATIL kalır: <c>GameStateManager._arena</c> boşsa savaş yine
    /// eski 22×25'lik `CombatTileMap`'e düşer, <c>CombatFormulaBinder</c> yoksa formül varsayılan
    /// değerlerle çalışır (yine de doğru çalışır, ama Inspector'dan tweaklenemez).
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string FormulaPath = "Assets/Data/Config/CombatFormula.asset";
        private const string ArenaPath   = "Assets/Data/Config/CombatArenaConfig.asset";

        [MenuItem("TacticalRPG/Savas - Arena + Formul Kur", false, 27)]
        public static void SetupCombatMenu()
        {
            SetupCombat();
            EditorUtility.DisplayDialog("Savas Kurulumu",
                "Hasar formulu ve prosedurel arena ureticisi kuruldu.\n\n" +
                "Savasa girince artik dugum tipine gore 65-120 karoluk arena uretilir\n" +
                "(eskiden 550 karoluk duz tahtaydi).", "Tamam");
        }

        /// <summary>Formül config'i + arena config'i üretir, bileşenleri ekleyip bağlar.</summary>
        public static void SetupCombat()
        {
            var state = FindComponentAnywhere<GameStateManager>();
            var grid  = FindComponentAnywhere<HexGridManager>();
            if (state == null || grid == null)
            {
                Debug.LogError("[Savas] GameStateManager ya da grid bulunamadi — once TAM KURULUM.");
                return;
            }
            GameObject host = state.gameObject;

            // ── 1) Hasar formulu ─────────────────────────────────────────────
            var formula = EnsureAsset<CombatFormulaSO>(FormulaPath);

            var binder = host.GetComponent<CombatFormulaBinder>();
            if (binder == null) binder = host.AddComponent<CombatFormulaBinder>();
            var bSO = new SerializedObject(binder);
            bSO.FindProperty("_formula").objectReferenceValue = formula;
            bSO.ApplyModifiedProperties();

            // ── 2) Arena config (4 kademe + seed havuzu) ─────────────────────
            var arenaCfg = AssetDatabase.LoadAssetAtPath<CombatArenaConfigSO>(ArenaPath);
            bool freshArena = arenaCfg == null;
            if (freshArena) arenaCfg = EnsureAsset<CombatArenaConfigSO>(ArenaPath);

            if (freshArena) FillArenaTiers(arenaCfg);

            // ── 3) Arena ureticisi ───────────────────────────────────────────
            var gen = host.GetComponent<CombatMapGenerator>();
            if (gen == null) gen = host.AddComponent<CombatMapGenerator>();
            var gSO = new SerializedObject(gen);
            gSO.FindProperty("_grid").objectReferenceValue   = grid;
            gSO.FindProperty("_config").objectReferenceValue = arenaCfg;
            gSO.ApplyModifiedProperties();

            // ── 4) Tuketiciler ───────────────────────────────────────────────
            var stSO = new SerializedObject(state);
            stSO.FindProperty("_arena").objectReferenceValue = gen;
            stSO.ApplyModifiedProperties();

            var spawner = FindComponentAnywhere<EnemySpawner>();
            if (spawner != null)
            {
                var spSO = new SerializedObject(spawner);
                spSO.FindProperty("_arena").objectReferenceValue = gen;
                spSO.ApplyModifiedProperties();
            }

            var turns = FindComponentAnywhere<TurnManager>();
            var units = FindComponentAnywhere<UnitManager>();

            // Gorus hatti: arena duvarlari menzilli atisi kessin (siper kesmez).
            if (turns != null)
            {
                var tSO = new SerializedObject(turns);
                tSO.FindProperty("_arena").objectReferenceValue = gen;
                tSO.ApplyModifiedProperties();
            }

            // ── 5) Davul karolarinin ETKI motoru + geri bildirimi ────────────
            // Bu iki bilesen olmadan karo yalnizca boyanir: sersemletme, can, patlama calismaz
            // (2026-08-12'ye kadarki durum — kullanici "karolar calismiyor" dedi).
            var fx = host.GetComponent<AugmentFeedback>();
            if (fx == null) fx = host.AddComponent<AugmentFeedback>();

            var augments = host.GetComponent<AugmentTileManager>();
            if (augments == null) augments = host.AddComponent<AugmentTileManager>();
            var aSO = new SerializedObject(augments);
            aSO.FindProperty("_grid").objectReferenceValue  = grid;
            aSO.FindProperty("_units").objectReferenceValue = units;
            aSO.FindProperty("_turns").objectReferenceValue = turns;
            aSO.FindProperty("_state").objectReferenceValue = state;
            aSO.FindProperty("_fx").objectReferenceValue    = fx;
            aSO.FindProperty("_mana").objectReferenceValue  = FindComponentAnywhere<KamManaManager>();
            aSO.ApplyModifiedProperties();

            // ── 5b) KAM BUYULERI: hedefleme + gosterge + animasyon ───────────
            // Ucu birlikte kurulmali: gosterge olmadan hedefleme korlemesine olur, VFX olmadan
            // buyu "sessizce" olur, caster olmadan ikisi de calismaz.
            var indicator = host.GetComponent<SkillAreaIndicator>();
            if (indicator == null) indicator = host.AddComponent<SkillAreaIndicator>();
            var indSO = new SerializedObject(indicator);
            indSO.FindProperty("_grid").objectReferenceValue = grid;
            indSO.ApplyModifiedProperties();

            var skillVfx = host.GetComponent<KamSkillVfx>();
            if (skillVfx == null) skillVfx = host.AddComponent<KamSkillVfx>();
            var vfxSO = new SerializedObject(skillVfx);
            vfxSO.FindProperty("_grid").objectReferenceValue   = grid;
            vfxSO.FindProperty("_camera").objectReferenceValue = FindComponentAnywhere<TacticalRPG.UI.CameraZoomSettings>();
            vfxSO.ApplyModifiedProperties();

            var skills = host.GetComponent<KamSkillCaster>();
            if (skills == null) skills = host.AddComponent<KamSkillCaster>();
            var skSO = new SerializedObject(skills);
            skSO.FindProperty("_grid").objectReferenceValue      = grid;
            skSO.FindProperty("_units").objectReferenceValue     = units;
            skSO.FindProperty("_turns").objectReferenceValue     = turns;
            skSO.FindProperty("_camera").objectReferenceValue    = Camera.main;
            skSO.FindProperty("_indicator").objectReferenceValue = indicator;
            skSO.FindProperty("_vfx").objectReferenceValue       = skillVfx;
            skSO.FindProperty("_feedback").objectReferenceValue  = fx;
            skSO.FindProperty("_zoom").objectReferenceValue      = FindComponentAnywhere<TacticalRPG.UI.CameraZoomSettings>();
            skSO.FindProperty("_augments").objectReferenceValue  = augments;
            skSO.ApplyModifiedProperties();

            // ── 6) Davul temposu (karo drafti + Kam cevresine yerlestirme) ───
            var drum = host.GetComponent<CombatDrumManager>();
            if (drum == null) drum = host.AddComponent<CombatDrumManager>();
            var dSO = new SerializedObject(drum);
            dSO.FindProperty("_turns").objectReferenceValue = turns;
            dSO.FindProperty("_units").objectReferenceValue = units;
            dSO.FindProperty("_grid").objectReferenceValue  = grid;
            dSO.FindProperty("_arena").objectReferenceValue = gen;
            dSO.FindProperty("_tiles").objectReferenceValue  = augments;
            dSO.FindProperty("_skills").objectReferenceValue = skills;   // her draftta garanti buyu
            dSO.ApplyModifiedProperties();

            // ── 6b) Davul karolarinin MODELLERI (24 prefab, tek renk temasi) ──
            TileVisualFactory.BuildAugments(force: false);

            // ── 7) Karo drafti ekrani (TFT tarzi 3 kart) ─────────────────────
            var draftHud = host.GetComponent<TacticalRPG.UI.AugmentDraftHUD>();
            if (draftHud == null) draftHud = host.AddComponent<TacticalRPG.UI.AugmentDraftHUD>();
            var hSO = new SerializedObject(draftHud);
            hSO.FindProperty("_drum").objectReferenceValue = drum;
            hSO.FindProperty("_grid").objectReferenceValue = grid;
            hSO.ApplyModifiedProperties();

            // ── 8) Harita tiklamasi yerlestirme moduna gitsin ────────────────
            var input = FindComponentAnywhere<MapInputHandler>();
            if (input != null)
            {
                var iSO = new SerializedObject(input);
                iSO.FindProperty("_drum").objectReferenceValue   = drum;
                iSO.FindProperty("_skills").objectReferenceValue = skills;
                iSO.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(host);
            AssetDatabase.SaveAssets();
            Debug.Log("[Savas] Arena ureticisi + hasar formulu baglandi.");
        }

        /// <summary>Kademe tablosunu doldurur — sohbetteki hesap (sahadaki birim × ~10 karo).
        /// Yalnız asset YENİ oluşturulduysa çalışır; kullanıcı tweaklerse ezilmez.</summary>
        private static void FillArenaTiers(CombatArenaConfigSO cfg)
        {
            var so    = new SerializedObject(cfg);
            var tiers = so.FindProperty("_tiers");
            tiers.ClearArray();

            // tier, w, h, deployDepth, enemyDepth, enemyCount, blocked%, interactive%, edgeRoughness
            var rows = new (ArenaTier t, int w, int h, int dd, int ed, int ec, float bp, float ip, float er)[]
            {
                (ArenaTier.Encounter, 10,  8, 2, 2, 4, 0.08f, 0.10f, 0.45f),
                (ArenaTier.Dungeon,   11,  9, 2, 2, 6, 0.09f, 0.11f, 0.50f),
                (ArenaTier.Mandatory, 12, 10, 2, 3, 7, 0.10f, 0.12f, 0.50f),
                (ArenaTier.Boss,      13, 11, 3, 3, 6, 0.10f, 0.12f, 0.55f),
            };

            for (int i = 0; i < rows.Length; i++)
            {
                tiers.InsertArrayElementAtIndex(i);
                var e = tiers.GetArrayElementAtIndex(i);
                var r = rows[i];
                e.FindPropertyRelative("tier").enumValueIndex   = (int)r.t;
                e.FindPropertyRelative("width").intValue        = r.w;
                e.FindPropertyRelative("height").intValue       = r.h;
                e.FindPropertyRelative("deployDepth").intValue  = r.dd;
                e.FindPropertyRelative("enemyDepth").intValue   = r.ed;
                e.FindPropertyRelative("enemyCount").intValue   = r.ec;
                e.FindPropertyRelative("blockedPct").floatValue     = r.bp;
                e.FindPropertyRelative("interactivePct").floatValue = r.ip;
                e.FindPropertyRelative("edgeRoughness").floatValue  = r.er;
            }

            // Seed havuzu: arka arkaya ayni arena gelmesin diye 16 sabit seed.
            var pool = so.FindProperty("_seedPool");
            pool.ClearArray();
            int[] seeds = { 7, 23, 41, 58, 76, 94, 113, 137, 152, 178, 191, 214, 233, 256, 271, 298 };
            for (int i = 0; i < seeds.Length; i++)
            {
                pool.InsertArrayElementAtIndex(i);
                pool.GetArrayElementAtIndex(i).intValue = seeds[i];
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(cfg);
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
