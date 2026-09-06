using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.UI;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// KİTAP → KARAKTERLER sayfası (2026-09-06, Efe'nin isteği): savaşta üretilebilen Kam DIŞI
    /// karakterler, sayfa sayfa. Sol sayfada büst + ad + kısa hikaye, sağ sayfada ÜRETİM BEDELİ
    /// (öz) + statlar, altta ◀ / ▶ ve "3 / 7" sayacı.
    ///
    /// KİMLER GİRER: <c>Assets/Data/Recipes</c> altındaki üretim tarifleri — yani savaş öncesi
    /// yerleştirme ekranında GERÇEKTEN üretebildiğin birimler. Liste elle yazılmaz, klasör taranır:
    /// yeni bir tarif eklendiğinde kitap kendiliğinden büyür. Kam (komutan) kapsam dışı: o
    /// üretilmiyor, zaten sahada.
    ///
    /// BÜSTLER YER TUTUCU: <see cref="InkArtFactory.Bust"/> ile prosedürel çizilir (Efe: "çok basit
    /// büst splashartları"). Gerçek görseller gelince tek iş, girdinin <c>_bust</c> alanını
    /// değiştirmek — sayfa kodu aynen çalışır.
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string RecipeFolderPath = "Assets/Data/Recipes";

        [MenuItem("TacticalRPG/UI - Kitap Karakter Sayfasini Kur", false, 30)]
        public static void SetupCharacterPageMenu()
        {
            bool prev = _silentSetup;
            _silentSetup = true;
            try { SetupUIShell(); } finally { _silentSetup = prev; }

            var view = FindInSceneIncludingInactive<CharacterBookView>();
            EditorUtility.DisplayDialog("Kitap - Karakterler",
                view != null ? "KITAP'ta KARAKTER yer imi kuruldu: her karakter bir sayfa, " +
                               "bust + oz maliyeti + statlar.\n\nSAHNEYI KAYDET (Ctrl+S)."
                             : "Kurulamadi: KITAP paneli uretilemedi.",
                "Tamam");
        }

        /// <summary>Batch girişi — Unity KAPALIYKEN kurulum + doğrulama.</summary>
        public static void SetupCharacterPageBatch()
        {
            var scene = EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[Kitap] Sahne acilamadi: {BatchScenePath}");
                EditorApplication.Exit(1);
                return;
            }

            bool prev = _silentSetup;
            _silentSetup = true;
            try { SetupUIShell(); } finally { _silentSetup = prev; }

            var view = FindInSceneIncludingInactive<CharacterBookView>();
            var so   = view != null ? new SerializedObject(view) : null;
            int count = so != null ? so.FindProperty("_entries").arraySize : 0;
            Debug.Log($"[Kitap] DOGRULAMA — karakter sayfasi:{(view != null)} karakter:{count}");

            if (view == null || count == 0) { EditorApplication.Exit(1); return; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // ── Sayfanın kurulumu ────────────────────────────────────────────────

        /// <summary>KİTAP gövdesine karakter sayfasını çizer ve <see cref="CharacterBookView"/>'a bağlar.</summary>
        private static void PopulateCharacterPage(Transform page, GameObject panelGO)
        {
            List<UnitRecipe> recipes = LoadUnitRecipes();

            // ── SOL SAYFA: büst çerçevesi + ad + hikaye ───────────────────────
            RectTransform bustFrame = InkPanel(page, "BustFrame", new Vector2(0.5f, 0.5f),
                new Vector2(-372f, 40f), new Vector2(470f, 470f), 22);

            // İlk büst DOĞRUDAN atanır: sprite'ı null bırakılan Image, çalışma zamanında
            // Refresh gelene kadar dolu bir kutu olarak çizilir (kitap açılışında siyah kare).
            Sprite firstBust = recipes.Count > 0 ? InkArtFactory.Bust(BustFor(recipes[0]), 420) : null;
            var bust = InkImage(bustFrame, "Bust", firstBust, new Vector2(0.5f, 0.5f),
                                new Vector2(0f, 6f), new Vector2(400f, 400f), Ink);
            bust.preserveAspect = true;
            bust.enabled = firstBust != null;

            var nameLabel = CreateCenteredLabel(page, "CharName", "—", new Vector2(0.5f, 0.5f),
                new Vector2(-372f, -224f), new Vector2(520f, 54f), Ink, 40f);

            var loreLabel = CreateCenteredLabel(page, "CharLore", "", new Vector2(0.5f, 0.5f),
                new Vector2(-372f, -292f), new Vector2(520f, 90f), InkSoft, 21f);

            // ── SAĞ SAYFA: bedel + statlar ────────────────────────────────────
            RectTransform costPanel = InkPanel(page, "CostPanel", new Vector2(0.5f, 0.5f),
                new Vector2(372f, 190f), new Vector2(470f, 160f), 18);
            var costLabel = CreateCenteredLabel(costPanel, "CostText", "ÜRETİM BEDELİ",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430f, 130f), Ink, 30f);

            RectTransform statPanel = InkPanel(page, "StatPanel", new Vector2(0.5f, 0.5f),
                new Vector2(372f, -80f), new Vector2(470f, 340f), 18);
            // Statlar İKİ SÜTUN: orantılı fontta boşlukla hizalama tutmuyor (sayılar kayıyordu).
            // Sol sütun adlar, sağ sütun değerler; satırlar birebir eşleşsin diye ikisi de aynı
            // sırayla, aynı satır sayısıyla yazılır (CharacterBookView).
            var statNames = CreateCenteredLabel(statPanel, "StatNames", "", new Vector2(0f, 0.5f),
                new Vector2(46f, 0f), new Vector2(230f, 300f), Ink, 25f);
            statNames.alignment = TextAlignmentOptions.Left;

            var statsLabel = CreateCenteredLabel(statPanel, "StatValues", "", new Vector2(1f, 0.5f),
                new Vector2(-56f, 0f), new Vector2(120f, 300f), Ink, 25f);
            statsLabel.alignment = TextAlignmentOptions.Right;

            // ── Sayfa çevirme ─────────────────────────────────────────────────
            // Ok GLİFİ DEĞİL kelime: TMP'nin varsayılan fontunda U+25C0/U+25B6 yok, yerine boş
            // kutu (tofu) çizilirdi.
            Button prev = InkButton(page, "Btn_PrevChar", "ÖNCEKİ", new Vector2(0.5f, 0f),
                                    new Vector2(196f, 44f), new Vector2(196f, 66f), 24f);
            Button next = InkButton(page, "Btn_NextChar", "SONRAKİ", new Vector2(0.5f, 0f),
                                    new Vector2(548f, 44f), new Vector2(196f, 66f), 24f);
            var pageLabel = CreateCenteredLabel(page, "CharPageNo", "1 / 1", new Vector2(0.5f, 0f),
                new Vector2(372f, 44f), new Vector2(150f, 62f), Ink, 28f);

            // ── Görünümü bağla ────────────────────────────────────────────────
            var view = panelGO.GetComponent<CharacterBookView>();
            if (view == null) view = panelGO.AddComponent<CharacterBookView>();

            var vso = new SerializedObject(view);
            vso.FindProperty("_wallet").objectReferenceValue        = FindComponentAnywhere<EssenceWallet>();
            vso.FindProperty("_essenceConfig").objectReferenceValue = FindEssenceConfig();
            vso.FindProperty("_bustImage").objectReferenceValue     = bust;
            vso.FindProperty("_nameLabel").objectReferenceValue     = nameLabel;
            vso.FindProperty("_loreLabel").objectReferenceValue     = loreLabel;
            vso.FindProperty("_costLabel").objectReferenceValue     = costLabel;
            vso.FindProperty("_statsLabel").objectReferenceValue    = statsLabel;
            vso.FindProperty("_statNameLabel").objectReferenceValue = statNames;
            vso.FindProperty("_pageLabel").objectReferenceValue     = pageLabel;
            vso.FindProperty("_prevButton").objectReferenceValue    = prev;
            vso.FindProperty("_nextButton").objectReferenceValue    = next;

            SerializedProperty arr = vso.FindProperty("_entries");
            arr.arraySize = recipes.Count;
            for (int i = 0; i < recipes.Count; i++)
            {
                SerializedProperty el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("_recipe").objectReferenceValue = recipes[i];
                el.FindPropertyRelative("_bust").objectReferenceValue   =
                    InkArtFactory.Bust(BustFor(recipes[i]), 420);
            }
            vso.ApplyModifiedProperties();

            Debug.Log($"[Kitap] Karakter sayfasi kuruldu — {recipes.Count} karakter " +
                      "(Kam haric, uretim tariflerinden).");
        }

        /// <summary>
        /// Üretim tariflerini klasörden okur (ad sırasıyla). KOMUTAN ELENİR: Kam üretilmiyor,
        /// zaten sahada — kitapta "kaç öze mal olur" sorusunun onda karşılığı yok.
        /// </summary>
        private static List<UnitRecipe> LoadUnitRecipes()
        {
            var list = new List<UnitRecipe>();
            foreach (string guid in AssetDatabase.FindAssets("t:UnitRecipe", new[] { RecipeFolderPath }))
            {
                var r = AssetDatabase.LoadAssetAtPath<UnitRecipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (r == null || r.UnitClass == null || r.UnitClass.IsCommander) continue;
                list.Add(r);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.UnitClass.ClassName, b.UnitClass.ClassName));
            return list;
        }

        /// <summary>Sınıf adından büst çeşidi. Ad eşleşmezse kılıçlı asker (nötr silüet).</summary>
        private static InkBust BustFor(UnitRecipe recipe)
        {
            string n = recipe != null && recipe.UnitClass != null
                     ? recipe.UnitClass.ClassName.ToLowerInvariant() : "";

            if (n.Contains("barbar"))  return InkBust.Balta;
            if (n.Contains("okçu") || n.Contains("okcu") || n.Contains("ranger")) return InkBust.Yay;
            if (n.Contains("büyücü") || n.Contains("buyucu")) return InkBust.Asa;
            if (n.Contains("rahip"))   return InkBust.Hale;
            if (n.Contains("serseri")) return InkBust.Hancer;
            return InkBust.Kilic;                     // savaşçı ve tanınmayanlar
        }
    }
}
