using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Yeni zorunlu görev açılırken oynayan **gökten düşüş** animasyonu (kullanıcı isteği
    /// 2026-08-28): hedef karonun tepesinde ışık sütunu belirir → gökten altın bir kütle
    /// hızlanarak düşer → çarpma anında ışık patlaması + iki halka + toz saçılır.
    ///
    /// Tamamen PROSEDÜREL: prefab/asset gerektirmez, whitebox aşamasında da çalışır
    /// (<see cref="TowerRevealEffect"/> ile aynı desen).
    ///
    /// Efekt oyuncunun ekranı dışında da olabilir (harita 36x34, kamera oyuncuyu takip eder) —
    /// bu yüzden geri bildirimin TEK taşıyıcısı değildir: miniharita ikonu sisten bağımsız anında
    /// belirir ve üstteki zincir barı çakar. Bu animasyon "yakındaysan görürsün" katmanıdır.
    /// </summary>
    public class MandatoryQuestFallEffect : MonoBehaviour
    {
        [Header("Zamanlama (saniye)")]
        [SerializeField] private float _telegraphTime = 0.45f;   // sütun belirir, düşüş başlamadan
        [SerializeField] private float _fallTime      = 0.70f;   // gökten yere
        [SerializeField] private float _impactTime    = 1.25f;   // patlama + halkalar + toz

        [Header("Ölçüler")]
        [SerializeField] private float _fallHeight     = 40f;
        [SerializeField] private float _coreScale      = 0.9f;
        [SerializeField] private float _ringMaxRadius  = 16f;
        [SerializeField] private float _trailLength    = 6f;
        [SerializeField] private int   _dustCount      = 10;

        [Header("Renk / ışık")]
        [Tooltip("Zorunlu görev işaretiyle AYNI altın (ChapterNodeManager.MarkerColors).")]
        [SerializeField] private Color _goldColor      = new(1.00f, 0.85f, 0.20f);
        [SerializeField] private float _emission       = 4f;
        [SerializeField] private float _lightIntensity = 14f;

        /// <summary>Efekt oynuyor mu? (üst üste tetiklenmesin)</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>Verilen dünya konumuna zorunlu görevi düşür.</summary>
        public void Play(Vector3 worldPos)
        {
            if (IsPlaying || !isActiveAndEnabled) return;
            StartCoroutine(Routine(worldPos));
        }

        private IEnumerator Routine(Vector3 ground)
        {
            IsPlaying = true;

            var root = new GameObject("ZorunluGorevDususFx").transform;
            var mats = new List<Material>();

            Material beamMat  = MakeEmissive(_goldColor, _emission, mats);
            Material coreMat  = MakeEmissive(_goldColor, _emission * 1.6f, mats);
            Material trailMat = MakeEmissive(_goldColor, _emission, mats);
            Material ringMat  = MakeEmissive(_goldColor, _emission, mats);
            Material ring2Mat = MakeEmissive(_goldColor, _emission * 0.6f, mats);

            // Işık sütunu — "buraya bir şey iniyor" uyarısı.
            Transform beam = MakePrimitive(PrimitiveType.Cylinder, root, beamMat);
            beam.position   = ground + Vector3.up * (_fallHeight * 0.5f);
            beam.localScale = new Vector3(0.05f, _fallHeight * 0.5f, 0.05f);

            Transform core  = MakePrimitive(PrimitiveType.Sphere,   root, coreMat);
            Transform trail = MakePrimitive(PrimitiveType.Cylinder, root, trailMat);
            core.gameObject.SetActive(false);
            trail.gameObject.SetActive(false);

            var lightGO = new GameObject("GorevFxLight");
            lightGO.transform.SetParent(root, false);
            lightGO.transform.position = ground + Vector3.up * 3f;
            Light light = lightGO.AddComponent<Light>();
            light.type  = LightType.Point;
            light.range = 55f;
            light.color = _goldColor;
            light.intensity = 0f;

            // ── 1) Telgraf: sütun kalınlaşarak belirir ───────────────────────
            for (float t = 0f; t < _telegraphTime; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / _telegraphTime);
                float w = Mathf.Lerp(0.05f, 0.55f, k);
                beam.localScale = new Vector3(w, _fallHeight * 0.5f, w);
                light.intensity = _lightIntensity * 0.15f * k;
                yield return null;
            }

            // ── 2) Düşüş: HIZLANARAK iner (k^3 → serbest düşüş hissi) ────────
            core.gameObject.SetActive(true);
            trail.gameObject.SetActive(true);
            core.localScale = Vector3.one * _coreScale;

            for (float t = 0f; t < _fallTime; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / _fallTime);
                float fall = k * k * k;                                   // ivmeli
                float y = Mathf.Lerp(_fallHeight, 0.4f, fall);
                core.position = ground + Vector3.up * y;

                // Kuyruk: düşülen mesafeye göre uzar, kütlenin ARKASINDA (üstünde) durur.
                float len = Mathf.Min(_trailLength, (_fallHeight - y) * 0.5f);
                trail.position   = core.position + Vector3.up * (len * 0.5f);
                trail.localScale = new Vector3(0.22f, Mathf.Max(0.01f, len * 0.5f), 0.22f);

                // Sütun inildikçe söner — işi bitti, yerini kütleye bırakıyor.
                float bw = Mathf.Lerp(0.55f, 0.05f, k);
                beam.localScale = new Vector3(bw, _fallHeight * 0.5f, bw);
                light.intensity = _lightIntensity * (0.15f + 0.35f * fall);
                yield return null;
            }

            // ── 3) Çarpma: patlama + iki halka + toz ─────────────────────────
            beam.gameObject.SetActive(false);
            trail.gameObject.SetActive(false);
            core.position = ground + Vector3.up * 0.4f;

            Transform ring  = MakePrimitive(PrimitiveType.Cylinder, root, ringMat);
            Transform ring2 = MakePrimitive(PrimitiveType.Cylinder, root, ring2Mat);
            ring.position  = ground + Vector3.up * 0.06f;
            ring2.position = ground + Vector3.up * 0.05f;

            var dust    = new List<Transform>(_dustCount);
            var dustVel = new List<Vector3>(_dustCount);
            Material dustMat = MakeEmissive(_goldColor, _emission * 0.5f, mats);
            for (int i = 0; i < _dustCount; i++)
            {
                Transform d = MakePrimitive(PrimitiveType.Sphere, root, dustMat);
                d.position   = ground + Vector3.up * 0.3f;
                d.localScale = Vector3.one * Random.Range(0.12f, 0.30f);
                dust.Add(d);

                float ang = i * (360f / _dustCount) + Random.Range(-12f, 12f);
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                dustVel.Add(dir * Random.Range(4f, 9f) + Vector3.up * Random.Range(4f, 8f));
            }

            for (float t = 0f; t < _impactTime; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / _impactTime);

                float r1 = Mathf.Lerp(0.5f, _ringMaxRadius, Mathf.Sqrt(k));           // hızlı halka
                float r2 = Mathf.Lerp(0.5f, _ringMaxRadius * 0.55f, k * k);           // yavaş halka
                ring.localScale  = new Vector3(r1, 0.02f, r1);
                ring2.localScale = new Vector3(r2, 0.03f, r2);
                SetEmission(ringMat,  _goldColor, _emission * (1f - k));
                SetEmission(ring2Mat, _goldColor, _emission * 0.6f * (1f - k));

                // Kütle çarpma anında bir "nefes alır", sonra küçülüp söner (yerini karo modeli alır).
                float pop = 1f + 0.8f * Mathf.Exp(-8f * k);
                core.localScale = Vector3.one * _coreScale * pop * (1f - k * 0.85f);
                SetEmission(coreMat, _goldColor, _emission * 1.6f * (1f - k));

                for (int i = 0; i < dust.Count; i++)
                {
                    dustVel[i] += Vector3.down * (22f * Time.deltaTime);   // yerçekimi
                    dust[i].position += dustVel[i] * Time.deltaTime;
                    if (dust[i].position.y < ground.y + 0.1f)              // yere çarpınca sekmesin
                    {
                        dust[i].position = new Vector3(dust[i].position.x, ground.y + 0.1f, dust[i].position.z);
                        dustVel[i] = Vector3.zero;
                    }
                    dust[i].localScale *= 0.965f;
                }
                SetEmission(dustMat, _goldColor, _emission * 0.5f * (1f - k));

                // Işık: çarpmada patlar, üstel sönümle iner.
                light.intensity = _lightIntensity * Mathf.Exp(-4.5f * k);
                yield return null;
            }

            Destroy(root.gameObject);
            foreach (Material m in mats) Destroy(m);
            IsPlaying = false;
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
