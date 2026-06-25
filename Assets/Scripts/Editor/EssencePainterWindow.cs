using System;
using UnityEngine;
using UnityEditor;
using TacticalRPG.Core;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Hex harita üzerine ÖZ boyama penceresi (rastgele değil — el yapımı).
    /// Kullanım:
    ///   1. TacticalRPG → Essence Painter - Oz Boyama
    ///   2. Referanslar otomatik bulunur (EssenceNodeManager'dan config + map)
    ///   3. Öz türünü seç (Ateş/Su/Toprak) + "Fırça miktarı"nı gir (örn. 3)
    ///   4. "Boyamayı Başlat" → Scene'de hex'e SOL tık = seçili türden o kadar EKLE (stack),
    ///      SAĞ tık = o karodaki tüm özleri sil.
    ///   • Aynı karoya farklı tür eklemek için türü değiştirip yine sol tıkla (üst üste binmez).
    /// </summary>
    public class EssencePainterWindow : EditorWindow
    {
        private HexGridManager      _grid;
        private EssenceNodeManager  _manager;
        private EssenceConfigSO     _config;
        private EssenceMapSO        _map;

        private static readonly EssenceType[] AllTypes =
            { EssenceType.Ates, EssenceType.Su, EssenceType.Toprak };

        private int  _selected   = 0;
        private int  _brushAmount = 1;
        private bool _isPainting  = false;
        private bool _hasHovered  = false;
        private HexCoordinate _hoveredCoord;

        [MenuItem("TacticalRPG/Essence Painter - Oz Boyama", false, 22)]
        public static void OpenWindow()
        {
            var w = GetWindow<EssencePainterWindow>("Essence Painter");
            w.minSize = new Vector2(280, 360);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            AutoFindReferences();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            _isPainting = false;
        }

        private void AutoFindReferences()
        {
#if UNITY_2023_1_OR_NEWER
            _grid    = UnityEngine.Object.FindFirstObjectByType<HexGridManager>();
            _manager = UnityEngine.Object.FindFirstObjectByType<EssenceNodeManager>();
#else
            _grid    = UnityEngine.Object.FindObjectOfType<HexGridManager>();
            _manager = UnityEngine.Object.FindObjectOfType<EssenceNodeManager>();
#endif
            if (_manager != null)
            {
                if (_config == null) _config = _manager.Config;
                if (_map == null)    _map    = _manager.Map;
            }
        }

        // ── GUI ──────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawReferences();

            if (_grid == null || _map == null)
            {
                EditorGUILayout.HelpBox(
                    "Grid Manager ve Essence Map gerekli.\n" +
                    "TAM KURULUM (veya Faz D) calistirdiysan otomatik bulunmalilar.",
                    MessageType.Info);
                if (GUILayout.Button("Yeniden Tara")) AutoFindReferences();
                return;
            }

            EditorGUILayout.Space(6);
            DrawPalette();
            EditorGUILayout.Space(6);
            DrawControls();
        }

        private void DrawReferences()
        {
            EditorGUILayout.LabelField("Referanslar", EditorStyles.boldLabel);
            _grid = (HexGridManager)EditorGUILayout.ObjectField(
                "Grid Manager", _grid, typeof(HexGridManager), true);
            _config = (EssenceConfigSO)EditorGUILayout.ObjectField(
                "Essence Config", _config, typeof(EssenceConfigSO), false);
            _map = (EssenceMapSO)EditorGUILayout.ObjectField(
                "Essence Map", _map, typeof(EssenceMapSO), false);
        }

        private void DrawPalette()
        {
            EditorGUILayout.LabelField("Öz Türleri", EditorStyles.boldLabel);

            for (int i = 0; i < AllTypes.Length; i++)
            {
                EssenceType t          = AllTypes[i];
                bool        isSelected = i == _selected;
                Color       color      = _config != null ? _config.ColorOf(t) : DefaultColor(t);
                string      name       = _config != null ? _config.NameOf(t)  : t.ToString();

                Color prevBG = GUI.backgroundColor;
                GUI.backgroundColor = isSelected ? Color.white : color * 0.6f;

                Rect r = EditorGUILayout.GetControlRect(false, 30);
                if (GUI.Button(r, GUIContent.none,
                        isSelected ? EditorStyles.helpBox : EditorStyles.miniButton))
                    _selected = i;

                EditorGUI.DrawRect(new Rect(r.x + 4, r.y + 5, 20, 20), color);
                var labelStyle = new GUIStyle(EditorStyles.label);
                if (isSelected) labelStyle.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(r.x + 30, r.y + 6, r.width - 34, 20), name, labelStyle);

                GUI.backgroundColor = prevBG;
            }

            EditorGUILayout.Space(4);
            _brushAmount = Mathf.Max(1, EditorGUILayout.IntField("Fırça miktarı", _brushAmount));
        }

        private void DrawControls()
        {
            Color prevColor = GUI.color;
            GUI.color = _isPainting ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 1f, 0.6f);
            string btnLabel = _isPainting ? "⏹  Boyamayı Durdur" : "▶  Boyamayı Başlat";
            if (GUILayout.Button(btnLabel, GUILayout.Height(38)))
            {
                _isPainting = !_isPainting;
                if (_isPainting) EnsureGridCells();
                SceneView.RepaintAll();
            }
            GUI.color = prevColor;

            if (_isPainting)
                EditorGUILayout.HelpBox(
                    "Sol tık  → seçili türden 'fırça miktarı' kadar EKLE (stack)\n" +
                    "Sağ tık  → bu karodaki TÜM özleri sil\n" +
                    "Tür değiştir + sol tık → aynı karoya farklı tür ekle",
                    MessageType.None);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Öz bulunan karo: {CountTiles()}", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Tüm Öz Haritasını Sıfırla"))
            {
                if (EditorUtility.DisplayDialog("Sıfırla",
                    "Tüm öz yerleşimleri silinecek. Emin misin?", "Evet", "İptal"))
                {
                    _map.ClearAll();
                    MarkDirtyAndSave();
                    SceneView.RepaintAll();
                }
            }
        }

        // ── Scene GUI (boyama + önizleme) ─────────────────────────────────────

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_map == null || _grid == null) return;

            DrawAllEssencePreview();

            if (!_isPainting) return;

            Event e   = Event.current;
            Ray   ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var   plane = new Plane(Vector3.up, new Vector3(0f, HexMetrics.TileHeight, 0f));
            _hasHovered = false;

            if (plane.Raycast(ray, out float dist))
            {
                Vector3 worldPt = ray.GetPoint(dist);
                _hoveredCoord = _grid.WorldToHex(worldPt);
                _hasHovered   = _grid.IsInBounds(_hoveredCoord);
            }

            if (_hasHovered)
            {
                Color color = _config != null ? _config.ColorOf(AllTypes[_selected]) : DefaultColor(AllTypes[_selected]);
                DrawHexHighlight(_hoveredCoord, color);
                sceneView.Repaint();
            }

            if (_hasHovered)
            {
                // Sol tık: tek tıkta EKLE (sürüklemede istemeden birikmesin).
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    _map.AddAmount(_hoveredCoord, AllTypes[_selected], _brushAmount);
                    MarkDirtyAndSave();
                    e.Use();
                }
                // Sağ tık/sürükle: karoyu temizle (alan silme kolay olsun).
                else if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 1)
                {
                    _map.ClearCoord(_hoveredCoord);
                    MarkDirtyAndSave();
                    e.Use();
                }
            }

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        // Haritadaki tüm özleri renkli disk + "3T 1S" etiketiyle önizler (edit modunda).
        private void DrawAllEssencePreview()
        {
            float hexSize = _grid.HexSize;
            int   typeCount = AllTypes.Length;

            foreach (var kv in _map.BuildLookup(typeCount))
            {
                Vector3 center = kv.Key.ToWorldPosition(hexSize) + Vector3.up * (HexMetrics.TileHeight + 0.12f);

                int present = 0;
                for (int t = 0; t < kv.Value.Length; t++) if (kv.Value[t] > 0) present++;
                int idx = 0;
                var label = new System.Text.StringBuilder();

                for (int t = 0; t < kv.Value.Length; t++)
                {
                    if (kv.Value[t] <= 0) continue;
                    var   type = (EssenceType)t;
                    Color color = _config != null ? _config.ColorOf(type) : DefaultColor(type);
                    float ang   = present > 1 ? (idx / (float)present) * Mathf.PI * 2f : 0f;
                    float r     = present > 1 ? 0.34f : 0f;
                    Vector3 pos = center + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);

                    Handles.color = color;
                    Handles.DrawSolidDisc(pos, Vector3.up, 0.1f);

                    if (label.Length > 0) label.Append(' ');
                    string nm = _config != null ? _config.NameOf(type) : type.ToString();
                    label.Append($"{kv.Value[t]}{(nm.Length > 0 ? nm[0].ToString() : "?")}");
                    idx++;
                }

                Handles.color = Color.white;
                Handles.Label(center + Vector3.up * 0.25f, label.ToString());
            }
        }

        private void DrawHexHighlight(HexCoordinate coord, Color color)
        {
            float   yOffset = HexMetrics.TileHeight + 0.03f;
            Vector3 center  = coord.ToWorldPosition(_grid.HexSize) + Vector3.up * yOffset;
            float   scale   = 0.94f;

            var pts = new Vector3[7];
            for (int i = 0; i < 6; i++)
            {
                Vector3 c = HexMetrics.Corners[i] * scale;
                pts[i] = center + new Vector3(c.x, 0f, c.z);
            }
            pts[6] = pts[0];

            Handles.color = new Color(color.r, color.g, color.b, 0.9f);
            Handles.DrawAAPolyLine(4f, pts);

            Handles.color = new Color(color.r, color.g, color.b, 0.15f);
            var fan = new Vector3[3];
            for (int i = 0; i < 6; i++)
            {
                fan[0] = center; fan[1] = pts[i]; fan[2] = pts[(i + 1) % 6];
                Handles.DrawAAConvexPolygon(fan);
            }
        }

        // ── Yardımcılar ───────────────────────────────────────────────────────

        private int CountTiles()
        {
            int count = 0;
            foreach (var _ in _map.BuildLookup(AllTypes.Length)) count++;
            return count;
        }

        private void EnsureGridCells()
        {
            if (_grid != null && !_grid.HasCells) _grid.GenerateGrid();
        }

        private void MarkDirtyAndSave()
        {
            EditorUtility.SetDirty(_map);
            AssetDatabase.SaveAssets();
        }

        private static Color DefaultColor(EssenceType t) => t switch
        {
            EssenceType.Ates   => new Color(0.90f, 0.25f, 0.20f),
            EssenceType.Su     => new Color(0.25f, 0.50f, 0.95f),
            EssenceType.Toprak => new Color(0.35f, 0.75f, 0.35f),
            _                  => Color.white
        };
    }
}
