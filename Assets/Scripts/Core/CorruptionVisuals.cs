using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// KARA AŞI ÇÜRÜMESİNİN GÖRÜNTÜSÜ (2026-10-10). Durum <see cref="CorruptionManager"/>'da;
    /// burası yalnız çizer. Üç katman:
    ///   1. MOR DAMAR KATMANI — çürük karonun üstüne serilen yarı saydam altıgen (damar + obsidyen
    ///      leke dokusu, kademeyle koyulaşır). ASIL GÖRÜNEN BU: Efe'nin ilk testinde morarma hiç
    ///      görünmedi, çünkü yalnız renk ÇARPMASI vardı ve yeşil doku × mor = koyu gri.
    ///   2. Karonun hafif kararması (<see cref="HexCell.OverlayTint"/>; minimap da bunu okur).
    ///   3. Sisli karonun bulutu mor pusa kayar — çürüme keşfedilmemiş yerde de uzaktan görünür.
    /// Katman yalnız KEŞFEDİLMİŞ karoda çizilir (sisin altındaki bilgi sızmasın, onu bulut söyler).
    /// Savaş arenasında hiçbir şey çizilmez.
    /// </summary>
    public class CorruptionVisuals : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private CorruptionManager _corruption;
        [SerializeField] private HexGridManager    _grid;
        [SerializeField] private FogOfWarManager   _fog;
        [SerializeField] private GameStateManager  _state;
        [SerializeField] private PlayerController  _player;
        [Tooltip("Çöken karonun üstünde katman havada kalmasın diye.")]
        [SerializeField] private MapCollapseManager _collapse;

        [Header("Mor damar katmanı")]
        [Tooltip("Boşsa çalışma anında saydam Unlit materyal kurulur.")]
        [SerializeField] private Material  _overlayMaterial;
        [Tooltip("Boşsa damar + obsidyen leke dokusu prosedürel üretilir.")]
        [SerializeField] private Texture2D _overlayTexture;
        [Tooltip("Katmanın karo yüzeyinin ne kadar üstünde durduğu.")]
        [SerializeField] private float     _lift = 0.05f;
        [Tooltip("Altıgen katmanın karoya oranı (1 = köşe köşe).")]
        [SerializeField, Range(0.5f, 1f)] private float _size = 0.97f;

        private Transform _root;
        private Mesh      _hexMesh;
        private Material  _mat;
        private MaterialPropertyBlock _mpb;
        private readonly Dictionary<HexCoordinate, MeshRenderer> _overlays = new();
        private readonly Stack<MeshRenderer> _pool = new();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        private bool InOverworld => _state == null || _state.State == GameState.Overworld;
        private CorruptionConfigSO Config => _corruption != null ? _corruption.Config : null;

        private void Awake()
        {
            _root = new GameObject("CorruptionOverlays").transform;
            _root.SetParent(transform, false);
        }

        private void OnEnable()
        {
            if (_corruption != null)
            {
                _corruption.OnCorruptionChanged += RedrawAll;
                _corruption.OnCellsChanged      += HandleCellsChanged;
            }
            if (_grid   != null) _grid.OnGridRegenerated += HandleGridRegenerated;
            if (_state  != null) _state.OnStateChanged   += HandleStateChanged;
            if (_player != null) _player.OnMoved         += HandlePlayerMoved;
            if (_collapse != null) _collapse.OnTileCollapsed += HandleTileCollapsed;
        }

        private void OnDisable()
        {
            if (_corruption != null)
            {
                _corruption.OnCorruptionChanged -= RedrawAll;
                _corruption.OnCellsChanged      -= HandleCellsChanged;
            }
            if (_grid   != null) _grid.OnGridRegenerated -= HandleGridRegenerated;
            if (_state  != null) _state.OnStateChanged   -= HandleStateChanged;
            if (_player != null) _player.OnMoved         -= HandlePlayerMoved;
            if (_collapse != null) _collapse.OnTileCollapsed -= HandleTileCollapsed;
        }

        private void HandleGridRegenerated()
        {
            if (InOverworld) RedrawAll();
        }

        private void HandleStateChanged(GameState s)
        {
            // Arenaya geçerken katmanlar gizlenir (koordinatlar arenada başka karoya denk gelir).
            if (_root != null) _root.gameObject.SetActive(s == GameState.Overworld);
            if (s == GameState.Overworld) RedrawAll();
        }

        private void HandleCellsChanged(IReadOnlyCollection<HexCoordinate> cells)
        {
            if (!Application.isPlaying) return;
            if (!InOverworld) return;      // savaştayken: dönüşte RedrawAll hepsini çizer
            Apply(cells);
        }

        // Keşif ilerledikçe yeni açılan çürük karoların katmanı görünür olsun.
        private void HandlePlayerMoved(HexCoordinate _) => RefreshVisibility();
        private void HandleTileCollapsed(int _, int __) => RefreshVisibility();

        /// <summary>Katman yalnız keşfedilmiş ve hâlâ AYAKTA olan karoda görünür.</summary>
        private void RefreshVisibility()
        {
            if (!InOverworld || _grid == null) return;
            foreach (var kv in _overlays)
            {
                if (kv.Value == null) continue;
                bool standing = _grid.TryGetCell(kv.Key, out HexCell cell) && cell.CellType != CellType.Obstacle;
                kv.Value.enabled = standing && (_fog == null || _fog.IsKnown(kv.Key));
            }
        }

        // ── Çizim ────────────────────────────────────────────────────────────

        private void RedrawAll()
        {
            if (!Application.isPlaying) return;      // editör önizlemesinde sahneye katman yazılmaz
            foreach (var kv in _overlays) Release(kv.Value);
            _overlays.Clear();
            if (_fog != null) _fog.ClearCloudTints();
            if (_corruption == null || !InOverworld) return;
            Apply(new List<HexCoordinate>(_corruption.CorruptedCells));
        }

        private void Apply(IEnumerable<HexCoordinate> coords)
        {
            CorruptionConfigSO cfg = Config;
            if (cfg == null || _grid == null) return;

            bool any = false;
            foreach (var c in coords)
            {
                int lvl = _corruption.LevelAt(c);
                if (_grid.TryGetCell(c, out HexCell cell))
                {
                    cell.OverlayTint = cfg.TintFor(lvl);
                    if (_fog != null) _fog.ReapplyCellBrightness(cell);
                    SetOverlay(c, cell, lvl, cfg);
                    any = true;
                }
                if (_fog != null) _fog.SetCloudTint(c, cfg.CloudTint, cfg.CloudShare(lvl));
            }
            // Sis parlaklığını son oyuncu konumuna göre yeniden hesapla (ipucu bandı doğru kalsın).
            if (any && _fog != null) _fog.RefreshFromLastPosition();
        }

        private void SetOverlay(HexCoordinate c, HexCell cell, int lvl, CorruptionConfigSO cfg)
        {
            float alpha = cfg.OverlayAlpha(lvl);
            bool has = _overlays.TryGetValue(c, out MeshRenderer r) && r != null;

            if (alpha <= 0.001f)
            {
                if (has) { Release(r); _overlays.Remove(c); }
                return;
            }
            if (!has)
            {
                r = Acquire();
                _overlays[c] = r;
                Transform t = r.transform;
                t.position = cell.WorldPosition + Vector3.up * (GroundHeight(cell) + _lift);
                // Her karoda başka yöne dönük → damar deseni tekrar etmez.
                t.rotation = Quaternion.Euler(0f, 60f * (((c.VariantHash() >> 5) & 0x7fff) % 6), 0f);
            }

            _mpb ??= new MaterialPropertyBlock();
            Color col = cfg.OverlayColor;
            col.a = alpha;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, col);
            _mpb.SetColor(ColorId, col);
            r.SetPropertyBlock(_mpb);
            r.enabled = cell.CellType != CellType.Obstacle && (_fog == null || _fog.IsKnown(c));
        }

        /// <summary>Katmanın oturacağı zemin. Karonun ORTASINDA ağaç/kütük varsa SurfaceHeight onun
        /// tepesini ölçer ve katman havada asılı kalırdı → o durumda karonun gövde kalınlığı kullanılır.</summary>
        private static float GroundHeight(HexCell cell)
        {
            float slab = HexMetrics.TileHeight;
            return cell.SurfaceHeight > slab + 0.4f ? slab + 0.04f : cell.SurfaceHeight;
        }

        // ── Havuz ────────────────────────────────────────────────────────────

        private MeshRenderer Acquire()
        {
            if (_pool.Count > 0)
            {
                MeshRenderer pooled = _pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            var go = new GameObject("Curuk");
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = HexMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial    = OverlayMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows    = false;
            return mr;
        }

        private void Release(MeshRenderer r)
        {
            if (r == null) return;
            r.gameObject.SetActive(false);
            _pool.Push(r);
        }

        // ── Kaynaklar (atanmadıysa çalışma anında) ──────────────────────────

        private Mesh HexMesh()
        {
            if (_hexMesh != null) return _hexMesh;
            float rad = HexMetrics.OuterRadius * (_grid != null ? _grid.HexSize : 1f) * _size;
            var v  = new Vector3[7];
            var uv = new Vector2[7];
            v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < 6; i++)
            {
                Vector3 c = HexMetrics.Corners[i].normalized * rad;
                v[i + 1]  = new Vector3(c.x, 0f, c.z);
                uv[i + 1] = new Vector2(0.5f + c.x / (2f * rad), 0.5f + c.z / (2f * rad));
            }
            var tris = new int[18];
            for (int i = 0; i < 6; i++)
            {
                tris[i * 3]     = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % 6 + 1;
            }
            _hexMesh = new Mesh { name = "CorruptionHex", vertices = v, uv = uv, triangles = tris };
            _hexMesh.RecalculateNormals();
            // Sarım yönü kameraya bakmıyorsa (normal aşağı) üçgenleri çevir.
            if (_hexMesh.normals[0].y < 0f)
            {
                for (int i = 0; i < 6; i++) (tris[i * 3 + 1], tris[i * 3 + 2]) = (tris[i * 3 + 2], tris[i * 3 + 1]);
                _hexMesh.triangles = tris;
                _hexMesh.RecalculateNormals();
            }
            return _hexMesh;
        }

        private Material OverlayMaterial()
        {
            if (_mat != null) return _mat;
            if (_overlayMaterial != null) { _mat = _overlayMaterial; return _mat; }

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
            _mat = new Material(sh) { name = "CorruptionOverlay (runtime)" };
            // URP'de saydamlık yalnız bu bayrak seti ile açılır (AugmentFeedback ile aynı reçete).
            if (_mat.HasProperty("_Surface"))
            {
                _mat.SetFloat("_Surface",  1f);
                _mat.SetFloat("_Blend",    0f);
                _mat.SetFloat("_ZWrite",   0f);
                _mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            _mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            Texture2D tex = _overlayTexture != null ? _overlayTexture : BuildVeinTexture(256, 7177);
            if (_mat.HasProperty("_BaseMap")) _mat.SetTexture("_BaseMap", tex);
            _mat.mainTexture = tex;
            return _mat;
        }

        /// <summary>
        /// Damar dokusu: hafif dalgalı mor dolgu + merkezden kenara uzanan dallı ince damarlar
        /// (parlak) + birkaç obsidyen leke (koyu). Hikâyedeki Kara Öz dili: "ince damarlar, birkaç
        /// obsidyen leke". RGB parlaklık çarpanıdır — asıl rengi ayar dosyası verir.
        /// </summary>
        private static Texture2D BuildVeinTexture(int size, int seed)
        {
            var rnd  = new System.Random(seed);
            var lum  = new float[size * size];
            var alp  = new float[size * size];
            float half = size * 0.5f;

            // 1) Dolgu: kenara doğru sönen, lekeli düşük opaklık.
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half, dy = (y - half) / half;
                    float r  = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = 1f - Mathf.SmoothStep(0.62f, 0.92f, r);
                    float mottle = Mathf.PerlinNoise(x * 0.045f + 3.1f, y * 0.045f + 7.7f);
                    int i = y * size + x;
                    lum[i] = 0.55f;
                    alp[i] = edge * Mathf.Lerp(0.38f, 0.62f, mottle);
                }

            // 2) Damarlar: merkez yakınından dışa, hafif kıvrılarak, dallanan çizgiler.
            void Dab(float cx, float cy, float rad, float l, float a)
            {
                int x0 = Mathf.Max(0, (int)(cx - rad - 1)), x1 = Mathf.Min(size - 1, (int)(cx + rad + 1));
                int y0 = Mathf.Max(0, (int)(cy - rad - 1)), y1 = Mathf.Min(size - 1, (int)(cy + rad + 1));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (d > rad) continue;
                        float k = 1f - d / rad;
                        float dx = (x - half) / half, dy = (y - half) / half;
                        float edge = 1f - Mathf.SmoothStep(0.70f, 0.95f, Mathf.Sqrt(dx * dx + dy * dy));
                        int i = y * size + x;
                        lum[i] = Mathf.Lerp(lum[i], l, k);
                        alp[i] = Mathf.Max(alp[i], a * k * edge);
                    }
            }

            void Vein(float x, float y, float ang, float width, int steps, int depth)
            {
                for (int s = 0; s < steps; s++)
                {
                    ang += (float)(rnd.NextDouble() - 0.5) * 0.55f;
                    x += Mathf.Cos(ang) * 2.2f;
                    y += Mathf.Sin(ang) * 2.2f;
                    float w = width * (1f - s / (float)steps * 0.7f);
                    Dab(x, y, w * 2.2f, 0.75f, 0.35f);     // hale
                    Dab(x, y, w, 1.0f, 0.95f);             // çekirdek
                    if (depth > 0 && rnd.NextDouble() < 0.06)
                        Vein(x, y, ang + (rnd.NextDouble() < 0.5 ? 0.9f : -0.9f), w * 0.7f, steps - s, depth - 1);
                }
            }

            int veins = 6 + rnd.Next(3);
            for (int v = 0; v < veins; v++)
            {
                float a0 = v / (float)veins * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.6f;
                float r0 = (float)rnd.NextDouble() * size * 0.08f;
                Vein(half + Mathf.Cos(a0) * r0, half + Mathf.Sin(a0) * r0, a0, 1.6f, 40 + rnd.Next(20), 2);
            }

            // 3) Obsidyen lekeler: koyu, opak.
            for (int k = 0; k < 4; k++)
            {
                float a0 = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r0 = size * (0.12f + 0.25f * (float)rnd.NextDouble());
                Dab(half + Mathf.Cos(a0) * r0, half + Mathf.Sin(a0) * r0, 5f + 5f * (float)rnd.NextDouble(), 0.08f, 0.9f);
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
                { name = "CorruptionVeins (runtime)", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                byte l = (byte)(Mathf.Clamp01(lum[i]) * 255f);
                px[i] = new Color32(l, l, l, (byte)(Mathf.Clamp01(alp[i]) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}
