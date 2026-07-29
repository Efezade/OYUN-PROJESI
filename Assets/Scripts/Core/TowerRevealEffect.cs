using System.Collections;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Kule ile sis açılırken oynayan **epik açılış efekti**: gökten inen ışık hüzmesi + zeminde
    /// dışa genişleyen halka + anlık ışık patlaması.
    ///
    /// Bu efekt eskiden <see cref="WatchtowerManager"/>'ın içindeydi; o bileşen ADA BAŞINA "tüm
    /// haritayı aç" mantığıyla çalışıyor ve bölüm dünyasında kullanılmıyor. Efekt oradan buraya
    /// AYRILDI ki düğüm tabanlı kule (<see cref="ChapterNodeManager"/>, 9 karo çapında açma) da
    /// aynı görseli oynatabilsin. <see cref="WatchtowerManager"/> kendi kopyasıyla çalışmaya
    /// devam ediyor — alternatif dünya bozulmadı.
    /// </summary>
    public class TowerRevealEffect : MonoBehaviour
    {
        [SerializeField] private float _duration       = 1.6f;
        [SerializeField] private float _beamHeight     = 14f;
        [SerializeField] private float _beamIntensity  = 3f;
        [SerializeField] private float _ringMaxRadius  = 22f;
        [SerializeField] private float _lightIntensity = 8f;

        [SerializeField] private Color _beamColor = new(1f, 0.95f, 0.6f);
        [SerializeField] private Color _ringColor = new(0.6f, 0.85f, 1f);

        /// <summary>Efekt şu an oynuyor mu? (üst üste tetiklenmesin)</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>Verilen dünya konumunda açılış efektini oynat.</summary>
        public void Play(Vector3 worldPos)
        {
            if (IsPlaying || !isActiveAndEnabled) return;
            StartCoroutine(Routine(worldPos));
        }

        private IEnumerator Routine(Vector3 basePos)
        {
            IsPlaying = true;
            var root = new GameObject("KuleAcilisFx").transform;

            // 1) Işık hüzmesi — dikey emissive silindir (gökten inen sütun).
            Material beamMat = MakeEmissive(_beamColor, _beamIntensity);
            Transform beam = MakePrimitive(PrimitiveType.Cylinder, root, beamMat);
            beam.position   = basePos + Vector3.up * (_beamHeight * 0.5f);
            beam.localScale = new Vector3(0.6f, _beamHeight * 0.5f, 0.6f);

            // 2) Zeminde dışa açılan emissive halka (yassı disk).
            Material ringMat = MakeEmissive(_ringColor, _beamIntensity);
            Transform ring = MakePrimitive(PrimitiveType.Cylinder, root, ringMat);
            ring.position   = basePos + Vector3.up * 0.06f;
            ring.localScale = new Vector3(0.5f, 0.02f, 0.5f);

            // 3) Anlık ışık patlaması.
            var lightGO = new GameObject("KuleLight");
            lightGO.transform.SetParent(root, false);
            lightGO.transform.position = basePos + Vector3.up * 3f;
            Light light = lightGO.AddComponent<Light>();
            light.type  = LightType.Point;
            light.range = 45f;
            light.color = _beamColor;

            float t = 0f;
            while (t < _duration)
            {
                t += Time.deltaTime;
                float k     = Mathf.Clamp01(t / _duration);
                float pulse = Mathf.Sin(k * Mathf.PI);   // 0→1→0 (belir, sön)

                float thick = 0.6f * pulse + 0.05f;      // hüzme: kalınlığı belirip söner
                beam.localScale = new Vector3(thick, _beamHeight * 0.5f, thick);

                float r = Mathf.Lerp(0.5f, _ringMaxRadius, k);   // halka: dışa genişler
                ring.localScale = new Vector3(r, 0.02f, r);
                SetEmission(ringMat, _ringColor, _beamIntensity * (1f - k));

                light.intensity = _lightIntensity * pulse;
                yield return null;
            }

            Destroy(root.gameObject);
            Destroy(beamMat);
            Destroy(ringMat);
            IsPlaying = false;
        }

        private static Transform MakePrimitive(PrimitiveType type, Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);   // efekt tıklamayı/ışını engellemesin
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static Material MakeEmissive(Color color, float intensity)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color"))     m.SetColor("_Color", color);
            SetEmission(m, color, intensity);
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
