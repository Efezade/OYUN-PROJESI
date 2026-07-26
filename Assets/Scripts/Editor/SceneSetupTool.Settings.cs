using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using TacticalRPG.Core;
using TacticalRPG.UI;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// SceneSetupTool'un AYARLAR parçası: AYARLAR panelinin GERÇEK içeriğini + arkasındaki "modelleri"
    /// (<see cref="GameAudio"/>, <see cref="DisplaySettings"/>) ve tam-ekran PARLAKLIK kaplamasını
    /// programatik kurar (SetupDebugHUD deseni — her şey kodla kurulur, elle prefab yok).
    ///
    /// Modeller sahne kökünde HER ZAMAN AKTİF çocuklara eklenir (menü gizliyken de açılışta prefs uygular);
    /// SettingsController panelin kendisine (görünüm). Müzik klibi Assets/Audio/Music'ten yüklenir.
    /// </summary>
    public static partial class SceneSetupTool
    {
        private const string MusicClipPath = "Assets/Audio/Music/medieval_theme_placeholder.wav";

        // Panel yerleşimi (1920x1080 referans; üst-merkez ankraj, y yukarıdan aşağı azalır)
        private const float RowLabelX  = -330f;
        private const float RowSliderX =   40f;
        private const float RowValueX  =  310f;
        private const float RowBtnX    =  330f;

        private static void PopulateSettingsScreen(GameObject panelGO)
        {
            Transform t = panelGO.transform;
            GameObject sceneRoot = GameObject.Find(SceneRootName);
            Transform modelParent = sceneRoot != null ? sceneRoot.transform : null;

            // ── Eski model/kaplama nesnelerini temizle (idempotent) ───────────
            DestroyIfExists<GameAudio>();
            DestroyIfExists<DisplaySettings>();
            GameObject oldBr = GameObject.Find("Brightness_Canvas");
            if (oldBr != null) Object.DestroyImmediate(oldBr);

            // ── Parlaklık kaplaması: en üstteki canvas + tek tam-ekran Image ──
            Image brightnessOverlay = CreateBrightnessOverlay(modelParent);

            // ── Modeller (hep aktif çocuklar) ─────────────────────────────────
            GameObject audioGO = new GameObject("GameAudio");
            if (modelParent != null) audioGO.transform.SetParent(modelParent, false);
            GameAudio audio = audioGO.AddComponent<GameAudio>();
            AudioClip music = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicClipPath);
            var aso = new SerializedObject(audio);
            aso.FindProperty("_backgroundMusic").objectReferenceValue = music;
            aso.ApplyModifiedProperties();

            GameObject dispGO = new GameObject("DisplaySettings");
            if (modelParent != null) dispGO.transform.SetParent(modelParent, false);
            DisplaySettings display = dispGO.AddComponent<DisplaySettings>();
            var dso = new SerializedObject(display);
            dso.FindProperty("_brightnessOverlay").objectReferenceValue = brightnessOverlay;
            dso.ApplyModifiedProperties();

            // ── Başlık ────────────────────────────────────────────────────────
            CreateCenteredLabel(t, "Title", "AYARLAR",
                new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(600f, 70f),
                new Color(0.95f, 0.90f, 0.75f), 54f);

            // ── SES bölümü ─────────────────────────────────────────────────────
            float y = -140f;
            CreateSectionHeader(t, "SES", ref y);
            var masterSlider = CreateSliderRow(t, "MASTER", ref y, 0f, 1f, out var masterVal);
            var musicSlider  = CreateSliderRow(t, "MÜZİK",  ref y, 0f, 1f, out var musicVal);
            var sfxSlider    = CreateSliderRow(t, "SFX",    ref y, 0f, 1f, out var sfxVal);

            // ── GÖRÜNTÜ bölümü ─────────────────────────────────────────────────
            y -= 24f;
            CreateSectionHeader(t, "GÖRÜNTÜ", ref y);
            var brSlider = CreateSliderRow(t, "PARLAKLIK", ref y, 0.5f, 1.5f, out var brVal);
            var qualityBtn    = CreateButtonRow(t, "KALİTE",    "DEĞİŞTİR", ref y, out var qualityVal);
            var fullscreenBtn = CreateButtonRow(t, "TAM EKRAN", "AÇ / KAPA", ref y, out var fullscreenVal);
            var vsyncBtn      = CreateButtonRow(t, "VSYNC",     "AÇ / KAPA", ref y, out var vsyncVal);

            // ── Görünüm bileşeni + tüm bağlar ─────────────────────────────────
            SettingsController ctrl = panelGO.AddComponent<SettingsController>();
            var cso = new SerializedObject(ctrl);
            cso.FindProperty("_audio").objectReferenceValue   = audio;
            cso.FindProperty("_display").objectReferenceValue = display;
            cso.FindProperty("_masterSlider").objectReferenceValue = masterSlider;
            cso.FindProperty("_musicSlider").objectReferenceValue  = musicSlider;
            cso.FindProperty("_sfxSlider").objectReferenceValue    = sfxSlider;
            cso.FindProperty("_masterValue").objectReferenceValue  = masterVal;
            cso.FindProperty("_musicValue").objectReferenceValue   = musicVal;
            cso.FindProperty("_sfxValue").objectReferenceValue     = sfxVal;
            cso.FindProperty("_brightnessSlider").objectReferenceValue = brSlider;
            cso.FindProperty("_brightnessValue").objectReferenceValue  = brVal;
            cso.FindProperty("_qualityValue").objectReferenceValue     = qualityVal;
            cso.FindProperty("_fullscreenValue").objectReferenceValue  = fullscreenVal;
            cso.FindProperty("_vsyncValue").objectReferenceValue       = vsyncVal;
            cso.ApplyModifiedProperties();

            // Buton onClick'leri controller metodlarına (persistent listener)
            UnityEditor.Events.UnityEventTools.AddPersistentListener(qualityBtn.onClick,    ctrl.OnCycleQuality);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(fullscreenBtn.onClick, ctrl.OnToggleFullscreen);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(vsyncBtn.onClick,      ctrl.OnToggleVSync);

            CreateCenteredLabel(t, "SettingsHint",
                "Değişiklikler anında kaydolur (PlayerPrefs) · müzik telifsiz placeholder · Kapat: Esc",
                new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(1300f, 40f),
                new Color(0.60f, 0.55f, 0.48f), 24f);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Yerleşim yardımcıları
        // ─────────────────────────────────────────────────────────────────────

        private static void CreateSectionHeader(Transform parent, string text, ref float y)
        {
            CreateCenteredLabel(parent, "Sec_" + text, text,
                new Vector2(0.5f, 1f), new Vector2(RowLabelX - 60f, y), new Vector2(360f, 46f),
                new Color(0.85f, 0.78f, 0.55f), 34f);
            y -= 64f;
        }

        /// <summary>Bir slider satırı: sol etiket + slider + sağ % değeri. y'yi bir sonraki satıra iler.</summary>
        private static Slider CreateSliderRow(Transform parent, string label, ref float y,
            float min, float max, out TextMeshProUGUI valueLabel)
        {
            CreateCenteredLabel(parent, "Lbl_" + label, label,
                new Vector2(0.5f, 1f), new Vector2(RowLabelX, y), new Vector2(260f, 46f),
                new Color(0.88f, 0.84f, 0.72f), 28f);

            Slider slider = CreateSlider(parent, "Sld_" + label,
                new Vector2(RowSliderX, y - 6f), new Vector2(420f, 30f), min, max);

            valueLabel = CreateCenteredLabel(parent, "Val_" + label, "0%",
                new Vector2(0.5f, 1f), new Vector2(RowValueX, y), new Vector2(140f, 46f),
                new Color(0.95f, 0.90f, 0.75f), 28f);

            y -= 66f;
            return slider;
        }

        /// <summary>Bir buton satırı: sol etiket + orta değer etiketi + sağ eylem butonu.</summary>
        private static Button CreateButtonRow(Transform parent, string label, string btnText, ref float y,
            out TextMeshProUGUI valueLabel)
        {
            CreateCenteredLabel(parent, "Lbl_" + label, label,
                new Vector2(0.5f, 1f), new Vector2(RowLabelX, y), new Vector2(260f, 46f),
                new Color(0.88f, 0.84f, 0.72f), 28f);

            valueLabel = CreateCenteredLabel(parent, "Val_" + label, "—",
                new Vector2(0.5f, 1f), new Vector2(RowSliderX + 30f, y), new Vector2(260f, 46f),
                new Color(0.95f, 0.90f, 0.75f), 28f);

            Button btn = CreateUIButton(parent, "Btn_" + label, btnText,
                new Vector2(0.5f, 1f), new Vector2(RowBtnX, y - 6f), new Vector2(230f, 52f),
                new Color(0.16f, 0.13f, 0.10f, 0.92f), 24f);

            y -= 66f;
            return btn;
        }

        /// <summary>Tam işlevsel uGUI Slider (Background + Fill + Handle) programatik kurar.</summary>
        private static Slider CreateSlider(Transform parent, string name,
            Vector2 anchoredPos, Vector2 size, float min, float max)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;

            // Arka oluk
            Image bg = CreateStretchImage(go.transform, "Background",
                new Color(0.08f, 0.07f, 0.05f, 1f));

            // Fill Area → Fill (slider fillRect'i doldurur)
            GameObject fillArea = CreateStretchChild(go.transform, "Fill Area");
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.offsetMin = new Vector2(8f, 0f); faRt.offsetMax = new Vector2(-8f, 0f);
            Image fill = CreateStretchImage(fillArea.transform, "Fill",
                new Color(0.72f, 0.55f, 0.28f, 1f));

            // Handle Slide Area → Handle
            GameObject slideArea = CreateStretchChild(go.transform, "Handle Slide Area");
            var saRt = slideArea.GetComponent<RectTransform>();
            saRt.offsetMin = new Vector2(8f, 0f); saRt.offsetMax = new Vector2(-8f, 0f);
            GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGO.transform.SetParent(slideArea.transform, false);
            var hRt = handleGO.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 0f); hRt.anchorMax = new Vector2(0f, 1f);
            hRt.pivot = new Vector2(0.5f, 0.5f);
            hRt.sizeDelta = new Vector2(22f, 6f);
            Image handleImg = handleGO.GetComponent<Image>();
            handleImg.color = new Color(0.95f, 0.90f, 0.75f, 1f);

            Slider slider = go.GetComponent<Slider>();
            slider.fillRect      = fill.rectTransform;
            slider.handleRect    = hRt;
            slider.targetGraphic = handleImg;
            slider.direction     = Slider.Direction.LeftToRight;
            slider.minValue      = min;
            slider.maxValue      = max;
            slider.wholeNumbers  = false;
            slider.value         = min; // gerçek değeri SettingsController.OnEnable senkronlar
            return slider;
        }

        private static Image CreateBrightnessOverlay(Transform parent)
        {
            GameObject canvasGO = new GameObject("Brightness_Canvas", typeof(RectTransform));
            if (parent != null) canvasGO.transform.SetParent(parent, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900; // her şeyin (menü sort=100 dahil) üstünde
            canvasGO.AddComponent<CanvasScaler>();

            GameObject imgGO = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            imgGO.transform.SetParent(canvasGO.transform, false);
            StretchFull(imgGO.GetComponent<RectTransform>());
            Image img = imgGO.GetComponent<Image>();
            img.color         = new Color(0f, 0f, 0f, 0f); // nötr başla; DisplaySettings.Awake uygular
            img.raycastTarget = false;                     // asla girişi engellemez
            return img;
        }

        private static Image CreateStretchImage(Transform parent, string name, Color color)
        {
            GameObject go = CreateStretchChild(parent, name);
            Image img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;
            return img;
        }

        private static GameObject CreateStretchChild(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            StretchFull(go.GetComponent<RectTransform>());
            return go;
        }

        private static void DestroyIfExists<T>() where T : Component
        {
            T c = FindComponentAnywhere<T>();
            if (c != null) Object.DestroyImmediate(c.gameObject);
        }
    }
}
