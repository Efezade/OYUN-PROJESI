using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Zorunlu görev TAMAMLANDIĞINDA oynayan **mühürleme** animasyonu (kullanıcı isteği 2026-08-28):
    /// gökten geniş bir ışık hüzmesi karoya iner → değdiği anda karodaki yapı modelleri silinip
    /// yerini altın karo alır → halka dışa açılır, altın zerrecikler yukarı süzülür.
    ///
    /// KARO DEĞİŞİMİ GERİ ÇAĞIRMAYLA olur (<paramref name="onImpact"/>): hüzme YERE VARDIĞI karede
    /// tetiklenir. Dönüşüm önceden yapılsaydı model hüzme inmeden kaybolur, sebep-sonuç tersine
    /// dönerdi — <see cref="KamSkillVfx"/> de aynı nedenle sayıyı vuruş karesinde değiştiriyor.
    ///
    /// <see cref="MandatoryQuestFallEffect"/>'in tersi: o gökten görev İNDİRİR (altın kütle düşer),
    /// bu görevi KAPATIR (hüzme iner, yer mühürlenir). İkisi aynı altın paletini paylaşır ki
    /// oyuncu "bu ikisi aynı sistemin iki ucu" bağlantısını görsel olarak kursun.
    /// </summary>
    public class MandatoryQuestClearEffect : MonoBehaviour
    {
        [Header("Zamanlama (saniye)")]
        [SerializeField] private float _descendTime = 0.55f;   // hüzme gökten karoya iner
        [SerializeField] private float _sealTime    = 1.15f;   // mühür: halka + zerrecikler

        [Header("Ölçüler")]
        [SerializeField] private float _beamHeight  = 34f;
        [SerializeField] private float _beamRadius  = 1.25f;
        [SerializeField] private float _ringRadius  = 11f;
        [SerializeField] private int   _moteCount   = 12;

        [Header("Renk / ışık")]
        [SerializeField] private Color _goldColor      = new(1.00f, 0.85f, 0.20f);
        [SerializeField] private Color _hotColor       = new(1.00f, 0.97f, 0.78f);
        [SerializeField] private float _emission       = 4.5f;
        [SerializeField] private float _lightIntensity = 16f;

        /// <summary>Aynı anda birden çok karo mühürlenebilir (art arda biten görevler) — bu yüzden
        /// tekil "IsPlaying" kilidi YOK; her çağrı kendi coroutine'ini açar.</summary>
        public void Play(Vector3 worldPos, System.Action onImpact)
        {
            if (!isActiveAndEnabled) { onImpact?.Invoke(); return; }   // efekt kapalıysa kural yine işlesin
            StartCoroutine(Routine(worldPos, onImpact));
        }

        private IEnumerator Routine(Vector3 ground, System.Action onImpact)
        {
            var root = new GameObject("GorevMuhurFx").transform;
            var mats = new List<Material>();

            Material beamMat = MakeEmissive(_hotColor,  _emission,        mats);
            Material ringMat = MakeEmissive(_goldColor, _emission,        mats);
            Material moteMat = MakeEmissive(_goldColor, _emission * 0.8f, mats);

            Transform beam = MakePrimitive(PrimitiveType.Cylinder, root, beamMat);

            var lightGO = new GameObject("MuhurLight");
            lightGO.transform.SetParent(root, false);
            lightGO.transform.position = ground + Vector3.up * 3f;
            Light light = lightGO.AddComponent<Light>();
            light.type      = LightType.Point;
            light.range     = 50f;
            light.color     = _hotColor;
            light.intensity = 0f;

            // ── 1) Hüzme iner: ALTI gökten karoya doğru süzülür ──────────────
            // Silindir merkezden ölçeklendiği için alt uç ile üst ucu ayrı hesaplanıyor;
            // yoksa "inen ışık" değil, "büyüyen sütun" görünürdü.
            for (float t = 0f; t < _descendTime; t += Time.deltaTime)
            {
                float k       = Mathf.Clamp01(t / _descendTime);
                float bottomY = Mathf.Lerp(_beamHeight, 0f, k * k);      // ivmelenerek iner
                float topY    = _beamHeight + 6f;
                float half    = (topY - bottomY) * 0.5f;

                beam.position   = ground + Vector3.up * (bottomY + half);
                beam.localScale = new Vector3(_beamRadius, Mathf.Max(0.01f, half), _beamRadius);
                light.intensity = _lightIntensity * (0.2f + 0.8f * k);
                light.transform.position = ground + Vector3.up * (bottomY + 2f);
                yield return null;
            }

            // ── 2) DEĞDİ: karo şimdi dönüşür (model silinir, altın karo gelir) ──
            onImpact?.Invoke();

            Transform ring = MakePrimitive(PrimitiveType.Cylinder, root, ringMat);
            ring.position  = ground + Vector3.up * 0.06f;

            var motes    = new List<Transform>(_moteCount);
            var moteRise = new List<float>(_moteCount);
            for (int i = 0; i < _moteCount; i++)
            {
                Transform m = MakePrimitive(PrimitiveType.Sphere, root, moteMat);
                float ang = i * (360f / _moteCount) + Random.Range(-14f, 14f);
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                m.position   = ground + dir * Random.Range(0.3f, 1.4f) + Vector3.up * 0.2f;
                m.localScale = Vector3.one * Random.Range(0.10f, 0.22f);
                motes.Add(m);
                moteRise.Add(Random.Range(1.6f, 3.4f));   // zerrecikler farklı hızda SÜZÜLÜR
            }

            // ── 3) Mühür: hüzme geri çekilir, halka açılır, zerrecikler yükselir ──
            for (float t = 0f; t < _sealTime; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / _sealTime);

                // Hüzme yukarı doğru toplanıp söner ("işi bitti, çekiliyor").
                float bottomY = Mathf.Lerp(0f, _beamHeight, k * k);
                float half    = Mathf.Max(0.01f, (_beamHeight + 6f - bottomY) * 0.5f);
                float w       = _beamRadius * (1f - k);
                beam.position   = ground + Vector3.up * (bottomY + half);
                beam.localScale = new Vector3(w, half, w);
                SetEmission(beamMat, _hotColor, _emission * (1f - k));

                float r = Mathf.Lerp(0.6f, _ringRadius, Mathf.Sqrt(k));
                ring.localScale = new Vector3(r, 0.02f, r);
                SetEmission(ringMat, _goldColor, _emission * (1f - k));

                for (int i = 0; i < motes.Count; i++)
                {
                    motes[i].position += Vector3.up * (moteRise[i] * Time.deltaTime);
                    motes[i].localScale *= 0.975f;
                }
                SetEmission(moteMat, _goldColor, _emission * 0.8f * (1f - k));

                light.intensity = _lightIntensity * Mathf.Exp(-3.5f * k);
                yield return null;
            }

            Destroy(root.gameObject);
            foreach (Material m in mats) Destroy(m);
        }

        // ── Prosedürel yardımcılar (TowerRevealEffect ile aynı desen) ────────

        private static Transform MakePrimitive(PrimitiveType type, Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);   // efekt tıklamayı/ışını engellemesin
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static Material MakeEmissive(Color color, float intensity, List<Material> track)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color"))     m.SetColor("_Color", color);
            SetEmission(m, color, intensity);
            track.Add(m);
            return m;
        }

        private static void SetEmission(Material m, Color color, float intensity)
        {
            if (!m.HasProperty("_EmissionColor")) return;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", color * Mathf.Max(0f, intensity));
        }
    }
}
