using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// ÖZ KAROSUNUN GÖRÜNÜŞÜ — "öz olduğunu fark edebilmem lazım" (kullanıcı isteği 2026-08-17).
    /// Dört katman, hepsi öz TÜRÜNÜN RENGİNDE:
    ///   1. KARO BOYASI — karonun temel rengi türün rengine çekilir (taş grimsi, doğa yeşilimsi).
    ///      Sis sistemi bu rengi çarptığı için boya sis altında da doğru kararır.
    ///   2. KALIN KONTUR — karonun altıgen hatlarını saran geniş, ışıklı band.
    ///   3. KOYU ŞERİT — bandın hemen dışında ince, neredeyse siyah bir çizgi. Kontur her arazi
    ///      renginin üstünde okunsun diye var: açık kumda ya da karda tek başına renkli band
    ///      kaybolurdu, koyu şerit onu zeminden AYIRIR (kullanıcı: "hatları bold belli olsun").
    ///   4. KÜRE — karonun biraz üstünde, içi hareketli, kor gibi parlayan küre
    ///      (<see cref="EssenceOrbVisual"/>).
    ///
    /// ÖZ ALININCA: <see cref="EssenceHarvestEffect"/> göğe solan bir hüzme fırlatır, küre rengini
    /// kaybederek yukarı süzülür ve karo KALICI OLARAK GRİLEŞİR — "ruhu çekilmiş" karo. Grilik
    /// savaştan dönünce de sürer (<see cref="EssenceFieldManager.DrainedTiles"/> üzerinden geri boyanır).
    ///
    /// Veriyi <see cref="EssenceFieldManager"/> tutar; bu bileşen yalnız çizer.
    /// </summary>
    [DefaultExecutionOrder(-70)]   // öz yatakları (-85) ve düğümler (-80) yerleştikten SONRA
    public class EssenceFieldVisuals : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private EssenceFieldManager _field;
        [SerializeField] private HexGridManager      _grid;
        [SerializeField] private EssenceConfigSO     _config;
        [SerializeField] private FogOfWarManager     _fog;
        [SerializeField] private GameStateManager    _state;
        [SerializeField] private PlayerController    _player;
        [Tooltip("Çöküş bir karoyu silince üstündeki öz de kalkmalı.")]
        [SerializeField] private MapCollapseManager  _collapse;
        [Tooltip("Öz sökülme gösterisi (göğe solan ışık hüzmesi). Boşsa çalışma zamanında eklenir.")]
        [SerializeField] private EssenceHarvestEffect _harvest;

        [Header("Yedek görsel (config'te materyal yoksa)")]
        [Tooltip("Kontur bandının materyali. Boşsa çalışma zamanında kendinden ışıklı bir " +
                 "materyal üretilir — kurulum koşmasa bile kontur görünür.")]
        [SerializeField] private Material _ringMaterial;

        [Tooltip("Özü alınmış karonun kurak yüzeyi + çatlakları için SAYDAM materyal. " +
                 "Boşsa çalışma zamanında üretilir.")]
        [SerializeField] private Material _drainMaterial;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");
        private static readonly int EmissionId  = Shader.PropertyToID("_EmissionColor");

        private const string DecorRootName = "EssenceDecor";

        // Karo başına kurulan süs (küre + kontur).
        private class Decor
        {
            public GameObject       Root;
            public EssenceOrbVisual Orb;
            public Color            Color;   // öz türünün rengi (sökülme gösterisi bunu kullanır)
        }

        private readonly Dictionary<HexCoordinate, Decor> _decor = new();

        // Özü SÖKÜLMÜŞ karoların üstüne serilen kurak yüzey + çatlaklar. Ayrı tutulur çünkü
        // ömrü farklıdır: öz süsü toplanınca kalkar, kuraklık KALIR (harita yenilenene dek).
        private readonly Dictionary<HexCoordinate, GameObject> _drainedDecor = new();

        // Karonun ÖZDEN ÖNCEKİ doğal rengi. Boyama HER ZAMAN bu renkten türetilir; yoksa arka
        // arkaya gelen Refresh çağrıları rengi üst üste karıştırır ve karo giderek doygunlaşırdı.
        // Grid yeniden kurulunca (hücreler yeniden üretilir) sıfırlanır.
        private readonly Dictionary<HexCoordinate, Color> _naturalColor = new();

        private Transform             _root;
        private Mesh                  _bandMesh, _edgeMesh, _capMesh, _crackMesh;
        private Material              _runtimeRingMat, _runtimeDrainMat;
        private MaterialPropertyBlock _mpb;

        private void Awake() => _mpb = new MaterialPropertyBlock();

        private void OnEnable()
        {
            if (_field != null)
            {
                _field.OnFieldRebuilt   += Refresh;
                _field.OnDepositRemoved += HandleRemoved;
            }
            if (_grid     != null) _grid.OnGridRegenerated   += HandleGridRegenerated;
            if (_state    != null) _state.OnStateChanged     += HandleStateChanged;
            if (_player   != null) _player.OnMoved           += RefreshVisibility;
            if (_collapse != null) _collapse.OnTileCollapsed += HandleCollapsed;
        }

        private void OnDisable()
        {
            if (_field != null)
            {
                _field.OnFieldRebuilt   -= Refresh;
                _field.OnDepositRemoved -= HandleRemoved;
            }
            if (_grid     != null) _grid.OnGridRegenerated   -= HandleGridRegenerated;
            if (_state    != null) _state.OnStateChanged     -= HandleStateChanged;
            if (_player   != null) _player.OnMoved           -= RefreshVisibility;
            if (_collapse != null) _collapse.OnTileCollapsed -= HandleCollapsed;
        }

        // Grid yeniden kuruldu → hücreler YENİ nesneler, kaydedilmiş doğal renkler geçersiz.
        private void HandleGridRegenerated()
        {
            _naturalColor.Clear();
            Refresh();
        }

        private void HandleStateChanged(GameState _) => Refresh();

        private void HandleCollapsed(int _, int __)
        {
            if (_field != null) _field.PruneUnwalkable();
        }

        // ── Kurulum / yıkım ──────────────────────────────────────────────────

        /// <summary>Süsleri sıfırdan kurar. Savaş ekranındayken yalnız temizler — arenanın
        /// grid'inde overworld karoları yok, süs oraya asılı kalırdı.</summary>
        public void Refresh()
        {
            Clear();
            if (_field == null || _grid == null) return;

            // ConfirmMission de bir OVERWORLD alt-durumu (harita hâlâ ekranda) → süs kalmalı.
            // Yalnız yerleştirme/savaşta grid başka bir haritaya döner.
            if (_state != null && (_state.State == GameState.Deployment || _state.State == GameState.Combat))
                return;

            // YALNIZ PLAY'DE kurulur. Editörde harita önizlemesi üretilirken (TAM KURULUM'un son
            // adımı) süsler sahneye YAZILIRDI: ~50 yatak × ~13 nesne = 650+ GameObject ve sahne
            // dosyası şişerdi (xd.unity zaten 17 MB). Öz karoları Play'e basınca görünür.
            if (!Application.isPlaying) return;

            _root = new GameObject(DecorRootName).transform;
            _root.SetParent(transform, false);

            foreach (EssenceFieldManager.Deposit d in _field.Deposits)
                Build(d);

            // Özü ALINMIŞ karolar kurak/çatlak kalır — savaştan dönüşte de. Animasyon YOK:
            // bunlar geçmişte olmuş olaylar, tekrar oynamaz.
            foreach (HexCoordinate c in _field.DrainedTiles)
                BuildDrained(c, animate: false);

            RefreshVisibility(default);
        }

        /// <summary>Süsleri yok eder. SAHNEDE KALMIŞ eskiler de temizlenir: harita editörde
        /// üretildiğinde süsler sahneye YAZILABİLİR; Play'e basınca <see cref="_root"/> alanı
        /// serileşmediği için null gelir ve o eski kök sonsuza kadar asılı kalırdı.</summary>
        private void Clear()
        {
            _decor.Clear();
            _drainedDecor.Clear();
            _root = null;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null || child.name != DecorRootName) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else                       DestroyImmediate(child.gameObject);
            }
        }

        private void Build(EssenceFieldManager.Deposit d)
        {
            if (!_grid.TryGetCell(d.Coord, out HexCell cell)) return;

            Color color = _config != null ? _config.ColorOf(d.Type) : Color.white;
            float glow  = _config != null ? _config.Glow : 3f;

            var go = new GameObject($"Essence_{d.Type}_{d.Coord.Q}_{d.Coord.R}");
            go.transform.SetParent(_root, false);
            go.transform.position = cell.WorldPosition;

            TintTile(d.Coord, cell, color);
            BuildOutline(go.transform, cell, color, glow);

            _decor[d.Coord] = new Decor
            {
                Root  = go,
                Color = color,
                Orb   = BuildOrb(go.transform, d, cell, color, glow)
            };
        }

        // 1) Karo boyası — sis sistemi cell.BaseColor'ı parlaklıkla çarptığı için boya sisle uyumlu.
        private void TintTile(HexCoordinate coord, HexCell cell, Color color)
        {
            float k = _config != null ? _config.TileTint : 0.4f;
            ApplyCellColor(cell, Color.Lerp(NaturalColorOf(coord, cell), color, Mathf.Clamp01(k)));
        }

        // ── Özü sökülmüş karo: kararma + kuraklık + çatlaklar ────────────────

        /// <summary>
        /// "RUHU SÖMÜRÜLMÜŞ" KARO (kullanıcı isteği 2026-08-17). Üç şey birden yapılır:
        ///   1. Karonun ve ÜSTÜNDEKİ SÜSLERİN (çim tutamı, taş…) rengi külü çekilir.
        ///   2. Karonun üstüne kurak, koyu bir yüzey serilir. Bu şart: karonun kendi rengini
        ///      boyamak dokuyla ÇARPILDIĞI için yeşil çimi griye çeviremiyor, sadece koyultuyordu
        ///      ("karo kendi rengine dönüyor" şikâyetinin sebebi buydu).
        ///   3. Kurumuş toprak gibi merkezden dışa yarılan çatlaklar çizilir.
        /// <paramref name="animate"/> true ise bunlar zamanla olur; false ise doğrudan son hâl.
        /// </summary>
        private void BuildDrained(HexCoordinate coord, bool animate)
        {
            if (_root == null) return;
            if (_drainedDecor.ContainsKey(coord)) return;          // zaten kurulmuş
            if (!_grid.TryGetCell(coord, out HexCell cell)) return;

            // 1) Karonun kendi rengi + süsleri kararır.
            Color natural = NaturalColorOf(coord, cell);
            Color drained = _config != null ? _config.DrainedColorOf(natural)
                                            : Color.Lerp(natural, Color.gray, 0.88f);
            ApplyCellColor(cell, drained);
            DarkenTileProps(cell, drained);

            // 2+3) Kurak yüzey + çatlaklar.
            var go = new GameObject($"Kuraklik_{coord.Q}_{coord.R}");
            go.transform.SetParent(_root, false);
            go.transform.position = cell.WorldPosition;

            Renderer cap = AddFlat(go.transform, "KurakYuzey",
                                   ref _capMesh, BuildCapMesh, cell.SurfaceHeight + 0.011f);

            Renderer cracks = AddFlat(go.transform, "Catlaklar",
                                      ref _crackMesh, BuildCrackMesh, cell.SurfaceHeight + 0.018f);

            // Çatlak deseni TEK mesh (paylaşılır) — her karoda aynı görünmesin diye altıgen
            // simetrisine oturan rastgele bir açıyla döndürülür. Açı KOORDİNATTAN türer:
            // savaştan dönüşte aynı karo aynı çatlağı taşısın.
            int hash = (coord.Q * 73856093) ^ (coord.R * 19349663);
            cracks.transform.localRotation = Quaternion.Euler(0f, (hash & 0x3F) * 5.625f, 0f);

            var anim = go.AddComponent<EssenceDrainVisual>();
            anim.Begin(cap,    _config != null ? _config.DrainCapColor : new Color(0.2f, 0.18f, 0.16f, 0.82f),
                       cracks, _config != null ? _config.CrackColor    : new Color(0.05f, 0.04f, 0.03f, 1f),
                       _config != null ? _config.DrainDuration : 1.15f,
                       animate);

            _drainedDecor[coord] = go;
        }

        /// <summary>Karonun üstündeki süslerin (çim tutamı, taş, dal) rengini de çeker — kurak
        /// yüzeyin arasından fışkıran yemyeşil bir tutam bütün etkiyi bozardı.</summary>
        private void DarkenTileProps(HexCell cell, Color color)
        {
            if (cell.Visual == null) return;
            foreach (Renderer r in cell.Visual.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r == cell.MeshRenderer) continue;   // zemin zaten sisle boyanıyor
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, color);
                _mpb.SetColor(ColorId,     color);
                r.SetPropertyBlock(_mpb);
            }
        }

        private Renderer AddFlat(Transform parent, string name, ref Mesh cache,
                                 System.Func<Mesh> builder, float y)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);

            go.GetComponent<MeshFilter>().sharedMesh = cache ??= builder();

            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial    = EnsureDrainMaterial();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows    = false;
            return rend;
        }

        /// <summary>Karonun öz DOKUNMADAN önceki rengi. İlk sorulduğunda hücrenin o anki rengi
        /// kaydedilir — sonraki boyamalar hep bundan türer (renk üst üste karışmasın).</summary>
        private Color NaturalColorOf(HexCoordinate coord, HexCell cell)
        {
            if (_naturalColor.TryGetValue(coord, out Color c)) return c;
            _naturalColor[coord] = cell.BaseColor;
            return cell.BaseColor;
        }

        private void ApplyCellColor(HexCell cell, Color color)
        {
            cell.BaseColor = color;

            if (_fog != null) { _fog.ReapplyCellBrightness(cell); return; }
            if (cell.MeshRenderer == null) return;

            cell.MeshRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            _mpb.SetColor(ColorId,     color);
            cell.MeshRenderer.SetPropertyBlock(_mpb);
        }

        // 2+3) Kontur — kalın renkli band + dışında koyu ince şerit (zeminden ayırır).
        private void BuildOutline(Transform parent, HexCell cell, Color color, float glow)
        {
            const float TileScale = 0.95f;   // karo mesh'inin ölçeği (HexMetrics.CreateHexMesh)

            float width = _config != null ? _config.OutlineWidth : 0.18f;
            if (width <= 0.0001f) return;

            float bandOuter = TileScale;
            float bandInner = Mathf.Max(0.05f, bandOuter - width / HexMetrics.OuterRadius);

            Color lit = color * Mathf.Max(1f, glow);
            lit.a = 1f;
            AddRing(parent, cell, "Kontur", ref _bandMesh, bandOuter, bandInner, lit, 0.014f);

            float edge = _config != null ? _config.OutlineEdgeWidth : 0.045f;
            if (edge <= 0.0001f) return;

            // Koyu şerit bandın DIŞINDA, karolar arası boşluğa oturur (karo mesh'i 0.95 ölçekli,
            // 1.0'a kadar boşluk var) → komşu karonun görselini kapatmaz.
            float edgeOuter = Mathf.Min(0.999f, bandOuter + edge / HexMetrics.OuterRadius);
            Color edgeColor = _config != null ? _config.OutlineEdgeColor : new Color(0.04f, 0.04f, 0.05f);
            AddRing(parent, cell, "KonturKenar", ref _edgeMesh, edgeOuter, bandOuter, edgeColor, 0.010f);
        }

        private void AddRing(Transform parent, HexCell cell, string name, ref Mesh cache,
                             float outerScale, float innerScale, Color color, float lift)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            // Karonun ÜST yüzeyinin hemen üstü — z-fighting olmasın diye ince bir pay.
            go.transform.localPosition = new Vector3(0f, cell.SurfaceHeight + lift, 0f);

            go.GetComponent<MeshFilter>().sharedMesh = cache ??= BuildRingMesh(name, outerScale, innerScale);

            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial    = EnsureRingMaterial();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows    = false;

            rend.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            _mpb.SetColor(ColorId,     color);
            _mpb.SetColor(EmissionId,  color);
            rend.SetPropertyBlock(_mpb);
        }

        // 4) Küre — config'teki prefab; yoksa basit ama yine hareketli bir yedek küre.
        private EssenceOrbVisual BuildOrb(Transform parent, EssenceFieldManager.Deposit d,
                                          HexCell cell, Color color, float glow)
        {
            float height = _config != null ? _config.OrbHeight : 0.34f;
            float scale  = _config != null ? _config.OrbScale  : 0.36f;
            GameObject prefab = _config != null ? _config.PrefabOf(d.Type) : null;

            GameObject go = prefab != null ? Instantiate(prefab, parent) : FallbackOrb(parent);
            go.name = "Kure";
            go.transform.localPosition = new Vector3(0f, cell.SurfaceHeight + height, 0f);
            go.transform.localScale    = Vector3.one * scale;

            // Küre tıklamayı yutmasın — karoya tıklayıp üstüne yürünebilmeli.
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);

            var orb = go.GetComponent<EssenceOrbVisual>() ?? go.AddComponent<EssenceOrbVisual>();
            orb.Apply(color, glow, _config != null ? _config.ShapeOf(d.Type) : EssenceOrbShape.Kristal);
            return orb;
        }

        /// <summary>Prefab üretilmemişse: kabuk + kor + 5 zerre. Kurulum koşmasa bile öz görünür
        /// ve hareket eder (sessiz "hiçbir şey çıkmadı" durumu olmasın).</summary>
        private static GameObject FallbackOrb(Transform parent)
        {
            var root = new GameObject("Kure");
            root.transform.SetParent(parent, false);

            Sphere(root.transform, EssenceOrbVisual.ShellName, Vector3.zero, 1f);
            Sphere(root.transform, EssenceOrbVisual.CoreName,  Vector3.zero, 0.45f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * (Mathf.PI * 2f / 5f);
                Sphere(root.transform, EssenceOrbVisual.MotePrefix + i,
                       new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a * 1.7f) * 0.16f, Mathf.Sin(a) * 0.3f),
                       0.17f);
            }
            return root;
        }

        private static void Sphere(Transform parent, string name, Vector3 pos, float diameter)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale    = Vector3.one * diameter;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            }
        }

        // ── Kaldırma / görünürlük ────────────────────────────────────────────

        private void HandleRemoved(HexCoordinate c, bool collected)
        {
            if (!_decor.TryGetValue(c, out Decor decor)) return;
            _decor.Remove(c);

            bool hasCell = _grid.TryGetCell(c, out HexCell cell);

            if (collected)
            {
                // "Karonun ruhu sökülüyor": göğe solan hüzme + rengini kaybederek yükselen küre +
                // karonun KALICI grileşmesi. (Karo bu noktada zaten ovaya çevrilmiş durumda —
                // EssenceFieldManager.CollectAt sırayı böyle kuruyor.)
                if (hasCell)
                {
                    EnsureHarvest().Play(cell.WorldPosition + Vector3.up * cell.SurfaceHeight, decor.Color);

                    // Karo bu noktada ARTIK OVA: kaydedilmiş "doğal renk" eski orman/taşlık
                    // karosunundu, geçersiz. Sil ki kararma yeni (ova) renginden türesin.
                    _naturalColor.Remove(c);
                    BuildDrained(c, animate: true);
                }

                if (decor.Orb != null)
                {
                    decor.Orb.transform.SetParent(_root, true);   // kök yok edilse de gösteri bitsin
                    decor.Orb.PlayCollected(_config != null ? _config.DrainedColor : Color.gray);
                }
            }
            else
            {
                // Çöküş sildi — gösteri yok, karo doğal rengine döner.
                if (hasCell) ApplyCellColor(cell, NaturalColorOf(c, cell));
                if (decor.Orb != null) Destroy(decor.Orb.gameObject);
            }

            if (decor.Root != null) Destroy(decor.Root);
        }

        private EssenceHarvestEffect EnsureHarvest()
            => _harvest != null ? _harvest
             : _harvest = GetComponent<EssenceHarvestEffect>() ?? gameObject.AddComponent<EssenceHarvestEffect>();

        /// <summary>Sis: keşfedilmemiş karodaki öz görünmez. Update'te DEĞİL, yalnız oyuncu
        /// hareket edince çalışır (CLAUDE.md §6).</summary>
        private void RefreshVisibility(HexCoordinate _)
        {
            if (_fog == null) return;
            foreach (var kv in _decor)
            {
                if (kv.Value.Root == null) continue;
                bool visible = _fog.IsKnown(kv.Key);
                if (kv.Value.Root.activeSelf != visible) kv.Value.Root.SetActive(visible);
            }
        }

        // ── Paylaşılan görsel kaynaklar ──────────────────────────────────────

        /// <summary>Altıgen halka mesh'i — TÜM konturlar iki mesh'i paylaşır (550 karoluk
        /// haritada karo başına mesh üretmek kabul edilemezdi).</summary>
        private static Mesh BuildRingMesh(string name, float outerScale, float innerScale)
        {
            var verts = new Vector3[12];
            for (int i = 0; i < 6; i++)
            {
                verts[i * 2]     = HexMetrics.Corners[i] * outerScale;
                verts[i * 2 + 1] = HexMetrics.Corners[i] * innerScale;
            }

            var tris = new int[36];
            int t = 0;
            for (int i = 0; i < 6; i++)
            {
                int o0 = i * 2,             i0 = i * 2 + 1;
                int o1 = ((i + 1) % 6) * 2, i1 = ((i + 1) % 6) * 2 + 1;
                tris[t++] = o0; tris[t++] = o1; tris[t++] = i0;
                tris[t++] = i0; tris[t++] = o1; tris[t++] = i1;
            }

            var mesh = new Mesh { name = $"EssenceRing_{name}" };
            mesh.vertices  = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material EnsureRingMaterial()
        {
            if (_ringMaterial != null) return _ringMaterial;
            if (_runtimeRingMat != null) return _runtimeRingMat;

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Universal Render Pipeline/Lit");
            _runtimeRingMat = new Material(sh) { name = "EssenceRing (runtime)", enableInstancing = true };
            return _runtimeRingMat;
        }

        /// <summary>Kurak yüzey + çatlaklar için SAYDAM materyal. Saydamlık şart: kuraklık
        /// belirirken alfası 0'dan hedefe gider ve altındaki zemini bir miktar göstermeye devam
        /// eder — tamamen opak bir kapak, karonun üstüne yapıştırılmış düz bir altıgen gibi durur.</summary>
        private Material EnsureDrainMaterial()
        {
            if (_drainMaterial != null) return _drainMaterial;
            if (_runtimeDrainMat != null) return _runtimeDrainMat;

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Universal Render Pipeline/Lit");
            _runtimeDrainMat = new Material(sh) { name = "EssenceDrain (runtime)" };
            MakeAlphaBlended(_runtimeDrainMat);
            return _runtimeDrainMat;
        }

        /// <summary>URP materyalini alfa harmanlı saydam moda alır. Yalnız rengin alfasını
        /// düşürmek YETMEZ — yüzey tipi, harmanlama ve render sırası da ayarlanmalı.</summary>
        private static void MakeAlphaBlended(Material m)
        {
            if (m.HasProperty("_Surface"))  m.SetFloat("_Surface", 1f);   // Transparent
            if (m.HasProperty("_Blend"))    m.SetFloat("_Blend", 0f);     // Alpha
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite"))   m.SetFloat("_ZWrite", 0f);

            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>Karonun üstünü kaplayan düz altıgen (kurak yüzey). Karo mesh'iyle aynı ölçek.</summary>
        private static Mesh BuildCapMesh()
        {
            const float scale = 0.95f;

            var verts = new Vector3[7];
            verts[0] = Vector3.zero;
            for (int i = 0; i < 6; i++) verts[i + 1] = HexMetrics.Corners[i] * scale;

            var tris = new int[18];
            int t = 0;
            for (int i = 0; i < 6; i++)
            {
                tris[t++] = 0;
                tris[t++] = (i + 1) % 6 + 1;
                tris[t++] = i + 1;
            }

            var mesh = new Mesh { name = "EssenceDrainCap" };
            mesh.vertices  = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// KURUMUŞ TOPRAK ÇATLAKLARI — merkezden dışa uzanan, uca doğru incelen ince yarıklar +
        /// birkaç yan dal. Tek mesh olarak üretilir ve TÜM sökülmüş karolar paylaşır; karo başına
        /// farklılık dönüş açısından gelir (bkz. BuildDrained).
        ///
        /// Desen SABİT TOHUMLA üretilir → her oturumda aynı çatlak. Rastgele bir desen her
        /// açılışta değişirdi ve "aynı harita aynı görünür" kuralını bozardı.
        /// </summary>
        private static Mesh BuildCrackMesh()
        {
            var rnd   = new System.Random(20260817);
            var verts = new List<Vector3>();
            var tris  = new List<int>();

            // Kullanıcı geri bildirimi 2026-08-17: "çatlak sayısını 3 katına çıkar, şu an baya az".
            const int   MainCracks = 15;
            const float MaxRadius  = 0.80f;   // karo kenarına değmesin

            for (int i = 0; i < MainCracks; i++)
            {
                // Açılar eşit aralıklı + gürültülü: tam simetrik bir yıldız kurumuş toprak gibi
                // durmaz, ama tamamen rastgele açı da bir yanı boş bırakır.
                float angle = i * (Mathf.PI * 2f / MainCracks) + (float)rnd.NextDouble() * 0.55f;

                // Hepsi merkezden çıkmasın — bir kısmı karonun ortasından uzakta başlasın ki
                // desen bir "yıldız" değil, bir çatlak AĞI gibi okunsun.
                float r0 = 0.03f + (float)rnd.NextDouble() * 0.30f;
                var start = new Vector3(Mathf.Cos(angle) * r0, 0f, Mathf.Sin(angle) * r0);
                float length = (MaxRadius - r0) * (0.55f + (float)rnd.NextDouble() * 0.45f);

                Vector3[] spine = AddCrack(verts, tris, start, angle, length,
                                           0.040f, 0.009f, 4, rnd, MaxRadius);

                // Yan dallar: ana yarığın GÖVDESİNDEN ayrılır (ucundan değil).
                int branches = rnd.NextDouble() < 0.8 ? (rnd.NextDouble() < 0.45 ? 2 : 1) : 0;
                for (int b = 0; b < branches; b++)
                {
                    int k = 1 + rnd.Next(spine.Length - 2);
                    float dir = angle + (rnd.NextDouble() < 0.5 ? -1f : 1f)
                                      * (0.6f + (float)rnd.NextDouble() * 0.5f);
                    AddCrack(verts, tris, spine[k], dir,
                             length * (0.35f + (float)rnd.NextDouble() * 0.3f),
                             0.022f, 0.006f, 3, rnd, MaxRadius);
                }
            }

            var mesh = new Mesh { name = "EssenceDrainCracks" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Tek bir yarığı birbirine eklenmiş dörtgenlerden kurar: verilen NOKTADAN başlar, verilen
        /// yönde ilerler, uca doğru incelir ve her adımda hafifçe kıvrılır (düz bir çizgi çatlaktan
        /// çok çizik gibi durur). Karo dışına taşmaması için yarıçap kırpılır.
        ///
        /// Yarığın omurga noktalarını döner — yan dallar buradan başlatılır, böylece dal gerçekten
        /// ana yarığa BAĞLI görünür (eskiden dallar boşlukta duruyordu).
        /// </summary>
        private static Vector3[] AddCrack(List<Vector3> verts, List<int> tris, Vector3 start,
                                          float angle, float length, float w0, float w1,
                                          int steps, System.Random rnd, float maxRadius)
        {
            var spine = new Vector3[steps + 1];
            spine[0] = start;

            float a      = angle;
            float segLen = length / steps;

            for (int s = 0; s < steps; s++)
            {
                a += ((float)rnd.NextDouble() - 0.5f) * 0.4f;                       // kıvrım
                Vector3 next = spine[s] + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * segLen;
                if (next.magnitude > maxRadius) next = next.normalized * maxRadius; // karodan taşmasın
                spine[s + 1] = next;

                Vector3 dir = next - spine[s];
                if (dir.sqrMagnitude < 1e-8f) continue;
                dir.Normalize();
                var n = new Vector3(-dir.z, 0f, dir.x);   // XZ düzleminde dike çevir

                float wa = Mathf.Lerp(w0, w1, s / (float)steps);
                float wb = Mathf.Lerp(w0, w1, (s + 1) / (float)steps);

                int b = verts.Count;
                verts.Add(spine[s] - n * wa * 0.5f);
                verts.Add(spine[s] + n * wa * 0.5f);
                verts.Add(next     + n * wb * 0.5f);
                verts.Add(next     - n * wb * 0.5f);

                // Yukarıdan bakınca görünsün (saat yönünün tersi sarım).
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }
            return spine;
        }
    }
}
