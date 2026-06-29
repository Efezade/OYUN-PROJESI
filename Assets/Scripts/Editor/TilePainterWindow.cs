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
        private int     _currentFace = 1;   // Küp = 6 yüz; şu an düzenlenen yüz (1=Ön … 6=Alt)
        private bool    _isPainting    = false;
        private bool    _hasHovered    = false;
        private HexCoordinate _hoveredCoord;

        private Vector2 _scroll;
        private Vector2 _windowScroll;   // tüm pencere kaydırması (yüz seçici içeriği aşağı itince kontroller erişilebilsin)

        // Klasörden karo ekleme: taranacak klasör (oturumlar arası EditorPrefs'te hatırlanır).
        private const string ScanFolderPrefKey = "TacticalRPG.TilePainter.ScanFolder";
        private DefaultAsset _scanFolder;

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

            string saved = EditorPrefs.GetString(ScanFolderPrefKey, "Assets/Art/Models/Tiles");
            if (!string.IsNullOrEmpty(saved))
                _scanFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(saved);
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
                DetectCurrentFace();
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

            _windowScroll = EditorGUILayout.BeginScrollView(_windowScroll);
            EditorGUILayout.Space(6);
            DrawFaceSelector();
            EditorGUILayout.Space(6);
            DrawPalette();
            EditorGUILayout.Space(6);
            DrawScanSection();
            EditorGUILayout.Space(6);
            DrawControls();
            EditorGUILayout.EndScrollView();
        }

        // ── Küp yüzü seçici (Küp = 6 yüz, açılım/cross düzeni) ───────────────
        // Her yüz KENDİ TileMapSO asset'i: yüz 1 (Ön) = TileMap.asset; 2-6 = Face_N.asset (ilk
        // seçimde oluşur). Yüz seçince grid o yüzün haritasıyla yenilenir → tasarlarsın, Ctrl+S
        // kalıcı kaydolur. (Sonra: oyun-içi yüz çubuğu + kenar geçişleri + küp dönüşü aynı asset'leri kullanır.)
        private static readonly string[] FaceNames = { "", "Ön", "Sağ", "Arka", "Sol", "Üst", "Alt" }; // 1-6

        private void DrawFaceSelector()
        {
            EditorGUILayout.LabelField($"Harita (3×3 snake) — şu an: HARİTA {_currentFace}",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField("9 harita. Haritayı seç → tasarla → Ctrl+S (her harita ayrı + kalıcı).",
                EditorStyles.miniLabel);

            const float w = 72f, h = 34f;
            int[,] layout = { { 9, 8, 7 }, { 6, 5, 4 }, { 3, 2, 1 } };  // snake dizilim
            for (int r = 0; r < 3; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int col = 0; col < 3; col++) FaceButton(layout[r, col], w, h);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void FaceButton(int face, float w, float h)
        {
            Color prev = GUI.backgroundColor;
            if (face == _currentFace) GUI.backgroundColor = new Color(0.40f, 0.80f, 1f);
            if (GUILayout.Button($"Harita {face}", GUILayout.Width(w), GUILayout.Height(h)))
                SelectFace(face);
            GUI.backgroundColor = prev;
        }

        private void SelectFace(int n)
        {
            if (n < 1 || n > 9) return;
            TileMapSO map = LoadOrCreateFace(n);
            if (map == null) return;
            _currentFace = n;
            _tileMap     = map;
            if (_gridManager != null)
            {
                _gridManager.SetTileMap(map); // _tileMap'i değiştirir + grid'i yeniden üretir
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        private void DetectCurrentFace()
        {
            string path = _tileMap != null ? AssetDatabase.GetAssetPath(_tileMap) : "";
            for (int n = 1; n <= 9; n++)
                if (path == FaceAssetPath(n)) { _currentFace = n; return; }
            _currentFace = 1;
        }

        private static string FaceAssetPath(int n) =>
            n == 1 ? "Assets/Data/Map/TileMap.asset" : $"Assets/Data/Map/Face_{n}.asset";

        private static TileMapSO LoadOrCreateFace(int n)
        {
            string path = FaceAssetPath(n);
            var map = AssetDatabase.LoadAssetAtPath<TileMapSO>(path);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<TileMapSO>();
                AssetDatabase.CreateAsset(map, path);
                AssetDatabase.SaveAssets();
            }
            return map;
        }

        // ── Klasörden karo ekleme ─────────────────────────────────────────────

        private void DrawScanSection()
        {
            EditorGUILayout.LabelField("Klasörden Karo Ekle", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _scanFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Karo Klasörü", _scanFolder, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString(ScanFolderPrefKey,
                    _scanFolder != null ? AssetDatabase.GetAssetPath(_scanFolder) : "");

            using (new EditorGUI.DisabledScope(_scanFolder == null))
            {
                if (GUILayout.Button("🔍  Klasörü Tara → Palete Ekle", GUILayout.Height(26)))
                    ScanFolder();
            }

            EditorGUILayout.HelpBox(
                "Klasördeki FBX/prefab karolar otomatik palete eklenir (FBX hex boyutuna ölçeklenir, " +
                "pivotu ayarlanır, collider eklenir). Bozuk/dev model varsa eklenmez, uyarı verilir.",
                MessageType.None);
        }

        private void ScanFolder()
        {
            string folder = AssetDatabase.GetAssetPath(_scanFolder);
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                EditorUtility.DisplayDialog("Hata", "Geçerli bir proje klasörü seç.", "Tamam");
                return;
            }

            int n = TileFolderImporter.ImportFolder(folder, _palette, out string report);

            EditorUtility.SetDirty(_palette);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RegenerateAll();

            Debug.Log($"[TilePainter] Klasör tarandı: {folder}\nEklenen/güncellenen: {n}\n{report}");
            EditorUtility.DisplayDialog("Klasör Tarandı",
                $"Palete eklenen/güncellenen karo: {n}\n\n" +
                "Atlanan veya uyarı varsa Console'a bak.\n\n" +
                "Paletten seçip Scene'de boyayabilirsin.", "Tamam");

            if (_selectedIndex >= _palette.tiles.Count) _selectedIndex = 0;
            Repaint();
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
            EditorGUILayout.LabelField("Sağdaki düğme: üstünden GEÇİLİR (yeşil) / GEÇİLMEZ (kırmızı). Tıkla → değiştir.",
                EditorStyles.miniLabel);

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

                Rect r = EditorGUILayout.GetControlRect(false, 30);

                // Sol = seçim (renk+isim); sağ = yürünürlük anahtarı (ayrı buton → tık çakışmaz).
                const float toggleW = 96f;
                Rect selectRect = new Rect(r.x, r.y, r.width - toggleW - 4f, r.height);
                Rect toggleRect = new Rect(r.xMax - toggleW, r.y + 4f, toggleW, r.height - 8f);

                Color prevBG = GUI.backgroundColor;
                GUI.backgroundColor = isSelected ? Color.white : entry.editorColor * 0.6f;
                if (GUI.Button(selectRect, GUIContent.none,
                        isSelected ? EditorStyles.helpBox : EditorStyles.miniButton))
                    _selectedIndex = i;
                GUI.backgroundColor = prevBG;

                EditorGUI.DrawRect(new Rect(r.x + 4, r.y + 5, 20, 20), entry.editorColor);
                var labelStyle = new GUIStyle(EditorStyles.label);
                if (isSelected) labelStyle.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(r.x + 30, r.y + 6, selectRect.width - 34f, 20),
                    $"{entry.displayName}  [{entry.id}]", labelStyle);

                // Yürünürlük anahtarı — tıkla → değiş + paleti dirty + grid hemen yenilen.
                GUI.backgroundColor = entry.isWalkable ? new Color(0.40f, 0.82f, 0.45f)
                                                       : new Color(0.90f, 0.42f, 0.36f);
                if (GUI.Button(toggleRect, entry.isWalkable ? "Yürünür ✓" : "Yürünmez ✗"))
                {
                    entry.isWalkable = !entry.isWalkable;
                    EditorUtility.SetDirty(_palette);
                    RegenerateAll(); // yürünürlük hemen etki etsin (grid yeniden üretilir)
                }
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
