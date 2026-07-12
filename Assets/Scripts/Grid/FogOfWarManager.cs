using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// Hex karolarının görünürlük durumunu (sis) yönetir.
    /// Karonun KENDİ görselini (placeholder tint / boyanmış-dokulu prefab) KORUR:
    /// materyali değiştirmek yerine MaterialPropertyBlock ile temel rengini bir
    /// parlaklık çarpanıyla karartır.
    ///   Visible  = tam parlak (görüş içi),
    ///   Explored = hafif kararmış (daha önce görülmüş, hatırlanan),
    ///   Hidden   = neredeyse siyah (hiç görülmemiş).
    /// Grid yeniden üretilince (harita geçişi) HexGridManager.OnGridRegenerated ile
    /// sis otomatik yeniden kurulur.
    /// </summary>
    [DefaultExecutionOrder(-50)] // HexGridManager'dan sonra, PlayerController'dan önce
    public class FogOfWarManager : MonoBehaviour
    {
        [Header("Bağımlılık")]
        [SerializeField] private HexGridManager _gridManager;

        [Header("Sis Parlaklığı (temel rengin çarpanı)")]
        [Tooltip("Görüş içindeki karo — tam parlaklık.")]
        [SerializeField, Range(0f, 1f)] private float _visibleBrightness  = 1f;
        [Tooltip("Daha önce görülmüş ama şu an görüş dışı — kararmış 'hatırlama'.")]
        [SerializeField, Range(0f, 1f)] private float _exploredBrightness = 0.4f;
        [Tooltip("Hiç görülmemiş — neredeyse siyah (sis).")]
        [SerializeField, Range(0f, 1f)] private float _hiddenBrightness   = 0.05f;

        [Header("Sis Görseli (asset — istediğin modelle değiştir)")]
        [Tooltip("Görülmemiş (Hidden) karonun ÜSTÜNE konan sis kapağı prefabı. Kendi bulut/sis " +
                 "modelinle değiştirebilirsin; boşsa sadece karartma uygulanır.")]
        [SerializeField] private GameObject _fogTilePrefab;
        [Tooltip("Sis kapağının karo yüzeyinin ne kadar ÜSTÜNE konacağı (z-fighting'i önler).")]
        [SerializeField] private float _fogLift = 0.03f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP/Lit
        private static readonly int ColorId     = Shader.PropertyToID("_Color");     // Standard yedek
        private MaterialPropertyBlock _block;

        // Sis kapağı havuzu — karo başına tek örnek, yeniden kullanılır (Instantiate/Destroy değil).
        private Transform _fogRoot;
        private readonly Dictionary<HexCoordinate, GameObject> _caps = new();

        // HexGridManager(-100) Awake'inde grid üretilmiş olur; burada (-50) dinlemeye başlarız.
        private void Awake()
        {
            if (_gridManager == null)
            {
                Debug.LogError("[FogOfWarManager] _gridManager NULL! Inspector'da bağlantı eksik. TAM KURULUM'u yeniden çalıştır.");
                return;
            }
            _fogRoot = new GameObject("FogTiles").transform;
            _fogRoot.SetParent(transform, false);

            _gridManager.OnGridRegenerated += InitializeFog; // harita değişince sisi yeniden kur
            InitializeFog();
        }

        private void OnDestroy()
        {
            if (_gridManager != null) _gridManager.OnGridRegenerated -= InitializeFog;
        }

        private void InitializeFog()
        {
            if (_gridManager.Cells == null) return;
            foreach (var cell in _gridManager.Cells.Values)
            {
                cell.FogState = FogState.Hidden;
                ApplyFogVisual(cell);
            }
        }

        /// <summary>Tüm karoları yeniden Hidden yapar (grid yeniden üretilince otomatik çağrılır).</summary>
        public void ResetFog() => InitializeFog();

        /// <summary>Tüm karoları Visible yapar (savaş haritasında tam görüş).</summary>
        public void RevealAll()
        {
            if (_gridManager.Cells == null) return;
            foreach (var cell in _gridManager.Cells.Values)
            {
                cell.FogState = FogState.Visible;
                ApplyFogVisual(cell);
            }
        }

        // ── Genel API ────────────────────────────────────────────────────

        /// <summary>
        /// Belirtilen merkez etrafında visionRange adımlık alanı Visible yapar.
        /// Önceden Visible olan karolar Explored'a (kalıcı keşif hafızası) düşer.
        /// </summary>
        public void RevealArea(HexCoordinate origin, int visionRange)
        {
            if (_gridManager.Cells == null) return;

            foreach (var cell in _gridManager.Cells.Values)
            {
                if (cell.FogState == FogState.Visible)
                {
                    cell.FogState = FogState.Explored;
                    ApplyFogVisual(cell);
                }
            }

            foreach (var coord in GetCoordsInRange(origin, visionRange))
            {
                if (_gridManager.TryGetCell(coord, out HexCell cell))
                {
                    cell.FogState = FogState.Visible;
                    ApplyFogVisual(cell);
                }
            }
        }

        public FogState GetFogState(HexCoordinate coord) =>
            _gridManager.TryGetCell(coord, out HexCell c) ? c.FogState : FogState.Hidden;

        public bool IsVisible(HexCoordinate coord) =>
            GetFogState(coord) == FogState.Visible;

        public bool IsKnown(HexCoordinate coord) =>
            GetFogState(coord) != FogState.Hidden;

        // ── Yardımcı metodlar ─────────────────────────────────────────────

        private List<HexCoordinate> GetCoordsInRange(HexCoordinate origin, int range)
        {
            var result = new List<HexCoordinate>();
            for (int q = -range; q <= range; q++)
            {
                int rMin = Mathf.Max(-range, -q - range);
                int rMax = Mathf.Min(range, -q + range);
                for (int r = rMin; r <= rMax; r++)
                {
                    var coord = new HexCoordinate(origin.Q + q, origin.R + r);
                    if (_gridManager.IsInBounds(coord))
                        result.Add(coord);
                }
            }
            return result;
        }

        // İki katman: (1) karonun temel rengini parlaklıkla çarparak karartır (MPB — materyali
        // değiştirmez, boyanmış karo korunur), (2) Hidden karonun ÜSTÜNE sis kapağı (asset) koyar.
        private void ApplyFogVisual(HexCell cell)
        {
            // (1) Parlaklık — explored=kararmış hatırlama, visible=tam, hidden=neredeyse siyah.
            if (cell.MeshRenderer != null)
            {
                float b = cell.FogState switch
                {
                    FogState.Visible  => _visibleBrightness,
                    FogState.Explored => _exploredBrightness,
                    _                 => _hiddenBrightness,
                };

                Color c = cell.BaseColor * b;
                c.a = 1f;

                _block ??= new MaterialPropertyBlock();
                cell.MeshRenderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, c);
                _block.SetColor(ColorId, c);
                cell.MeshRenderer.SetPropertyBlock(_block);
            }

            // (2) Sis kapağı — sadece hiç görülmemiş (Hidden) karoda; terreni fiziksel olarak örter.
            UpdateFogCap(cell);
        }

        // Karo başına sis kapağını (asset) havuzdan alıp konumlandırır ve durumuna göre aç/kapatır.
        private void UpdateFogCap(HexCell cell)
        {
            if (_fogTilePrefab == null || _fogRoot == null) return; // asset yoksa yalnız karartma kalır

            bool covered = cell.FogState == FogState.Hidden;

            if (!_caps.TryGetValue(cell.Coordinate, out GameObject cap) || cap == null)
            {
                if (!covered) return;                         // görünür karoya kapak üretme
                cap = Instantiate(_fogTilePrefab, _fogRoot);  // havuza ekle (bir kez)
                cap.name = $"Fog_{cell.Coordinate}";
                _caps[cell.Coordinate] = cap;
            }

            // Karonun yürüme yüzeyinin biraz üstüne otur (engebe/köprü yüksekliğini takip eder).
            cap.transform.position = cell.WorldPosition + Vector3.up * (cell.SurfaceHeight + _fogLift);
            if (cap.activeSelf != covered) cap.SetActive(covered);
        }
    }
}
