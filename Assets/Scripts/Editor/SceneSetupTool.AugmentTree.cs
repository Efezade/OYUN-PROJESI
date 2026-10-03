using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;
using TacticalRPG.UI;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// DEĞİŞTİRİLEBİLİR KAROLAR AĞACI kurulumu (Efe'nin isteği 2026-10-03): ayar asset'i +
    /// <see cref="AugmentTreeProgress"/> bileşeni + KİTAP'ın KAROLAR sekmesi + davul/bölüm bağları.
    ///
    /// Sayfa yetenek ağacıyla AYNI kurucudan geçer (<see cref="PopulateTreePage"/>) — tek fark
    /// düğümlerin karo kataloğundan gelmesi ve dalların karo GRUPLARI olması. Sayfa, KİTAP
    /// kabuğu (SetupUIShell) her kurulduğunda yeniden çizilir; yani TAM KURULUM'a ve
    /// "UI - Kam Yetenek Agacini Kur" menüsüne kendiliğinden dahildir.
    ///
    /// ASSET İDEMPOTENT: `AugmentTree.asset` VARSA dokunulmaz (CLAUDE.md §9.1).
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string AugmentTreeAssetPath = "Assets/Data/Config/AugmentTree.asset";

        // Dal merkezleri (KİTAP gövdesi 1500x780, merkez 0,0). Her dalda düğümler sol/sağ iki
        // sütun halinde durur; satırlar aşağıdan yukarı.
        private static readonly float[] AugBranchX = { -560f, -280f, 0f, 280f, 560f };
        private const float AugColOffset = 70f;
        private static readonly float[] AugRowY = { 10f, 150f, 290f };

        /// <summary>KİTAP'ın KAROLAR sayfası (SetupUIShell çağırır).</summary>
        private static void PopulateAugmentPage(Transform page)
        {
            AugmentTreeSO       tree     = EnsureAugmentTreeAsset();
            AugmentTreeProgress progress = EnsureAugmentProgress(tree);

            var groups = new List<(string, Vector2)>
            {
                ("KUT · yandaşa",    new Vector2(AugBranchX[0], -112f)),
                ("KARGIŞ · düşmana", new Vector2(AugBranchX[1], -112f)),
                ("NÖTR · arazi",     new Vector2(AugBranchX[2], -112f)),
                ("PATLAYICI",        new Vector2(AugBranchX[3], -112f)),
                ("SINIFSAL",         new Vector2(AugBranchX[4], -112f)),
            };

            PopulateTreePage(page, "TileTitle", "KARO AĞACI", InkIcon.Gear, "Tile", tree, progress,
                             n =>
                             {
                                 AugmentCatalog.Entry e = AugmentCatalog.Get(n.Id);
                                 return (e != null ? e.Name : n.Id, AugIconFor(e));
                             },
                             nameWidth: 128f, nameFont: 15f, branchPrefix: "augtree_branches",
                             groupLabels: groups);

            WireAugmentTreeConsumers(progress);
        }

        /// <summary>Davul ve bölüm yöneticisi karo ağacını tanısın.</summary>
        private static void WireAugmentTreeConsumers(AugmentTreeProgress progress)
        {
            var drum = FindComponentAnywhere<CombatDrumManager>();
            if (drum != null)
            {
                var dso = new SerializedObject(drum);
                dso.FindProperty("_tileTree").objectReferenceValue = progress;
                dso.ApplyModifiedProperties();
            }

            // Karo ağacı da yetenek ağacı gibi ÖLÜNCE SIFIRLANIR (aynı kural).
            var run = FindComponentAnywhere<ChapterRunManager>();
            if (run != null)
            {
                var rso = new SerializedObject(run);
                rso.FindProperty("_tileTree").objectReferenceValue = progress;
                rso.ApplyModifiedProperties();
            }

            Debug.Log($"[KaroAgaci] DOGRULAMA — davul:{(drum != null)} bolum-sifirlama:{(run != null)} " +
                      $"dugum:{(progress != null && progress.Tree != null ? progress.Tree.Nodes.Count : 0)}");
        }

        private static AugmentTreeProgress EnsureAugmentProgress(AugmentTreeSO tree)
        {
            // Yetenek ilerlemesiyle aynı nesnede: ikisi de "run boyunca yaşayan ekonomi".
            EssenceWallet wallet = FindComponentAnywhere<EssenceWallet>();
            GameObject host = wallet != null ? wallet.gameObject : GameObject.Find(SceneRootName);
            if (host == null) return null;

            var progress = host.GetComponent<AugmentTreeProgress>();
            if (progress == null) progress = host.AddComponent<AugmentTreeProgress>();

            var so = new SerializedObject(progress);
            so.FindProperty("_tree").objectReferenceValue   = tree;
            so.FindProperty("_wallet").objectReferenceValue = wallet;
            so.ApplyModifiedProperties();
            return progress;
        }

        /// <summary>Karonun etkisine göre mürekkep ikonu (kilitliyken asma kilit çizilir).</summary>
        private static InkIcon AugIconFor(AugmentCatalog.Entry e)
            => e == null ? InkIcon.Star : e.Effect switch
            {
                AugmentEffect.Regen       => InkIcon.Drop,
                AugmentEffect.Defense     => InkIcon.Shield,
                AugmentEffect.Damage      => InkIcon.Sword,
                AugmentEffect.EntryDamage => InkIcon.Sword,
                AugmentEffect.Move        => InkIcon.Wind,
                AugmentEffect.Initiative  => InkIcon.Wind,
                AugmentEffect.Stun        => InkIcon.Spiral,
                AugmentEffect.Explode     => InkIcon.Flame,
                AugmentEffect.Impassable  => InkIcon.Gear,
                AugmentEffect.BlockSight  => InkIcon.Gear,
                AugmentEffect.ExtraAction => InkIcon.Hand,
                AugmentEffect.Range       => InkIcon.Leaf,
                AugmentEffect.Mana        => InkIcon.Star,
                _                         => InkIcon.Star
            };

        // ── Ayar asset'i ─────────────────────────────────────────────────────

        /// <summary>
        /// AugmentTree.asset'i yükler; YOKSA varsayılan ağaçla üretir. BEŞ DAL = beş karo grubu;
        /// her dalın ilk iki karosu AÇIK başlar (davul her vuruşta Kut + Kargış + Nötr/Patlayıcı
        /// sunar — bir grubun havuzu boş kalırsa o vuruş eksik kartla gelirdi).
        ///
        /// KORKU SİSİ YOK: isabet sistemi gelene kadar draftta zaten çıkmıyor; ağaçta durursa
        /// oyuncu çalışmayan bir karoya öz harcardı.
        ///
        /// Seviye ne büyütür: sayısal karolarda büyüklük, arazi karolarında karo sayısı. Sersemletme
        /// ve aksiyon karoları yalnız AÇILIR (1 tur sersemletme / +1 aksiyon zaten güçlü).
        /// SAYILAR ÖRNEK (CLAUDE.md §9: denge işi durduruldu).
        /// </summary>
        private static AugmentTreeSO EnsureAugmentTreeAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AugmentTreeSO>(AugmentTreeAssetPath);
            if (existing != null) return existing;

            EnsureFolder("Assets/Data/Config");
            var tree = ScriptableObject.CreateInstance<AugmentTreeSO>();
            AssetDatabase.CreateAsset(tree, AugmentTreeAssetPath);

            // (id, ön koşul, açık başlar, dal, sütun -1/0/+1, satır, tavan, güç/sv, karo/sv)
            var spec = new (string id, string req, bool open, int branch, int col, int row,
                            int max, int mag, int tiles)[]
            {
                // KUT — yandaşa artı
                ("ocak",          "",              true,  0, -1, 0, 3, 1, 0),
                ("kalkan_tasi",   "",              true,  0, +1, 0, 3, 1, 0),
                ("ruzgar_tasi",   "ocak",          false, 0, -1, 1, 2, 1, 0),
                ("ata_tasi",      "kalkan_tasi",   false, 0, +1, 1, 3, 1, 0),
                ("ofke_tasi",     "ruzgar_tasi",   false, 0,  0, 2, 3, 1, 0),
                // KARGIŞ — düşmana eksi
                ("camur",         "",              true,  1, -1, 0, 3, 1, 0),
                ("agirlik",       "",              true,  1, +1, 0, 3, 1, 0),
                ("diken",         "camur",         false, 1, -1, 1, 3, 2, 0),
                ("tuzak_tasi",    "agirlik",       false, 1, +1, 1, 1, 0, 0),
                // NÖTR — herkese / arazi
                ("duvar",         "",              true,  2, -1, 0, 2, 0, 1),
                ("sarsinti",      "",              true,  2, +1, 0, 2, 1, 0),
                ("bosluk",        "duvar",         false, 2, -1, 1, 2, 0, 1),
                ("ruh_kapisi",    "sarsinti",      false, 2, +1, 1, 1, 0, 0),
                // PATLAYICI — bir kez tetiklenir
                ("cig_tasi",      "",              true,  3, -1, 0, 2, 0, 1),
                ("ates_ficisi",   "",              true,  3, +1, 0, 3, 2, 0),
                ("buz_kabugu",    "cig_tasi",      false, 3, -1, 1, 1, 0, 0),
                ("ruh_bombasi",   "ates_ficisi",   false, 3, +1, 1, 3, 1, 0),
                // SINIFSAL — yalnız o sınıf sahadaysa çıkar
                ("kalkan_duvari", "",              true,  4, -1, 0, 3, 1, 0),
                ("nisan_kayasi",  "",              true,  4, +1, 0, 2, 1, 0),
                ("ley_damari",    "kalkan_duvari", false, 4, -1, 1, 1, 0, 0),
                ("golge_yarigi",  "nisan_kayasi",  false, 4, +1, 1, 3, 1, 0),
                ("kutsal_zemin",  "ley_damari",    false, 4, -1, 2, 3, 1, 0),
                ("davul_tasi",    "golge_yarigi",  false, 4, +1, 2, 1, 0, 0),
            };

            var so = new SerializedObject(tree);
            SerializedProperty nodes = so.FindProperty("_nodes");
            nodes.arraySize = spec.Length;

            for (int i = 0; i < spec.Length; i++)
            {
                var s = spec[i];
                // Bedel satıra göre artar: kök ucuz, üst basamak pahalı.
                int unlockTas  = s.open ? 0 : (s.row == 1 ? 8 : 16);
                int unlockDoga = s.open ? 0 : (s.row == 1 ? 6 : 10);

                SerializedProperty el = nodes.GetArrayElementAtIndex(i);
                WriteNode(el, s.id, s.req, s.open,
                          new Vector2(AugBranchX[s.branch] + s.col * AugColOffset, AugRowY[s.row]),
                          unlockTas, unlockDoga, 5, 4, magnitude: s.mag, radius: 0, push: 0, stun: 0);
                el.FindPropertyRelative("_maxLevel").intValue      = s.max;
                el.FindPropertyRelative("_tilesPerLevel").intValue = s.tiles;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();
            Debug.Log($"[KaroAgaci] AugmentTree.asset uretildi — {spec.Length} dugum / 5 dal.");
            return tree;
        }
    }
}
