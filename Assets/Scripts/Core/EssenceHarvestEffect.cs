using System.Collections;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// ÖZ SÖKÜLME EFEKTİ — "karonun ruhu çekiliyor" (kullanıcı isteği 2026-08-17).
    ///
    /// Karodan öz toplanınca:
    ///   1. Zeminde öz renginde bir halka dışa patlar (ruh karodan koptu).
    ///   2. Gökyüzüne bir IŞIK HÜZMESİ fırlar — ama tek renk değil: hüzme YÜKSELDİKÇE rengi
    ///      SOLAR. Dipte özün canlı rengi, ortada kül grisi, tepede neredeyse siyah. Hüzme tek
    ///      parça değil, üst üste dizili segmentlerden kurulur ve segmentler ALTTAN ÜSTE doğru
    ///      sırayla yanar → ışığın gerçekten yukarı doğru "gittiği" okunur.
    ///   3. Kısa bir ışık patlaması.
    /// Karonun kendisinin griye dönmesi <see cref="EssenceFieldVisuals"/>'in işi (kalıcı bir durum,
    /// bir efekt değil); bu bileşen yalnız o anki gösteriyi oynatır.
    ///
    /// Toplama başına bir kez çalışır ve kendini temizler; üst üste birden çok karo toplanabilir
    /// (her çağrı kendi kökünü kurar — <see cref="TowerRevealEffect"/> gibi tekil DEĞİL).
    /// </summary>
    public class EssenceHarvestEffect : MonoBehaviour
    {
        [Header("Hüzme")]
        [Tooltip("Hüzmenin toplam yüksekliği (dünya birimi).")]
        [SerializeField, Min(1f)] private float _beamHeight = 16f;
        [Tooltip("Hüzme kaç dilimden kurulsun. Çok az olursa renk geçişi basamaklanır.")]
        [SerializeField, Range(3, 24)] private int _segments = 10;
        [SerializeField, Min(0.02f)] private float _beamRadius = 0.16f;
        [Tooltip("Işığın dipten tepeye çıkma süresi (sn).")]
        [SerializeField, Min(0.05f)] private float _travelTime = 0.5f;
        [Tooltip("Bir dilimin yanıp sönme süresi (sn).")]
        [SerializeField, Min(0.05f)] private float _segmentLife = 0.55f;

        [Header("Renk sönümü")]
        [Tooltip("Hüzme yükselirken çekildiği ara renk (kül).")]
        [SerializeField] private Color _ashColor = new(0.42f, 0.40f, 0.38f);
        [Tooltip("Tepede kalan renk — 'ruh tükendi'. Siyaha yakın olmalı.")]
        [SerializeField] private Color _voidColor = new(0.05f, 0.05f, 0.06f);
        [Tooltip("Dipteki parlaklık çarpanı (HDR → bloom yakalar).")]
        [SerializeField, Min(0f)] private float _glow = 3.2f;

        [Header("Zemin halkası")]
        [SerializeField, Min(0.1f)] private float _ringMaxRadius = 2.4f;
        [SerializeField, Min(0.05f)] private float _ringTime = 0.45f;

        [Header("Işık")]
        [SerializeField, Min(0f)] private float _lightIntensity = 6f;
        [SerializeField, Min(1f)] private float _lightRange = 14f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        /// <summary>Karonun ÜST yüzeyinde öz sökülme gösterisini oynat.</summary>
        public void Play(Vector3 tileTopWorld, Color essenceColor)
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(Routine(tileTopWorld, essenceColor));
        }

        private IEnumerator Routine(Vector3 basePos, Color essence)
        {
            var root = new GameObject("OzSokulmeFx").transform;
            root.position = basePos;

            Material glowMat = MakeGlowMaterial();
            var mpb = new MaterialPropertyBlock();

            // ── Hüzme dilimleri: alttan üste, rengi giderek solan bir sütun ──
            float segH = _beamHeight / _segments;
            var segs      = new Renderer[_segments];
            var segColors = new Color[_segments];

            for (int i = 0; i < _segments; i++)
            {
                float u = _segments > 1 ? i / (float)(_segments - 1) : 0f;   // 0 = dip, 1 = tepe

                // Dipte özün rengi → ortada kül → tepede boşluk. İki aşamalı geçiş, tek Lerp'ten
                // daha okunur: renk önce SOLAR (doygunluk gider), sonra KARARIR.
                segColors[i] = u < 0.5f
                    ? Color.Lerp(essence,   _ashColor,  u * 2f)
                    : Color.Lerp(_ashColor, _voidColor, (u - 0.5f) * 2f);

                Transform seg = MakeCylinder(root, glowMat);
                seg.localPosition = new Vector3(0f, segH * (i + 0.5f), 0f);
                // Unity silindiri 2 birim yüksek → yarım yükseklik ölçek.
                seg.localScale = new Vector3(_beamRadius * 2f, segH * 0.5f, _beamRadius * 2f);
                segs[i] = seg.GetComponent<Renderer>();
                Paint(segs[i], mpb, Color.clear);          // yanana dek görünmez
            }

            // ── Zemin halkası: ruh karodan koparken dışa açılan dalga ──
            Transform ring = MakeCylinder(root, glowMat);
            ring.localPosition = new Vector3(0f, 0.05f, 0f);
            Renderer ringRend = ring.GetComponent<Renderer>();

            // ── Anlık ışık ──
            var lightGO = new GameObject("OzIsik");
            lightGO.transform.SetParent(root, false);
            lightGO.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            Light light = lightGO.AddComponent<Light>();
            light.type  = LightType.Point;
            light.range = _lightRange;
            light.color = essence;

            float total = _travelTime + _segmentLife;
            float t     = 0f;

            while (t < total)
            {
                t += Time.deltaTime;

                // Dilimler: i. dilim (i/N)*travelTime anında yanar, sonra _segmentLife'ta söner.
                for (int i = 0; i < _segments; i++)
                {
                    float onAt = _segments > 1 ? i / (float)(_segments - 1) * _travelTime : 0f;
                    float age  = t - onAt;

                    float k = age <= 0f || age >= _segmentLife
                        ? 0f
                        : Mathf.Sin(Mathf.Clamp01(age / _segmentLife) * Mathf.PI);   // belir → sön

                    Color c = segColors[i] * (_glow * k);
                    c.a = k;
                    Paint(segs[i], mpb, c);
                }

                // Halka: hızla dışa açılıp söner (zemine yapışık kalsın diye ince).
                float rk = Mathf.Clamp01(t / _ringTime);
                float r  = Mathf.Lerp(0.35f, _ringMaxRadius, rk);
                ring.localScale = new Vector3(r, 0.015f, r);
                Color rc = essence * (_glow * (1f - rk));
                rc.a = 1f - rk;
                Paint(ringRend, mpb, rc);

                // Işık: dipteki patlama, hüzme yükselirken söner.
                light.intensity = _lightIntensity * Mathf.Clamp01(1f - t / _travelTime);

                yield return null;
            }

            Destroy(root.gameObject);
            Destroy(glowMat);
        }

        private static void Paint(Renderer r, MaterialPropertyBlock mpb, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId,     c);
            r.SetPropertyBlock(mpb);
        }

        private static Transform MakeCylinder(Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);            // efekt tıklamayı/ışını engellemesin

            go.transform.SetParent(parent, false);
            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial    = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows    = false;
            return go.transform;
        }

        /// <summary>TOPLAMALI (additive) saydam materyal: siyah = görünmez. Bir ışık hüzmesinin
        /// sönmesi böyle doğru okunur — alfa düşünce sahnede koyu bir boru kalmaz, ışık gerçekten
        /// yok olur. Renk her dilime MaterialPropertyBlock ile yazılır (tek materyal, N dilim).</summary>
        private static Material MakeGlowMaterial()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh) { name = "OzHuzme (runtime)" };

            if (m.HasProperty("_Surface"))  m.SetFloat("_Surface", 1f);   // Transparent
            if (m.HasProperty("_Blend"))    m.SetFloat("_Blend", 2f);     // Additive
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_ZWrite"))   m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull"))     m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }
    }
}
