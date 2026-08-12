using System.Collections;
using UnityEngine;
using TMPro;

namespace TacticalRPG.Core
{
    /// <summary>
    /// DAVUL KAROSU GERİ BİLDİRİMİ — "bir şey oldu"yu oyuncuya gösteren tek yer.
    ///
    /// Üç dil konuşur:
    ///   1. <see cref="Burst"/>      — etki alanı büyüklüğünde genişleyen halka (nerede oldu)
    ///   2. <see cref="FloatingText"/> — birimin üstünde yükselen yazı (ne oldu: "+2 CAN", "SERSEM")
    ///   3. Kalıcı aura halkası (<see cref="CreateAuraRing"/>) — bu karo hâlâ etkili, alanı bu kadar
    ///
    /// Neden ayrı bileşen: <c>AugmentTileManager</c> KURALLARI çözer, burası yalnız gösterir.
    /// İkisi tek sınıf olsaydı "etki çalışıyor mu yoksa sadece animasyon mu oynadı" sorusu
    /// ayrıştırılamazdı — mevcut hatanın (görsel değişiyor ama etki yok) kaynağı tam olarak bu
    /// karışıklıktı.
    ///
    /// Malzemeler çalışma zamanında üretilir (Whiteboxing: renk/süre Inspector'dan gelir).
    /// </summary>
    public class AugmentFeedback : MonoBehaviour
    {
        [Header("Halka")]
        [SerializeField, Min(8)]  private int   _segments   = 48;
        [SerializeField, Min(0f)] private float _burstTime  = 0.7f;
        [SerializeField]          private float _ringWidth  = 0.16f;
        [Tooltip("Halkanın zeminden yüksekliği (karo üstü ~0.3).")]
        [SerializeField]          private float _ringLift   = 0.42f;

        [Header("Yükselen yazı")]
        [SerializeField] private float _textRise     = 1.5f;
        [SerializeField] private float _textDuration = 1.25f;
        [SerializeField] private float _textSize     = 3.2f;
        [Tooltip("Yazının birim tepesinden başlangıç yüksekliği.")]
        [SerializeField] private float _textLift     = 2.1f;

        private Material _ringMat;
        private Transform _root;

        private void Awake()
        {
            _root = new GameObject("AugmentFX").transform;
            _root.SetParent(transform, false);
        }

        // ── Halka ────────────────────────────────────────────────────────────

        /// <summary>Merkezden dışa açılan tek halka — etki alanının büyüklüğünü gösterir.</summary>
        public void Burst(Vector3 center, float radius, Color color)
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(BurstRoutine(center, Mathf.Max(0.4f, radius), color));
        }

        private IEnumerator BurstRoutine(Vector3 center, float radius, Color color)
        {
            LineRenderer lr = NewRing(_root, color);
            float t = 0f;
            while (t < _burstTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / _burstTime);
                DrawCircle(lr, center, Mathf.Lerp(0.25f, radius, EaseOut(k)));
                SetRingColor(lr, color, 1f - k * k);      // sonda hızlanan sönme
                yield return null;
            }
            if (lr != null) Destroy(lr.gameObject);
        }

        /// <summary>Karonun etki alanını KALICI olarak çizen sönük halka. Karo tükenince
        /// çağıran nesneyi yok eder.</summary>
        public GameObject CreateAuraRing(Vector3 center, float radius, Color color, float alpha = 0.28f)
        {
            LineRenderer lr = NewRing(_root, color);
            DrawCircle(lr, center, radius);
            SetRingColor(lr, color, alpha);
            return lr.gameObject;
        }

        private LineRenderer NewRing(Transform parent, Color color)
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace   = true;
            lr.loop            = true;
            lr.widthMultiplier = _ringWidth;
            lr.positionCount   = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows  = false;
            lr.sharedMaterial  = EnsureRingMaterial();
            lr.material        = new Material(lr.sharedMaterial);  // her halka kendi alfası
            SetRingColor(lr, color, 1f);
            return lr;
        }

        private Material EnsureRingMaterial()
        {
            if (_ringMat != null) return _ringMat;

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            _ringMat = new Material(sh);
            // URP'de saydamlık yalnız bu bayrak seti ile açılır (CollapseWaveEffect ile aynı reçete).
            if (_ringMat.HasProperty("_Surface"))
            {
                _ringMat.SetFloat("_Surface",  1f);
                _ringMat.SetFloat("_Blend",    0f);
                _ringMat.SetFloat("_ZWrite",   0f);
                _ringMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _ringMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _ringMat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                _ringMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            _ringMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return _ringMat;
        }

        private static void SetRingColor(LineRenderer lr, Color color, float alpha)
        {
            if (lr == null) return;
            Color c = color; c.a = Mathf.Clamp01(color.a * alpha);
            Material m = lr.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
            lr.startColor = lr.endColor = c;
        }

        private void DrawCircle(LineRenderer lr, Vector3 center, float radius)
        {
            if (lr == null) return;
            if (lr.positionCount != _segments) lr.positionCount = _segments;
            for (int i = 0; i < _segments; i++)
            {
                float a = i / (float)_segments * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * radius,
                                              center.y + _ringLift,
                                              center.z + Mathf.Sin(a) * radius));
            }
        }

        private static float EaseOut(float k) => 1f - (1f - k) * (1f - k);

        // ── Yükselen yazı ────────────────────────────────────────────────────

        /// <summary>Birimin/karonun üstünde yükselip sönen yazı ("SERSEMLEDİ", "+2 CAN").
        /// Kameraya döner (billboard) — izometrik açıda düz yazı okunmaz.</summary>
        public void FloatingText(Vector3 worldPos, string text, Color color)
        {
            if (!isActiveAndEnabled || string.IsNullOrEmpty(text)) return;
            StartCoroutine(TextRoutine(worldPos, text, color));
        }

        private IEnumerator TextRoutine(Vector3 worldPos, string text, Color color)
        {
            var go = new GameObject("AugmentText");
            go.transform.SetParent(_root, false);
            go.transform.position = worldPos + Vector3.up * _textLift;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text      = text;
            tmp.fontSize  = _textSize;
            tmp.color     = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.GetComponent<MeshRenderer>().sortingOrder = 100;

            Camera cam = Camera.main;
            Vector3 start = go.transform.position;
            float t = 0f;
            while (t < _textDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / _textDuration);
                go.transform.position = start + Vector3.up * (_textRise * EaseOut(k));
                if (cam != null) go.transform.forward = cam.transform.forward;

                Color c = color;
                c.a = 1f - Mathf.Clamp01((k - 0.55f) / 0.45f);   // son %45'te sön
                tmp.color = c;
                yield return null;
            }
            Destroy(go);
        }
    }
}
