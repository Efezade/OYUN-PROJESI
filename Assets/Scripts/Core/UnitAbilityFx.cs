using System.Collections;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// SINIF YETENEKLERİNİN YER TUTUCU ANİMASYONLARI (Efe: "şimdilik dandik animasyon, yeter ki
    /// düz vuruş olmasın"). Dört kalıp, hepsi prosedürel — prefab/VFX asset'i istemez:
    ///   • <see cref="Lunge"/>      — yakın dövüş: birim hedefe doğru atılır, geri döner.
    ///   • <see cref="Projectile"/> — uzaktan: renkli küre kavisle uçar.
    ///   • <see cref="Burst"/>      — alan: yere yayılan, sonra sönen halka.
    ///   • <see cref="Rise"/>       — şifa/kalkan: hedeften yükselen ışık küreleri.
    /// Hepsi coroutine döndürür; <see cref="UnitAbilityCaster"/> etkiyi "vurduğu an"da uygular.
    /// Gerçek VFX geldiğinde yalnız bu sınıf değişir.
    /// </summary>
    public class UnitAbilityFx : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float _lungeSeconds      = 0.32f;
        [SerializeField, Range(0.1f, 0.9f)] private float _lungeReach = 0.45f;
        [SerializeField, Min(0.05f)] private float _projectileSeconds = 0.35f;
        [SerializeField] private float _projectileArc  = 0.8f;
        [SerializeField] private float _projectileSize = 0.28f;
        [SerializeField, Min(0.05f)] private float _burstSeconds = 0.45f;
        [SerializeField, Min(0.05f)] private float _riseSeconds  = 0.6f;
        [Tooltip("Efektlerin yerden yüksekliği (birimin göğüs hizası).")]
        [SerializeField] private float _height = 0.9f;

        private Material _template;

        /// <summary>Yakın dövüş hamlesi: hedefe doğru atıl, ortada <paramref name="onHit"/>, geri dön.</summary>
        public IEnumerator Lunge(Transform actor, Vector3 target, System.Action onHit)
        {
            if (actor == null) { onHit?.Invoke(); yield break; }
            Vector3 home = actor.position;
            Vector3 peak = Vector3.Lerp(home, new Vector3(target.x, home.y, target.z), _lungeReach);
            float half = _lungeSeconds * 0.5f;

            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                actor.position = Vector3.Lerp(home, peak, Mathf.SmoothStep(0f, 1f, t / half));
                yield return null;
            }
            actor.position = peak;
            onHit?.Invoke();
            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                actor.position = Vector3.Lerp(peak, home, Mathf.SmoothStep(0f, 1f, t / half));
                yield return null;
            }
            actor.position = home;
        }

        /// <summary>Kavisle uçan renkli küre; varınca <paramref name="onHit"/>.</summary>
        public IEnumerator Projectile(Vector3 from, Vector3 to, Color color, System.Action onHit)
        {
            from.y += _height; to.y += _height;
            GameObject ball = Blob(color, _projectileSize);
            for (float t = 0f; t < _projectileSeconds; t += Time.deltaTime)
            {
                float k = t / _projectileSeconds;
                Vector3 p = Vector3.Lerp(from, to, k);
                p.y += Mathf.Sin(k * Mathf.PI) * _projectileArc;
                ball.transform.position = p;
                yield return null;
            }
            Kill(ball);
            onHit?.Invoke();
        }

        /// <summary>Yere yayılan halka (alan hasarı); en geniş anında <paramref name="onHit"/>.</summary>
        public IEnumerator Burst(Vector3 center, float radiusWorld, Color color, System.Action onHit)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            ring.transform.position = center + Vector3.up * 0.08f;

            bool hit = false;
            for (float t = 0f; t < _burstSeconds; t += Time.deltaTime)
            {
                float k = t / _burstSeconds;
                float d = radiusWorld * 2f * Mathf.Sin(Mathf.Min(1f, k * 1.4f) * Mathf.PI * 0.5f);
                ring.transform.localScale = new Vector3(d, 0.03f * (1f - k) + 0.005f, d);
                if (!hit && k >= 0.5f) { hit = true; onHit?.Invoke(); }
                yield return null;
            }
            if (!hit) onHit?.Invoke();
            Kill(ring);
        }

        /// <summary>Hedeften yükselen ışık küreleri (şifa/kalkan); başta <paramref name="onHit"/>.</summary>
        public IEnumerator Rise(Vector3 at, Color color, System.Action onHit)
        {
            onHit?.Invoke();
            var blobs = new GameObject[5];
            for (int i = 0; i < blobs.Length; i++) blobs[i] = Blob(color, 0.16f);

            for (float t = 0f; t < _riseSeconds; t += Time.deltaTime)
            {
                float k = t / _riseSeconds;
                for (int i = 0; i < blobs.Length; i++)
                {
                    float a = i * Mathf.PI * 2f / blobs.Length + k * 4f;
                    blobs[i].transform.position = at + new Vector3(Mathf.Cos(a) * 0.45f,
                                                                   0.2f + k * 1.6f,
                                                                   Mathf.Sin(a) * 0.45f);
                    blobs[i].transform.localScale = Vector3.one * 0.16f * (1f - k * 0.7f);
                }
                yield return null;
            }
            foreach (var b in blobs) Kill(b);
        }

        // ── Yardımcı ─────────────────────────────────────────────────────────

        /// <summary>Nesneyi KOPYA malzemesiyle birlikte yok eder (malzeme sızmasın).</summary>
        private static void Kill(GameObject g)
        {
            if (g == null) return;
            var r = g.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null) Destroy(r.sharedMaterial);
            Destroy(g);
        }

        private GameObject Blob(Color color, float size)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            g.transform.localScale = Vector3.one * size;
            return g;
        }

        /// <summary>Renkli, ışıksız malzeme. Efekt başına bir kopya — yer tutucu olduğu için
        /// havuz/önbellek yok (yetenek başına birkaç nesne, savaşta seyrek).</summary>
        private Material MaterialFor(Color color)
        {
            if (_template == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _template = new Material(sh);
            }
            var m = new Material(_template);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color"))     m.SetColor("_Color", color);
            return m;
        }
    }
}
