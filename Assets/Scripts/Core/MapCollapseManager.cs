using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Kıyamet Sayacı — 1 GÜN ÖNCEDEN UYARILI çöküş:
    ///   • Bir gün başında, o günün SONUNDA çökecek karolar seçilir ve İŞARETLENİR:
    ///     karo kenarlarında KIRMIZI çizgi + üstünde "kalan AP" sayısı (karakter ilerledikçe azalır).
    ///   • Gün bitince (AP=0 → yeni gün) işaretli karolar BÖLGESEL depremsi bir sarsıntıyla silinir.
    /// Oyuncu üstündeki karo / kule korunur. Harita değişince (savaş/geçiş) işaretler temizlenir.
    /// </summary>
    public class MapCollapseManager : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager     _gridManager;
        [SerializeField] private ActionPointManager _apManager;
        [SerializeField] private PlayerController   _player;
        [SerializeField] private CollapseConfig     _config;
        [SerializeField] private Camera             _camera;
        [Tooltip("Karo çökünce üstündeki özü de siler. Boşsa Awake'te sahnede aranır.")]
        [SerializeField] private EssenceNodeManager _essenceNodes;

        [Header("Çöküş Görseli")]
        [SerializeField] private Material _collapsedMaterial;

        [Header("Uyarı (kırmızı çizgi + AP)")]
        [SerializeField] private Color  _outlineColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private float  _outlineWidth = 0.08f;
        [SerializeField] private float  _outlineLift  = 0.06f;
        [SerializeField] private Color  _labelColor   = new Color(1f, 0.35f, 0.25f);

        [Header("Deprem (bölgesel sarsıntı)")]
        [SerializeField] private float _shakeDuration  = 0.7f;
        [SerializeField] private float _shakeMagnitude = 0.12f;

        public int  TotalRemovedTiles { get; private set; }
        public bool IsCollapseActive  { get; private set; }
        public event Action<int, int> OnTileCollapsed;

        private int _lastProcessedDay = 0;

        // Bu gün sonunda çökecek karolar (koordinat) + kırmızı çizgileri.
        private readonly List<HexCoordinate> _doomed = new();
        private readonly Dictionary<HexCoordinate, LineRenderer> _outlines = new();
        private Transform _outlineRoot;
        private Material  _lineMat;
        private GUIStyle  _labelStyle;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            if (_essenceNodes == null) _essenceNodes = FindObjectOfType<EssenceNodeManager>();
            _outlineRoot = new GameObject("CollapseWarnings").transform;
            _outlineRoot.SetParent(transform, false);
        }

        private void OnEnable()
        {
            if (_apManager   != null) _apManager.OnTimeAdvanced += HandleTimeAdvanced;
            if (_gridManager != null) _gridManager.OnGridRegenerated += ClearWarnings; // harita değişti
        }

        private void OnDisable()
        {
            if (_apManager   != null) _apManager.OnTimeAdvanced -= HandleTimeAdvanced;
            if (_gridManager != null) _gridManager.OnGridRegenerated -= ClearWarnings;
        }

        private void HandleTimeAdvanced(int day, int slot, string slotName)
        {
            if (slot != 0) return;                 // yalnız gün sınırında
            if (day <= _lastProcessedDay) return;
            _lastProcessedDay = day;
            StartCoroutine(DayBoundaryRoutine(day));
        }

        private IEnumerator DayBoundaryRoutine(int day)
        {
            // 1) Dün işaretlenen karolar → gün bitti, ŞİMDİ çöker (bölgesel deprem).
            if (_doomed.Count > 0)
            {
                IsCollapseActive = true;
                var toCollapse = new List<HexCoordinate>(_doomed);
                _doomed.Clear();
                ClearOutlines();

                foreach (var coord in toCollapse)
                {
                    if (_gridManager.TryGetCell(coord, out HexCell cell) && cell.IsWalkable)
                        StartCoroutine(ShakeAndRemove(cell));
                    yield return new WaitForSeconds(0.12f);
                }
                yield return new WaitForSeconds(_shakeDuration);
                IsCollapseActive = false;
            }

            // 2) Bu GÜN sonunda çökecekleri seç + 1 gün önceden işaretle (kırmızı + AP).
            int count = _config != null ? _config.GetRemovalCount(day) : 0;
            if (count > 0) MarkDoomed(count);
        }

        private void MarkDoomed(int count)
        {
            HexCoordinate playerCoord = _player != null ? _player.CurrentCoord : default;
            var candidates = new List<HexCell>();
            foreach (HexCell cell in _gridManager.Cells.Values)
            {
                if (!cell.IsWalkable)                      continue;
                if (cell.Coordinate == playerCoord)        continue;
                if (cell.CellType == CellType.Watchtower)  continue;
                if (_doomed.Contains(cell.Coordinate))     continue;
                candidates.Add(cell);
            }

            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, candidates.Count);
                HexCell cell = candidates[idx];
                candidates.RemoveAt(idx);
                _doomed.Add(cell.Coordinate);
                CreateOutline(cell);
            }
        }

        // ── Kırmızı hex çizgisi ──────────────────────────────────────────────
        private void CreateOutline(HexCell cell)
        {
            if (_lineMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                _lineMat = new Material(sh);
                if (_lineMat.HasProperty("_BaseColor")) _lineMat.SetColor("_BaseColor", _outlineColor);
                if (_lineMat.HasProperty("_Color"))     _lineMat.SetColor("_Color",     _outlineColor);
            }

            var go = new GameObject($"Warn_{cell.Coordinate}");
            go.transform.SetParent(_outlineRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace  = true;
            lr.loop           = true;
            lr.positionCount  = 6;
            lr.widthMultiplier = _outlineWidth;
            lr.material       = _lineMat;
            lr.startColor = lr.endColor = _outlineColor;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector3 baseP = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + _outlineLift);
            for (int i = 0; i < 6; i++)
            {
                Vector3 c = HexMetrics.Corners[i] * 0.95f; // karo footprint'i (0.95)
                lr.SetPosition(i, baseP + new Vector3(c.x, 0f, c.z));
            }
            _outlines[cell.Coordinate] = lr;
        }

        private void ClearOutlines()
        {
            foreach (var kv in _outlines) if (kv.Value != null) Destroy(kv.Value.gameObject);
            _outlines.Clear();
        }

        // Harita yeniden üretilince (savaş/geçiş) uyarıları temizle (karo referansları değişti).
        private void ClearWarnings()
        {
            _doomed.Clear();
            ClearOutlines();
        }

        // ── Bölgesel deprem + silme ──────────────────────────────────────────
        private IEnumerator ShakeAndRemove(HexCell cell)
        {
            Transform vis = cell.Visual != null ? cell.Visual.transform : null;
            Vector3 basePos = vis != null ? vis.position : cell.WorldPosition;

            float t = 0f;
            while (t < _shakeDuration && vis != null)
            {
                t += Time.deltaTime;
                float damp = 1f - (t / _shakeDuration);              // sönümlenen sarsıntı
                Vector3 j = new Vector3(
                    (UnityEngine.Random.value - 0.5f),
                    (UnityEngine.Random.value - 0.5f) * 0.6f,
                    (UnityEngine.Random.value - 0.5f)) * (_shakeMagnitude * damp);
                vis.position = basePos + j;
                yield return null;
            }
            if (vis != null) vis.position = basePos;

            RemoveTile(cell);
            TotalRemovedTiles++;
            OnTileCollapsed?.Invoke(1, TotalRemovedTiles);
            Debug.Log($"[Collapse] Karo silindi: {cell.Coordinate} | Toplam: {TotalRemovedTiles}");
        }

        private void RemoveTile(HexCell cell)
        {
            cell.IsWalkable = false;
            cell.CellType   = CellType.Obstacle;

            if (_essenceNodes != null) _essenceNodes.RemoveNodeAt(cell.Coordinate); // öz de yok olsun

            if (cell.MeshRenderer != null && _collapsedMaterial != null)
                cell.MeshRenderer.sharedMaterial = _collapsedMaterial;
            else if (cell.Visual != null)
                cell.Visual.SetActive(false);
        }

        // ── Karo üstü "kalan AP" etiketi ─────────────────────────────────────
        private void OnGUI()
        {
            if (_doomed.Count == 0 || _camera == null || _apManager == null) return;

            if (_labelStyle == null)
                _labelStyle = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 14 };
            _labelStyle.normal.textColor = _labelColor;

            int ap = _apManager.APRemainingToday;
            foreach (var coord in _doomed)
            {
                if (!_gridManager.TryGetCell(coord, out HexCell cell)) continue;
                Vector3 world = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + 0.5f);
                Vector3 sp = _camera.WorldToScreenPoint(world);
                if (sp.z <= 0f) continue;                            // kamera arkası
                var rect = new Rect(sp.x - 24f, Screen.height - sp.y - 12f, 48f, 24f);
                GUI.Label(rect, ap.ToString(), _labelStyle);
            }
        }
    }
}
