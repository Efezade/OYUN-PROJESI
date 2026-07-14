using System.Collections;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Portal ışınlanma efekti (Endgame "toz olma" hissi):
    ///   • Dissolve  — karakter modeli gizlenir, gövde hacminden küçük toz parçacıkları çıkıp
    ///     dışa/yukarı savrularak küçülüp kaybolur (toza ayrışma).
    ///   • Reassemble — tozlar dışarıdan gövde hacmine toplanır, sonra model belirir (yeniden oluşma).
    /// Whitebox: küçük küre parçacıkları (havuzlu). Player'a eklenir; modeli çocuklarından bulur.
    /// </summary>
    public class TeleportDustEffect : MonoBehaviour
    {
        [Header("Toz")]
        [SerializeField] private int   _particleCount = 320;   // bol toz
        [SerializeField] private float _duration      = 1.0f;
        [SerializeField] private float _particleSize  = 0.045f;
        [SerializeField] private Color _dustColor     = new Color(0.28f, 0.24f, 0.22f); // kül/toz
        [SerializeField] private float _spread        = 1.4f;  // dağılma / toplanma yarıçapı
        [SerializeField] private float _rise          = 1.0f;  // yukarı sürüklenme
        [Tooltip("Toplanma bitince: model yoğun tozun ALTINDA belirir, toz bu sürede eriyerek karoyu " +
                 "açar → keskin 'spawn' yerine yumuşak geçiş.")]
        [SerializeField] private float _settle        = 0.5f;

        private Renderer[]  _modelRenderers;
        private Transform   _root;
        private Transform[] _pool;
        private Vector3[]   _dir;    // parçacık başına rastgele yön
        private Mesh        _bakeTmp;

        private void Awake() => _modelRenderers = GetComponentsInChildren<Renderer>(true);

        public bool HasModel => _modelRenderers != null && _modelRenderers.Length > 0;

        // Karakterin yaklaşık hacmi (ayak transform tabanında, gövde yukarı). Disabled renderer
        // bounds'una güvenmez (SkinnedMeshRenderer gizliyken güncellenmeyebilir).
        private Bounds DustVolume() =>
            new Bounds(transform.position + Vector3.up * 0.85f, new Vector3(0.6f, 1.7f, 0.6f));

        private void SetModelVisible(bool v)
        {
            if (_modelRenderers == null) return;
            foreach (var r in _modelRenderers) if (r != null) r.enabled = v;
        }

        // ── Ayrışma ──────────────────────────────────────────────────────────
        public IEnumerator Dissolve()
        {
            EnsurePool();
            var start = SampleModelPoints(_particleCount);   // KARAKTER ŞEKLİNDE başla
            SetModelVisible(false);

            for (int i = 0; i < _particleCount; i++)
            {
                _pool[i].position   = start[i];
                _pool[i].localScale = Vector3.one * _particleSize;
                _pool[i].gameObject.SetActive(true);
            }

            float t = 0f;
            while (t < _duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / _duration);
                float e = k * k;                                    // hızlanan savrulma
                for (int i = 0; i < _particleCount; i++)
                {
                    Vector3 drift = _dir[i] * (_spread * e) + Vector3.up * (_rise * e);
                    _pool[i].position   = start[i] + drift;
                    _pool[i].localScale = Vector3.one * (_particleSize * (1f - k));
                }
                yield return null;
            }
            HidePool();
        }

        // ── Yeniden oluşma ───────────────────────────────────────────────────
        public IEnumerator Reassemble()
        {
            EnsurePool();
            var target = SampleModelPoints(_particleCount);   // KARAKTER ŞEKLİ (hedef)
            var start  = new Vector3[_particleCount];
            for (int i = 0; i < _particleCount; i++)
            {
                start[i] = target[i] + _dir[i] * _spread + Vector3.up * _rise; // dışarıda başla
                _pool[i].position   = start[i];
                _pool[i].localScale = Vector3.one * _particleSize;
                _pool[i].gameObject.SetActive(true);
            }

            // 1) Toplanma: tozlar karakter şekline oturur (model hâlâ gizli).
            float t = 0f;
            while (t < _duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / _duration);
                float e = 1f - (1f - k) * (1f - k);                 // ease-out (toplanma yavaşlar)
                for (int i = 0; i < _particleCount; i++)
                    _pool[i].position = Vector3.Lerp(start[i], target[i], e);
                yield return null;
            }

            // 2) YUMUŞAK AÇILIŞ: model YOĞUN tozun ALTINDA belirir; toz bu sürede eriyerek (küçülerek)
            //    karoyu açar → keskin 'spawn' hissi yerine tozdan var olma.
            SetModelVisible(true);
            float s = 0f;
            while (s < _settle)
            {
                s += Time.deltaTime;
                float sk = Mathf.Clamp01(s / _settle);
                for (int i = 0; i < _particleCount; i++)
                    _pool[i].localScale = Vector3.one * (_particleSize * (1f - sk));
                yield return null;
            }
            HidePool();
        }

        // Model mesh yüzeyinden count kadar DÜNYA noktası → tozlar karakter şeklinde toplanır/dağılır.
        // SkinnedMeshRenderer için pozlanmış mesh BakeMesh ile alınır; yoksa hacim (DustVolume) fallback.
        private Vector3[] SampleModelPoints(int count)
        {
            var pts = new System.Collections.Generic.List<Vector3>(1024);
            if (_bakeTmp == null) _bakeTmp = new Mesh();

            if (_modelRenderers != null)
            {
                foreach (var r in _modelRenderers)
                {
                    if (r == null) continue;
                    Vector3[] vs = null;
                    Matrix4x4 mtx = r.transform.localToWorldMatrix;
                    if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                    { smr.BakeMesh(_bakeTmp); vs = _bakeTmp.vertices; }
                    else
                    {
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null) vs = mf.sharedMesh.vertices;
                    }
                    if (vs == null) continue;
                    for (int i = 0; i < vs.Length; i++) pts.Add(mtx.MultiplyPoint3x4(vs[i]));
                }
            }

            var result = new Vector3[count];
            if (pts.Count == 0)
            {
                Bounds b = DustVolume();                          // fallback: kaba hacim
                for (int i = 0; i < count; i++) result[i] = RandomInBounds(b);
            }
            else
            {
                for (int i = 0; i < count; i++) result[i] = pts[Random.Range(0, pts.Count)];
            }
            return result;
        }

        // ── Havuz ────────────────────────────────────────────────────────────
        private void EnsurePool()
        {
            if (_pool != null) return;
            _root = new GameObject("TeleportDust").transform;

            var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh sphereMesh = probe.GetComponent<MeshFilter>().sharedMesh;
            Destroy(probe);

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var dustMat = new Material(sh);
            if (dustMat.HasProperty("_BaseColor")) dustMat.SetColor("_BaseColor", _dustColor);
            if (dustMat.HasProperty("_Color"))     dustMat.SetColor("_Color",     _dustColor);

            _pool = new Transform[_particleCount];
            _dir  = new Vector3[_particleCount];
            for (int i = 0; i < _particleCount; i++)
            {
                var go = new GameObject("dust");
                go.transform.SetParent(_root, false);
                go.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = dustMat;
                var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
                go.SetActive(false);
                _pool[i] = go.transform;
                _dir[i]  = Random.onUnitSphere;
            }
        }

        private void HidePool()
        {
            if (_pool == null) return;
            for (int i = 0; i < _particleCount; i++)
                if (_pool[i] != null) _pool[i].gameObject.SetActive(false);
        }

        private static Vector3 RandomInBounds(Bounds b) => new Vector3(
            Random.Range(b.min.x, b.max.x),
            Random.Range(b.min.y, b.max.y),
            Random.Range(b.min.z, b.max.z));
    }
}
