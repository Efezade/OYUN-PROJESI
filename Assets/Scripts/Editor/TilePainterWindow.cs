using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Editor
{
    /// <summary>
    /// Hex harita üzerine karo türü boyama penceresi.
    /// Kullanım:
    ///   1. TacticalRPG → Tile Painter - Karo Boyama
    ///   2. Referansları doldur (otomatik bulunur)
    ///   3. Paletten karo türünü seç
    ///   4. "Boyamayı Başlat" → Scene'de hex karolara sol tıkla
    ///   5. "Görüntüyü Yenile" ile sonucu gör
    /// </summary>
    public class TilePainterWindow : EditorWindow
    {
        private HexGridManager _gridManager;
        private TilePaletteSO  _palette;
        private TileMapSO      _tileMap;

        private int     _selectedIndex = 0;
        private bool    _isPainting    = false;
        private bool    _hasHovered    = false;
        private HexCoordinate _hoveredCoord;

        private Vector2 _scroll;

        [MenuItem("TacticalRPG/Tile Painter - Karo Boyama", false, 20)]
        public static void OpenWindow()
        {
            var w = GetWindow<TilePainterWindow>("Tile Painter");
            w.minSize = new Vector2(280, 420);
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
            _gridManager = Object.FindFirstObjectByType<HexGridManager>();
#else
            _gridManager = Object.FindObjectOfType<HexGridManager>();
#endif
            if (_gridManager != null)
            {
                _palette = _gridManager.TilePalette;
                _tileMap = _gridManager.TileMap;
            }
        }

        // ── GUI ──────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawReferences();

            if (_gridManager == null || _palette == null || _tileMap == null)
            {
                EditorGUILayout.HelpBox(
                    "Tüm referansları doldur.\n" +
                    "TAM KURULUM menüsünü çalıştırdıysan otomatik bulunmalılar.",
                    MessageType.Info);
                if (GUILayout.Button("Yeniden Tara"))
                    AutoFindReferences();
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
            _gridManager = (HexGridManager)EditorGUILayout.ObjectField(
                "Grid Manager", _gridManager, typeof(HexGridManager), true);
            _palette = (TilePaletteSO)EditorGUILayout.ObjectField(
                "Tile Palette", _palette, typeof(TilePaletteSO), false);
            _tileMap = (TileMapSO)EditorGUILayout.ObjectField(
                "Tile Map", _tileMap, typeof(TileMapSO), false);
        }

        private void DrawPalette()
        {
            EditorGUILayout.LabelField("Karo Paleti", EditorStyles.boldLabel);

            if (_palette.tiles.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Paleti doldurmak için:\n" +
                    "Assets/Data/Map/TilePalette asset'ini seç → Inspector'dan karo ekle.\n" +
                    "Her karonun kendi Blender FBX prefabı olacak.",
                    MessageType.Warning);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(180));

            for (int i = 0; i < _palette.tiles.Count; i++)
            {
                var  entry      = _palette.tiles[i];
                bool isSelected = i == _selectedIndex;

                Color prevBG = GUI.backgroundColor;
                GUI.backgroundColor = isSelected ? Color.white : entry.editorColor * 0.6f;

                Rect r = EditorGUILayout.GetControlRect(false, 30);
                if (GUI.Button(r, GUIContent.none,
                        isSelected ? EditorStyles.helpBox : EditorStyles.miniButton))
                    _selectedIndex = i;

                // Renk karesi
                EditorGUI.DrawRect(new Rect(r.x + 4, r.y + 5, 20, 20), entry.editorColor);

                // İsim + id
                var labelStyle = new GUIStyle(EditorStyles.label);
                if (isSelected) labelStyle.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(r.x + 30, r.y + 6, r.width - 34, 20),
                    $"{entry.displayName}  [{entry.id}]", labelStyle);

                GUI.backgroundColor = prevBG;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawControls()
        {
            // Boyama toggle butonu
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
                    "Sol tık  → seçili karo yaz\n" +
                    "Sağ tık  → varsayılana sıfırla\n" +
                    "Sürükle → boyama fırçası",
                    MessageType.None);

            EditorGUILayout.Space(4);

            if (GUILayout.Button("🔄  Görüntüyü Yenile (Tüm Grid)", GUILayout.Height(28)))
            {
                RegenerateAll();
                EditorUtility.DisplayDialog("Tamam", "Grid görseli yenilendi.", "OK");
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            EditorGUILayout.LabelField(
                $"Toplam atama: {_tileMap.assignments.Count} / {_gridManager.Width * _gridManager.Height}",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Haritayı Sıfırla (Tümünü Varsayılan Yap)"))
            {
                if (EditorUtility.DisplayDialog("Haritayı Sıfırla",
                    "Tüm karo atamaları silinecek. Emin misin?", "Evet", "İptal"))
                {
                    _tileMap.assignments.Clear();
                    MarkDirtyAndSave();
                    RegenerateAll();
                }
            }
        }

        // ── Scene GUI (boyama) ────────────────────────────────────────────────

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_isPainting || _palette == null || _tileMap == null ||
                _gridManager == null || !_gridManager.HasCells) return;

            if (_palette.tiles.Count == 0 || _selectedIndex >= _palette.tiles.Count) return;

            // Mouse pozisyonundan hex bul
            Event e   = Event.current;
            Ray   ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            // Tile üst yüzeyinde (Y = TileHeight) kesişim
            var plane = new Plane(Vector3.up, new Vector3(0f, HexMetrics.TileHeight, 0f));
            _hasHovered = false;

            if (plane.Raycast(ray, out float dist))
            {
                Vector3 worldPt = ray.GetPoint(dist);
                _hoveredCoord = _gridManager.WorldToHex(worldPt);
                _hasHovered   = _gridManager.IsInBounds(_hoveredCoord);
            }

            // Vurgulama çiz
            if (_hasHovered)
            {
                var entry = _palette.tiles[_selectedIndex];
                DrawHexHighlight(_hoveredCoord, entry.editorColor);
                sceneView.Repaint();
            }

            // Sol tık / sürükle → boya
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && _hasHovered)
            {
                if (e.button == 0)
                {
                    var entry = _palette.tiles[_selectedIndex];
                    _tileMap.SetTileId(_hoveredCoord, entry.id);
                    MarkDirtyAndSave();
                    _gridManager.RegenerateCellVisual(_hoveredCoord);
                    e.Use();
                }
                else if (e.button == 1)
                {
                    _tileMap.RemoveAssignment(_hoveredCoord);
                    MarkDirtyAndSave();
                    _gridManager.RegenerateCellVisual(_hoveredCoord);
                    e.Use();
                }
            }

            // Scene view'daki diğer araçların devreye girmesini engelle
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        private void DrawHexHighlight(HexCoordinate coord, Color color)
        {
            if (!_gridManager.TryGetCell(coord, out HexCell cell)) return;

            float   yOffset = HexMetrics.TileHeight + 0.03f;
            Vector3 center  = cell.WorldPosition + Vector3.up * yOffset;
            float   scale   = 0.94f;

            var pts = new Vector3[7];
            for (int i = 0; i < 6; i++)
            {
                Vector3 c = HexMetrics.Corners[i] * scale;
                pts[i] = center + new Vector3(c.x, 0f, c.z);
            }
            pts[6] = pts[0]; // kapat

            Handles.color = new Color(color.r, color.g, color.b, 0.85f);
            Handles.DrawAAPolyLine(4f, pts);

            // Yarı saydam dolgu
            Color fill = new Color(color.r, color.g, color.b, 0.15f);
            Handles.color = fill;
            var fanVerts = new Vector3[3];
            for (int i = 0; i < 6; i++)
            {
                fanVerts[0] = center;
                fanVerts[1] = pts[i];
                fanVerts[2] = pts[(i + 1) % 6];
                Handles.DrawAAConvexPolygon(fanVerts);
            }
        }

        // ── Yardımcılar ───────────────────────────────────────────────────────

        private void EnsureGridCells()
        {
            if (_gridManager != null && !_gridManager.HasCells)
                _gridManager.GenerateGrid();
        }

        private void RegenerateAll()
        {
            if (_gridManager == null) return;
            _gridManager.GenerateGrid();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private void MarkDirtyAndSave()
        {
            EditorUtility.SetDirty(_tileMap);
            AssetDatabase.SaveAssets();
        }
    }
}
