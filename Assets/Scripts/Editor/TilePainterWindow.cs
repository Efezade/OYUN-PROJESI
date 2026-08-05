using System.Collections.Generic;
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
        private string  _tileFilter = "";   // palet araması (60+ karo arasında karo bulmak için)
        private Vector2 _windowScroll;   // tüm pencere kaydırması (yüz seçici içeriği aşağı itince kontroller erişilebilsin)

        // Klasörden karo ekleme: taranacak klasör (oturumlar arası EditorPrefs'te hatırlanır).
        private const string ScanFolderPrefKey = "TacticalRPG.TilePainter.ScanFolder";
        private DefaultAsset _scanFolder;

        // Karo id'si → ŞU ANKİ haritada kaç hücrede kullanılıyor. null = yeniden hesapla.
        //
        // NEDEN VAR: palette 58 karo var ama üretilen Bölüm 1 haritası bunların yalnız ~16'sını
        // kullanıyor (kalanı eski 3x3 dünyanın karoları: agac1-3, cicek, mantar, portal*, deneme*).
        // Sayaç olmadan "Ağaç 1'i Yürünmez yaptım ama ağaçların üstünden hâlâ geçiyorum" tuzağı
        // görünmüyordu — haritadaki ağaçlar aslında 'orman'/'nadir_yuksek_orman' karoları.
        private Dictionary<string, int> _usageCounts;

        // ── Arşiv karoları ────────────────────────────────────────────────────
        // Eski 3×3 dünyanın karoları. SİLİNMEDİ (alternatif tasarım hâlâ çalışır:
        // Docs/Alternatif_Tasarimlar/3x3_Dunya_Haritasi + "Arsiv" menüsü), ama yürürlükteki
        // tasarımda (1 bölüm = 1 harita, prosedürel terrain) hiçbiri kullanılmıyor →
        // varsayılan olarak GÖSTERİLMEZ (kullanıcı isteği 2026-08-04).
        private static readonly HashSet<string> ArchivedIds = new HashSet<string>
        {
            "default", "agac1", "agac2", "agac3", "cicek", "mantar", "su", "kum", "lav",
        };

        private const string ShowArchivedPrefKey = "TacticalRPG.TilePainter.ShowArchived";
        private bool _showArchived;

        /// <summary>Karo eski 3×3 dünyaya mı ait? (portal1-12 ve deneme1-20 önek ile yakalanır.)</summary>
        private static bool IsArchived(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return ArchivedIds.Contains(id) ||
                   id.StartsWith("portal", System.StringComparison.Ordinal) ||
                   id.StartsWith("deneme", System.StringComparison.Ordinal);
        }

        [MenuItem("TacticalRPG/Tile Painter - Karo Boyama", false, 20)]
        public static void OpenWindow()
        {
            var w = GetWindow<TilePainterWindow>("Tile Painter");
            w.minSize = new Vector2(400, 420);   // isim + kullanım sayacı + yürünürlük yan yana sığsın
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            AutoFindReferences();

            string saved = EditorPrefs.GetString(ScanFolderPrefKey, "Assets/Art/Models/Tiles");
            if (!string.IsNullOrEmpty(saved))
                _scanFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(saved);

            _showArchived = EditorPrefs.GetBool(ShowArchivedPrefKey, false);
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
            _usageCounts = null;
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
            DrawCurrentMapHeader();
            EditorGUILayout.Space(6);
            DrawPalette();
            EditorGUILayout.Space(6);
            DrawScanSection();
            EditorGUILayout.Space(6);
            DrawControls();
            EditorGUILayout.EndScrollView();
        }

        // ── Düzenlenen harita ─────────────────────────────────────────────────
        // ESKİ "9 harita / 3×3 snake" YÜZ SEÇİCİSİ KALDIRILDI (2026-08-04, kullanıcı isteği:
        // eski tasarım saklansın ama GÖRÜNMESİN). O seçici sadece göze batmıyordu, aktif bir
        // TEHLİKEYDİ: "Harita N" düğmesine basmak grid'in haritasını sessizce eski elle boyanmış
        // TileMap.asset / Face_N.asset ile DEĞİŞTİRİYORDU → üretilen bölüm haritası kayboluyordu.
        // Eski haritalar silinmedi: Assets/Data/Map/{TileMap,Face_2..9}.asset yerinde +
        // Docs/Alternatif_Tasarimlar/3x3_Dunya_Haritasi/ yedeği + "TacticalRPG/Arsiv" menüsü.
        private void DrawCurrentMapHeader()
        {
            string mapName = _tileMap != null ? _tileMap.name : "(yok)";
            EditorGUILayout.LabelField($"Düzenlenen harita: {mapName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Bölüm haritası PROSEDÜREL üretilir. Yeni harita için: " +
                "TacticalRPG → Bolum - Haritayi Simdi Uret.",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "Buradaki boyama üretilen haritanın ÜSTÜNE yazar; yeniden üretince silinir.",
                EditorStyles.miniLabel);
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
            EditorGUI.BeginChangeCheck();
            _gridManager = (HexGridManager)EditorGUILayout.ObjectField(
                "Grid Manager", _gridManager, typeof(HexGridManager), true);
            _palette = (TilePaletteSO)EditorGUILayout.ObjectField(
                "Tile Palette", _palette, typeof(TilePaletteSO), false);
            _tileMap = (TileMapSO)EditorGUILayout.ObjectField(
                "Tile Map", _tileMap, typeof(TileMapSO), false);
            if (EditorGUI.EndChangeCheck())
                _usageCounts = null;   // başka harita/grid seçildi → sayaçlar bayat
        }

        private void DrawPalette()
        {
            EditorGUILayout.LabelField("Karo Paleti", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Sağdaki düğme: üstünden GEÇİLİR (yeşil) / GEÇİLMEZ (kırmızı). Tıkla → değiştir.",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Düğmenin solundaki sayı: karo bu haritada kaç hücrede var. " +
                "\"bu haritada YOK\" ise yürünürlüğü değiştirmek burada bir şey yapmaz.",
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

            // Palet 60+ karoya çıktı (prosedürel terrain + düğüm karoları) → arama + daha uzun liste,
            // yoksa aranan karoyu bulmak için sürekli kaydırmak gerekiyor.
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Ara", GUILayout.Width(28f));
                _tileFilter = EditorGUILayout.TextField(_tileFilter);
                if (GUILayout.Button("Temizle", GUILayout.Width(64f))) { _tileFilter = ""; GUI.FocusControl(null); }
            }

            // Eski 3×3 dünyanın karoları varsayılan olarak GİZLİ (silinmedi — bkz ArchivedIds).
            int archivedCount = 0;
            for (int i = 0; i < _palette.tiles.Count; i++)
                if (IsArchived(_palette.tiles[i].id)) archivedCount++;

            if (archivedCount > 0)
            {
                EditorGUI.BeginChangeCheck();
                _showArchived = EditorGUILayout.ToggleLeft(
                    $"Eski (arşiv) karoları da göster — {archivedCount} adet", _showArchived);
                if (EditorGUI.EndChangeCheck())
                    EditorPrefs.SetBool(ShowArchivedPrefKey, _showArchived);
            }

            // Seçili karo gizlendiyse görünür ilk karoya kay — yoksa görünmeyen bir karoyla boyanır.
            if (!_showArchived && _selectedIndex < _palette.tiles.Count &&
                IsArchived(_palette.tiles[_selectedIndex].id))
            {
                _selectedIndex = _palette.tiles.FindIndex(t => !IsArchived(t.id));
                if (_selectedIndex < 0) _selectedIndex = 0;
            }

            int shown = 0;
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(320));

            for (int i = 0; i < _palette.tiles.Count; i++)
            {
                var  entry      = _palette.tiles[i];
                bool isSelected = i == _selectedIndex;

                if (!_showArchived && IsArchived(entry.id)) continue;

                if (!string.IsNullOrEmpty(_tileFilter) &&
                    entry.id.IndexOf(_tileFilter, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    (entry.displayName == null ||
                     entry.displayName.IndexOf(_tileFilter, System.StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                shown++;

                Rect r = EditorGUILayout.GetControlRect(false, 30);

                // Sol = seçim (renk+isim); orta = bu haritadaki kullanım; sağ = yürünürlük anahtarı
                // (ayrı buton → tık çakışmaz).
                const float toggleW = 96f;
                const float usageW  = 104f;
                Rect selectRect = new Rect(r.x, r.y,
                    Mathf.Max(40f, r.width - toggleW - usageW - 8f), r.height);
                Rect usageRect  = new Rect(r.xMax - toggleW - usageW - 4f, r.y + 6f, usageW, 18f);
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

                // Bu karo ŞU ANKİ haritada kaç hücrede var? 0 ise yürünürlüğü değiştirmek bu
                // haritada HİÇBİR ŞEY yapmaz — kullanıcı yanlış karoyu ayarlamasın diye uyarıyoruz.
                int  usage      = UsageOf(entry.id);
                var  usageStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
                usageStyle.normal.textColor = usage > 0
                    ? new Color(0.55f, 0.55f, 0.55f)
                    : new Color(0.95f, 0.62f, 0.20f);
                GUI.Label(usageRect, usage > 0 ? $"haritada {usage}" : "bu haritada YOK", usageStyle);

                // Yürünürlük anahtarı — tıkla → değiş + paleti dirty + grid hemen yenilen.
                GUI.backgroundColor = entry.isWalkable ? new Color(0.40f, 0.82f, 0.45f)
                                                       : new Color(0.90f, 0.42f, 0.36f);
                if (GUI.Button(toggleRect, entry.isWalkable ? "Yürünür ✓" : "Yürünmez ✗"))
                {
                    entry.isWalkable = !entry.isWalkable;
                    EditorUtility.SetDirty(_palette);
                    AssetDatabase.SaveAssets();   // DİSKE yaz — yalnız SetDirty ayarı kaydetmeden
                                                  // çıkınca/reimport'ta kaybediyordu.
                    RegenerateAll(); // yürünürlük hemen etki etsin (grid yeniden üretilir)
                    _usageCounts = null;          // sayaçlar tazelensin
                }
                GUI.backgroundColor = prevBG;
            }

            EditorGUILayout.EndScrollView();

            int visibleTotal = _showArchived ? _palette.tiles.Count
                                             : _palette.tiles.Count - archivedCount;
            EditorGUILayout.LabelField(
                string.IsNullOrEmpty(_tileFilter)
                    ? $"Toplam {visibleTotal} karo" +
                      (_showArchived || archivedCount == 0 ? "" : $"  (+{archivedCount} arşiv gizli)")
                    : $"{shown} / {visibleTotal} karo (filtre: \"{_tileFilter}\")",
                EditorStyles.miniLabel);
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

        // ── Karo kullanım sayacı ──────────────────────────────────────────────

        /// <summary>Karo id'sinin şu anki haritadaki hücre sayısı (gerekirse tabloyu kurar).</summary>
        private int UsageOf(string id)
        {
            if (_usageCounts == null) BuildUsageCounts();
            return !string.IsNullOrEmpty(id) && _usageCounts.TryGetValue(id, out int n) ? n : 0;
        }

        /// <summary>Haritadaki karo dağılımını sayar. Atanmamış hücreler
        /// <see cref="TileMapSO.defaultTileId"/>'ye yazılır — HexGridManager.ResolveEntry ile aynı kural.</summary>
        private void BuildUsageCounts()
        {
            _usageCounts = new Dictionary<string, int>();
            if (_tileMap == null) return;

            int assigned = 0;
            foreach (var a in _tileMap.assignments)
            {
                if (string.IsNullOrEmpty(a.tileId)) continue;
                _usageCounts.TryGetValue(a.tileId, out int n);
                _usageCounts[a.tileId] = n + 1;
                assigned++;
            }

            int total = _gridManager != null ? _gridManager.Width * _gridManager.Height : 0;
            int rest  = total - assigned;
            if (rest > 0 && !string.IsNullOrEmpty(_tileMap.defaultTileId))
            {
                _usageCounts.TryGetValue(_tileMap.defaultTileId, out int n);
                _usageCounts[_tileMap.defaultTileId] = n + rest;
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
            _usageCounts = null;   // harita değişti → kullanım sayaçları bayat
        }
    }
}
