using UnityEngine;
using UnityEditor;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// SceneSetupTool'un BÖLÜM (chapter) parçası — **1 bölüm = 1 harita, toplam 8 bölüm**
    /// (Docs/GAME_DESIGN.md §0/§3, TASK-004 · 2026-07-28).
    ///
    ///   • <c>Assets/Data/Chapters/ChapterConfig.asset</c> üretir (8 giriş; adı/teması netleşmemiş
    ///     bölümler "?" ile PLACEHOLDER işaretli — uydurma içerik gerçek karar sanılmasın).
    ///   • GameManager'a <see cref="ChapterProgress"/> (ilerlemenin tek kaynağı) + TAB ile açılan
    ///     8 bölümlük ilerleme şeridini (<see cref="TacticalRPG.UI.MinimapHUD"/>) ekler ve bağlar.
    ///
    /// TAM KURULUM zincirinde **SetupUIShell'den ÖNCE** çalışmalı — HARİTA ekranı
    /// (<c>PopulateMapScreen</c>) kurulurken sahnede hazır bir ChapterProgress arar.
    ///
    /// KULLANICI VERİSİNİ KORUR: ChapterConfig.asset varsa ÜZERİNE YAZILMAZ (elle düzenlenen
    /// bölüm adları/temaları TAM KURULUM'da silinmesin — CLAUDE.md tuzak notu).
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string ChapterFolder = "Assets/Data/Chapters";
        private const string ChapterConfigPath = ChapterFolder + "/ChapterConfig.asset";

        /// <summary>8 bölümün başlangıç tanımı. Bölüm 1-3 GAME_DESIGN.md §3'ten (2-3 "~" ile
        /// yaklaşık verilmiş); 4-8 HENÜZ TASARLANMADI → placeholder.</summary>
        private static readonly (string name, string theme, bool placeholder)[] DefaultChapters =
        {
            ("Bölüm 1", "Taş & Doğa",        false),
            ("Bölüm 2", "~ Ateş / Volkanik", true),
            ("Bölüm 3", "~ Teknoloji",       true),
            ("Bölüm 4", "?",                 true),
            ("Bölüm 5", "?",                 true),
            ("Bölüm 6", "?",                 true),
            ("Bölüm 7", "?",                 true),
            ("Bölüm 8", "?",                 true),
        };

        [MenuItem("TacticalRPG/Bolum - 8 Bolum Ilerlemesi Kur", false, 24)]
        public static void SetupChapters()
        {
            GameObject host = FindComponentAnywhere<GameStateManager>()?.gameObject
                              ?? GameObject.Find("GameManager");
            if (host == null)
            {
                if (!_silentSetup)
                    EditorUtility.DisplayDialog("Bolum",
                        "GameManager bulunamadi! Once TAM KURULUM (Faz 0-2) calistir.", "Tamam");
                return;
            }

            ChapterConfigSO config = EnsureChapterConfig();

            var progress = host.GetComponent<ChapterProgress>();
            if (progress == null) progress = host.AddComponent<ChapterProgress>();
            var pso = new SerializedObject(progress);
            pso.FindProperty("_config").objectReferenceValue = config;
            pso.ApplyModifiedProperties();

            // TAB ile 8 bolumluk ilerleme seridi (eski 3x3 ada minimap'inin yerine).
            var mm = host.GetComponent<TacticalRPG.UI.MinimapHUD>();
            if (mm == null) mm = host.AddComponent<TacticalRPG.UI.MinimapHUD>();
            var mmSO = new SerializedObject(mm);
            mmSO.FindProperty("_progress").objectReferenceValue = progress;
            mmSO.ApplyModifiedProperties();

            EditorUtility.SetDirty(host);
            Debug.Log($"[Bolum] {config.Count} bolumluk ilerleme kuruldu (ChapterProgress + TAB seridi).");

            if (!_silentSetup)
                EditorUtility.DisplayDialog("Bolum",
                    $"{config.Count} bolum kuruldu.\n\n" +
                    "• ChapterConfig.asset: Assets/Data/Chapters\n" +
                    "• GameManager -> ChapterProgress (ilerlemenin tek kaynagi)\n" +
                    "• TAB: 8 bolumluk ilerleme seridi\n\n" +
                    "HARITA ekranindaki 8'lik yol icin 'UI - Menu Iskeleti Kur' calistir.", "Tamam");
        }

        /// <summary>ChapterConfig.asset'i yükler; YOKSA varsayılan 8 bölümle oluşturur.
        /// VARSA dokunmaz — kullanıcının Inspector'da düzenlediği ad/tema ezilmez.</summary>
        private static ChapterConfigSO EnsureChapterConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ChapterConfigSO>(ChapterConfigPath);
            if (config != null) return config;

            EnsureFolder(ChapterFolder);
            config = ScriptableObject.CreateInstance<ChapterConfigSO>();
            AssetDatabase.CreateAsset(config, ChapterConfigPath);

            var so   = new SerializedObject(config);
            var list = so.FindProperty("chapters");
            list.arraySize = DefaultChapters.Length;
            for (int i = 0; i < DefaultChapters.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("displayName").stringValue = DefaultChapters[i].name;
                e.FindPropertyRelative("theme").stringValue       = DefaultChapters[i].theme;
                e.FindPropertyRelative("isPlaceholder").boolValue = DefaultChapters[i].placeholder;
            }
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }
    }
}
