using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.UI;   // CameraZoomSettings (kamera sarsıntısı)

namespace TacticalRPG.Core
{
    /// <summary>
    /// KAM BÜYÜLERİNİN GÖSTERİSİ — beş büyünün animasyonu.
    ///
    /// Hepsi PROSEDÜREL: hazır VFX asset'i, particle sistemi ya da shader yok. Sebep karo
    /// modellerindekiyle aynı (bkz. TileVisualFactory): lisans/boyut derdi yok, her makinede
    /// aynı sonuç, ve Efe kendi efektini takınca burası kolayca devre dışı kalır.
    ///
    /// SORUMLULUK SINIRI: burası YALNIZ gösterir. Hasar/iyileştirme/itme/sersemletme
    /// <see cref="KamSkillCaster"/>'ın işidir; efektin "vurduğu an"da geri çağrı (onImpact)
    /// tetiklenir — böylece sayılar ekranda meteorun çarptığı KAREDE değişir, öncesinde değil.
    ///
    /// Zamanlama ilkesi (her beş büyüde aynı): HAZIRLIK → VURUŞ → ARTÇI.
    /// Hazırlık olmadan efekt "pat diye" olur ve oyuncu ne olduğunu göremez; artçı olmadan da
    /// vuruş ucuz durur.
    /// </summary>
    public class KamSkillVfx : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager     _grid;
        [Tooltip("Opsiyonel — meteor çarpmasında kamera sarsıntısı için.")]
        [SerializeField] private CameraZoomSettings _camera;

        [Header("Genel")]
        [Tooltip("Efektlerin zemin üstü referans yüksekliği.")]
        [SerializeField] private float _groundLift = 0.35f;

        [Header("Gök Ateşi")]
        [SerializeField] private float _meteorWarnTime = 0.55f;
        [SerializeField] private float _meteorFallTime = 0.75f;
        [SerializeField] private float _meteorStartHeight = 42f;
        [SerializeField] private int   _meteorDebris = 16;

        [Header("Taş Kesilme")]
        [Tooltip("Karolar ve karakterler kaç saniye taş renginde kalsın.")]
        [SerializeField] private float _petrifySeconds = 4.5f;

        private Transform _root;
        private readonly List<Material> _mats = new();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");
        private static readonly int EmissionId  = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _root = new GameObject("SkillVfx").transform;
            _root.SetParent(transform, false);
        }

        private void OnDestroy()
        {
            foreach (var m in _mats) if (m != null) Destroy(m);
            _mats.Clear();
        }

        /// <summary>Büyünün gösterisini oynatır. <paramref name="onImpact"/> efektin VURDUĞU anda
        /// tetiklenir (hasar/iyileştirme/itme tam o karede uygulanır).</summary>
        public IEnumerator Play(KamSkillCatalog.Entry skill, HexCoordinate center,
                                IReadOnlyList<Unit> affected, System.Action onImpact)
        {
            Color color = new(skill.R, skill.G, skill.B);
            Vector3 c   = World(center);
            float   r   = AreaRadius(skill.Radius);

            // Bu atışta üretilecek materyaller: bittiğinde SİLİNİR. Her büyü ~50 materyal
            // üretiyor; savaş boyunca birikselerdi bellek sessizce şişerdi.
            int matMark = _mats.Count;

            switch (skill.Effect)
            {
                case KamSkillEffect.Meteor:  yield return MeteorRoutine(c, r, color, onImpact);            break;
                case KamSkillEffect.Heal:    yield return HealRoutine(c, r, color, onImpact);              break;
                case KamSkillEffect.Push:    yield return PushRoutine(c, r, color, onImpact);              break;
                case KamSkillEffect.Petrify: yield return PetrifyRoutine(center, skill.Radius, c, r, color, affected, onImpact); break;
                case KamSkillEffect.Pull:    yield return PullRoutine(c, r, color, onImpact);              break;
                default:                     onImpact?.Invoke();                                          break;
            }

            for (int i = _mats.Count - 1; i >= matMark; i--)
            {
                if (_mats[i] != null) Destroy(_mats[i]);
                _mats.RemoveAt(i);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  1) GÖK ATEŞİ — uzaydan düşen alev topu
        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator MeteorRoutine(Vector3 center, float radius, Color color, System.Action onImpact)
        {
            // HAZIRLIK: yerde nişan halkası — nereye düşeceği önce BİLİNSİN, sonra düşsün.
            GameObject warn  = Ring(center, radius, color, 0.9f, 0.16f);
            GameObject warn2 = Ring(center, radius * 0.55f, color, 0.7f, 0.10f);
            float t = 0f;
            while (t < _meteorWarnTime)
            {
                t += Time.deltaTime;
                float k = t / _meteorWarnTime;
                float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 4f));
                SetRingColor(warn,  color, pulse);
                SetRingColor(warn2, color, pulse * 0.8f);
                ScaleRing(warn2, center, radius * (0.25f + 0.35f * Mathf.PingPong(k * 2f, 1f)));
                yield return null;
            }

            // VURUŞ: alev topu tepeden hızlanarak iner; arkasında ateş kuyruğu bırakır.
            Vector3 from = center + new Vector3(radius * 0.55f, _meteorStartHeight, radius * 0.35f);
            GameObject ball = Sphere(from, radius * 0.85f, color * 1.4f, emissive: true);
            AddTrail(ball, color, radius * 0.75f);

            t = 0f;
            while (t < _meteorFallTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / _meteorFallTime);
                float ease = k * k;                                   // yerçekimi hissi
                ball.transform.position   = Vector3.Lerp(from, center, ease);
                ball.transform.localScale = Vector3.one * radius * (0.85f + 0.35f * ease);
                ball.transform.Rotate(180f * Time.deltaTime, 90f * Time.deltaTime, 0f);
                yield return null;
            }

            onImpact?.Invoke();                                       // hasar TAM BURADA
            if (_camera != null) _camera.Shake(0.55f, 0.45f);
            Destroy(ball);
            Destroy(warn);
            Destroy(warn2);

            // ARTÇI: patlama küresi + şok halkası + moloz + kalıcı alev dilleri.
            GameObject flash = Sphere(center, 0.4f, color * 1.6f, emissive: true, transparent: true);
            GameObject shock = Ring(center, 0.3f, color, 1f, 0.30f);

            var debris = new List<(GameObject go, Vector3 vel, float spin)>();
            for (int i = 0; i < _meteorDebris; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Random.Range(0.6f, 1.5f), Mathf.Sin(a));
                GameObject d = Cube(center + Vector3.up * 0.3f, Random.Range(0.12f, 0.32f),
                                    color * Random.Range(0.5f, 1.1f), emissive: true);
                debris.Add((d, dir * Random.Range(5f, 11f), Random.Range(180f, 720f)));
            }

            var flames = new List<GameObject>();
            for (int i = 0; i < 7; i++)
            {
                float a = Random.value * Mathf.PI * 2f, rr = Random.value * radius * 0.8f;
                flames.Add(Cone(center + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr),
                                Random.Range(0.35f, 0.7f), Random.Range(0.8f, 1.9f), color * 1.3f));
            }

            const float after = 1.15f;
            t = 0f;
            while (t < after)
            {
                t += Time.deltaTime;
                float k = t / after;

                flash.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, radius * 2.4f, EaseOut(k));
                SetAlpha(flash, 1f - k);

                ScaleRing(shock, center, Mathf.Lerp(0.3f, radius * 2.1f, EaseOut(k)));
                SetRingColor(shock, color, 1f - k * k);

                foreach (var (go, vel, spin) in debris)
                {
                    if (go == null) continue;
                    Vector3 p = go.transform.position + vel * Time.deltaTime;
                    p.y = Mathf.Max(center.y, p.y - 9.8f * t * Time.deltaTime);   // yerçekimi
                    go.transform.position = p;
                    go.transform.Rotate(spin * Time.deltaTime, spin * 0.5f * Time.deltaTime, 0f);
                    go.transform.localScale *= 1f - Time.deltaTime * 0.8f;
                }

                foreach (var f in flames)
                {
                    if (f == null) continue;
                    float s = Mathf.Sin(k * Mathf.PI);                            // yükselip söner
                    Vector3 sc = f.transform.localScale;
                    f.transform.localScale = new Vector3(sc.x, Mathf.Max(0.05f, s * 2.2f), sc.z);
                }
                yield return null;
            }

            Destroy(flash); Destroy(shock);
            foreach (var (go, _, _) in debris) if (go != null) Destroy(go);
            foreach (var f in flames) if (f != null) Destroy(f);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  2) UMAY'IN ŞİFASI — iki tarafı da iyileştirir → YEŞİL DEĞİL, ak/altın ışık
        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator HealRoutine(Vector3 center, float radius, Color color, System.Action onImpact)
        {
            // Renk bilinçli olarak altın-ak: yeşil "bizim iyileştirmemiz" demek olurdu, oysa bu
            // büyü düşmanı da iyileştiriyor. Tarafsız kutsal ışık (kullanıcı isteği 2026-08-13).
            GameObject outer = Ring(center, 0.4f, color, 1f, 0.16f);
            GameObject inner = Ring(center, 0.2f, color, 0.8f, 0.10f);
            GameObject dome  = Sphere(center, 0.5f, color, emissive: true, transparent: true);

            var motes = new List<(GameObject go, float speed, float sway, float phase)>();
            int moteCount = Mathf.Clamp(Mathf.RoundToInt(radius * 9f), 18, 70);
            for (int i = 0; i < moteCount; i++)
            {
                float a = Random.value * Mathf.PI * 2f, rr = Mathf.Sqrt(Random.value) * radius;
                GameObject m = Sphere(center + new Vector3(Mathf.Cos(a) * rr, 0.1f, Mathf.Sin(a) * rr),
                                      Random.Range(0.10f, 0.22f), color * 1.5f, emissive: true);
                motes.Add((m, Random.Range(1.6f, 3.4f), Random.Range(0.2f, 0.7f), Random.value * 6.28f));
            }

            const float grow = 0.5f, total = 1.5f;
            float t = 0f;
            bool impacted = false;

            while (t < total)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / grow);

                ScaleRing(outer, center, Mathf.Lerp(0.4f, radius, EaseOut(k)));
                ScaleRing(inner, center, Mathf.Lerp(0.2f, radius * 0.62f, EaseOut(k)));
                outer.transform.Rotate(0f,  40f * Time.deltaTime, 0f);
                inner.transform.Rotate(0f, -60f * Time.deltaTime, 0f);

                dome.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, radius * 1.9f, EaseOut(k));
                SetAlpha(dome, 0.45f * (1f - t / total));

                foreach (var (go, speed, sway, phase) in motes)
                {
                    if (go == null) continue;
                    Vector3 p = go.transform.position;
                    p.y += speed * Time.deltaTime;
                    p.x += Mathf.Sin(Time.time * 2f + phase) * sway * Time.deltaTime;
                    p.z += Mathf.Cos(Time.time * 2f + phase) * sway * Time.deltaTime;
                    go.transform.position = p;
                }

                // Işık kubbesi tam açıldığında can gelir — göz "geldi" dediği anda sayı değişir.
                if (!impacted && t >= grow) { impacted = true; onImpact?.Invoke(); }

                float fade = 1f - Mathf.Clamp01((t - grow) / (total - grow));
                SetRingColor(outer, color, fade);
                SetRingColor(inner, color, fade * 0.8f);
                yield return null;
            }

            if (!impacted) onImpact?.Invoke();
            Destroy(outer); Destroy(inner); Destroy(dome);
            foreach (var (go, _, _, _) in motes) if (go != null) Destroy(go);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  3) YEL ATA — içten dışa SERT rüzgâr (toplayıcı değil, savurucu)
        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator PushRoutine(Vector3 center, float radius, Color color, System.Action onImpact)
        {
            // HAZIRLIK: merkez BİR AN içine çöker (nefes alma) — patlama ancak sıkışmadan sonra
            // "patlama" gibi okunur.
            GameObject core = Sphere(center + Vector3.up * 0.6f, radius * 0.9f, color,
                                     emissive: true, transparent: true);
            float t = 0f;
            const float squeeze = 0.18f;
            while (t < squeeze)
            {
                t += Time.deltaTime;
                float k = t / squeeze;
                core.transform.localScale = Vector3.one * Mathf.Lerp(radius * 0.9f, radius * 0.25f, k);
                SetAlpha(core, 0.25f + 0.55f * k);
                yield return null;
            }

            onImpact?.Invoke();                       // birimler TAM patlama anında savrulmaya başlar

            // VURUŞ: üç kademeli şok halkası + dışa fırlayan rüzgâr çizgileri.
            var rings = new GameObject[3];
            for (int i = 0; i < rings.Length; i++) rings[i] = Ring(center, 0.3f, color, 1f, 0.16f);

            var gusts = new List<(GameObject go, Vector3 dir)>();
            for (int i = 0; i < 26; i++)
            {
                float a = (i / 26f) * Mathf.PI * 2f + Random.Range(-0.12f, 0.12f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                GameObject g = Cube(center + dir * 0.4f + Vector3.up * Random.Range(0.2f, 1.4f),
                                    0.16f, color * 1.3f, emissive: true);
                g.transform.localScale = new Vector3(0.10f, 0.10f, Random.Range(0.7f, 1.6f));
                g.transform.rotation   = Quaternion.LookRotation(dir);
                gusts.Add((g, dir));
            }

            const float blast = 0.85f;
            t = 0f;
            while (t < blast)
            {
                t += Time.deltaTime;
                float k = t / blast;

                for (int i = 0; i < rings.Length; i++)
                {
                    float kk = Mathf.Clamp01(k - i * 0.13f);
                    ScaleRing(rings[i], center, Mathf.Lerp(0.3f, radius * (2.4f + i * 0.4f), EaseOut(kk)));
                    SetRingColor(rings[i], color, (1f - kk) * (1f - i * 0.22f));
                }

                foreach (var (go, dir) in gusts)
                {
                    if (go == null) continue;
                    go.transform.position += dir * (26f * Time.deltaTime);
                    Vector3 s = go.transform.localScale;
                    go.transform.localScale = new Vector3(s.x * 0.985f, s.y * 0.985f, s.z * 1.03f);
                }

                core.transform.localScale = Vector3.one * Mathf.Lerp(radius * 0.25f, radius * 0.05f, k);
                SetAlpha(core, 1f - k);
                yield return null;
            }

            Destroy(core);
            foreach (var r in rings) if (r != null) Destroy(r);
            foreach (var (go, _) in gusts) if (go != null) Destroy(go);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  4) TAŞ KESİLME — alan taşlaşır, sarmaşıklar çıkar, herkes donar
        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator PetrifyRoutine(HexCoordinate centerCoord, int hexRadius, Vector3 center,
                                           float radius, Color color, IReadOnlyList<Unit> affected,
                                           System.Action onImpact)
        {
            var stone = new Color(0.62f, 0.60f, 0.57f);
            var vineColor = new Color(0.24f, 0.34f, 0.20f);

            // HAZIRLIK: taş kubbe hızla açılır.
            GameObject dome = Sphere(center, 0.4f, color, emissive: false, transparent: true);
            GameObject ring = Ring(center, 0.3f, color, 1f, 0.20f);

            const float open = 0.32f;
            float t = 0f;
            while (t < open)
            {
                t += Time.deltaTime;
                float k = t / open;
                dome.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, radius * 2f, EaseOut(k));
                SetAlpha(dome, 0.5f * (1f - k * 0.4f));
                ScaleRing(ring, center, Mathf.Lerp(0.3f, radius, EaseOut(k)));
                yield return null;
            }

            onImpact?.Invoke();                                   // sersemletme TAM BURADA
            if (_camera != null) _camera.Shake(0.22f, 0.3f);

            // Karolar taş rengine döner (MPB — materyal bozulmaz) ve karakterler de taşlaşır.
            var tinted = new List<(Renderer r, Color original)>();
            TintTilesInArea(centerCoord, hexRadius, stone, tinted);
            TintUnits(affected, stone, tinted);

            // Sarmaşıklar: alanın her yerinden burularak yükselir.
            var vines = new List<GameObject>();
            int vineCount = Mathf.Clamp(Mathf.RoundToInt(radius * 6f), 10, 44);
            for (int i = 0; i < vineCount; i++)
            {
                float a = Random.value * Mathf.PI * 2f, rr = Mathf.Sqrt(Random.value) * radius;
                var pos = center + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                GameObject v = Cube(pos, 0.14f, vineColor, emissive: false);
                v.transform.localScale = new Vector3(0.14f, 0.02f, 0.14f);
                v.transform.rotation   = Quaternion.Euler(Random.Range(-18f, 18f), Random.value * 360f,
                                                          Random.Range(-18f, 18f));
                vines.Add(v);
            }

            const float grow = 0.55f;
            t = 0f;
            while (t < grow)
            {
                t += Time.deltaTime;
                float k = EaseOut(t / grow);
                foreach (var v in vines)
                {
                    if (v == null) continue;
                    Vector3 s = v.transform.localScale;
                    v.transform.localScale = new Vector3(s.x, Mathf.Lerp(0.02f, Random.Range(0.9f, 1.9f), k), s.z);
                    v.transform.Rotate(0f, 120f * Time.deltaTime, 0f);
                }
                SetAlpha(dome, 0.3f * (1f - k));
                SetRingColor(ring, color, 1f - k);
                yield return null;
            }

            Destroy(dome); Destroy(ring);

            // Taşlaşma bir süre DURUR (bu büyünün karakteri: alan donmuş görünür), sonra çözülür.
            yield return new WaitForSeconds(_petrifySeconds);

            const float release = 0.5f;
            t = 0f;
            while (t < release)
            {
                t += Time.deltaTime;
                float k = t / release;
                foreach (var v in vines)
                {
                    if (v == null) continue;
                    Vector3 s = v.transform.localScale;
                    v.transform.localScale = new Vector3(s.x, Mathf.Max(0.01f, s.y * (1f - k * 0.15f)), s.z);
                }
                RestoreTint(tinted, k);
                yield return null;
            }

            RestoreTint(tinted, 1f);
            foreach (var v in vines) if (v != null) Destroy(v);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  5) KARA KASIRGA — herkesi merkeze toplar (Clash Royale mantığı)
        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator PullRoutine(Vector3 center, float radius, Color color, System.Action onImpact)
        {
            // Huni: üstte geniş, altta dar 6 halka. Hepsi TEK bir gövdeye asılı → gövde dönünce
            // huni döner; halkalar ayrıca kendi hızlarında dönerek "burgu" hissi verir.
            var funnel = new GameObject("Funnel").transform;
            funnel.SetParent(_root, false);
            funnel.position = center;

            const int levels = 6;
            var rings = new GameObject[levels];
            for (int i = 0; i < levels; i++)
            {
                float k = i / (float)(levels - 1);
                float rr = Mathf.Lerp(radius * 0.28f, radius * 1.15f, k);
                float y  = Mathf.Lerp(0.15f, 5.2f, k);
                rings[i] = Ring(center + Vector3.up * y, rr, color, 0.9f - k * 0.25f, 0.13f);
                rings[i].transform.SetParent(funnel, true);
            }

            GameObject dust = Ring(center, radius * 0.9f, color, 0.7f, 0.22f);

            var motes = new List<(GameObject go, float ang, float rad, float y, float speed)>();
            for (int i = 0; i < 24; i++)
            {
                float a  = Random.value * Mathf.PI * 2f;
                float rr = Random.Range(radius * 0.4f, radius * 1.2f);
                float y  = Random.Range(0.2f, 4.5f);
                GameObject m = Cube(center, Random.Range(0.10f, 0.26f), color * 1.2f, emissive: true);
                motes.Add((m, a, rr, y, Random.Range(4f, 8f)));
            }

            onImpact?.Invoke();                     // çekilme kasırga dönerken başlar

            const float total = 1.7f;
            float t = 0f;
            while (t < total)
            {
                t += Time.deltaTime;
                float k = t / total;

                funnel.Rotate(0f, 420f * Time.deltaTime, 0f);
                for (int i = 0; i < levels; i++)
                {
                    if (rings[i] == null) continue;
                    rings[i].transform.Rotate(0f, (140f + i * 60f) * Time.deltaTime, 0f);
                    SetRingColor(rings[i], color, Mathf.Clamp01(1f - Mathf.Pow(k, 3f)) * (0.9f - i * 0.08f));
                }

                dust.transform.Rotate(0f, -220f * Time.deltaTime, 0f);
                ScaleRing(dust, center, radius * (0.9f + 0.12f * Mathf.Sin(t * 7f)));
                SetRingColor(dust, color, 1f - k * k);

                // Molozlar SPİRAL çizerek içeri ve yukarı — çekilme yönü gözle okunur olsun.
                for (int i = 0; i < motes.Count; i++)
                {
                    var (go, ang, rad, y, speed) = motes[i];
                    if (go == null) continue;
                    ang += speed * Time.deltaTime;
                    rad  = Mathf.Max(0.15f, rad - Time.deltaTime * radius * 0.55f);
                    y   += Time.deltaTime * 2.2f;
                    motes[i] = (go, ang, rad, y, speed);
                    go.transform.position = center + new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad);
                    go.transform.Rotate(220f * Time.deltaTime, 160f * Time.deltaTime, 0f);
                }
                yield return null;
            }

            foreach (var r in rings) if (r != null) Destroy(r);
            foreach (var (go, _, _, _, _) in motes) if (go != null) Destroy(go);
            Destroy(dust);
            Destroy(funnel.gameObject);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Boyama yardımcıları (taşlaşma)
        // ═════════════════════════════════════════════════════════════════════

        private MaterialPropertyBlock _mpb;

        private void TintTilesInArea(HexCoordinate center, int hexRadius, Color tint,
                                     List<(Renderer, Color)> captured)
        {
            if (_grid == null) return;
            for (int dq = -hexRadius; dq <= hexRadius; dq++)
                for (int dr = Mathf.Max(-hexRadius, -dq - hexRadius); dr <= Mathf.Min(hexRadius, -dq + hexRadius); dr++)
                {
                    var c = new HexCoordinate(center.Q + dq, center.R + dr);
                    if (!_grid.TryGetCell(c, out HexCell cell) || cell.Visual == null) continue;
                    foreach (var r in cell.Visual.GetComponentsInChildren<Renderer>(true))
                        Capture(r, tint, captured);
                }
        }

        private void TintUnits(IReadOnlyList<Unit> units, Color tint, List<(Renderer, Color)> captured)
        {
            if (units == null) return;
            foreach (var u in units)
            {
                if (u == null) continue;
                foreach (var r in u.GetComponentsInChildren<Renderer>(true))
                    if (r.enabled) Capture(r, tint, captured);
            }
        }

        private void Capture(Renderer r, Color tint, List<(Renderer, Color)> captured)
        {
            if (r == null) return;
            Material m = r.sharedMaterial;
            Color original = m == null ? Color.white
                           : m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId)
                           : m.HasProperty(ColorId)     ? m.GetColor(ColorId) : Color.white;
            captured.Add((r, original));
            WriteColor(r, tint);
        }

        /// <summary>k=0 → tam taş, k=1 → özgün renk.</summary>
        private void RestoreTint(List<(Renderer r, Color original)> captured, float k)
        {
            foreach (var (r, original) in captured)
            {
                if (r == null) continue;
                WriteColor(r, Color.Lerp(new Color(0.62f, 0.60f, 0.57f), original, Mathf.Clamp01(k)));
            }
        }

        private void WriteColor(Renderer r, Color c)
        {
            _mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId,     c);
            r.SetPropertyBlock(_mpb);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  İlkel geometri yardımcıları
        // ═════════════════════════════════════════════════════════════════════

        private Vector3 World(HexCoordinate coord)
        {
            if (_grid != null && _grid.TryGetCell(coord, out HexCell cell))
                return cell.WorldPosition + Vector3.up * cell.SurfaceHeight;
            float size = _grid != null ? _grid.HexSize : 1f;
            return coord.ToWorldPosition(size) + Vector3.up * _groundLift;
        }

        /// <summary>Hex yarıçapının dünya yarıçapı (komşu merkezler arası √3 × hexSize).</summary>
        private float AreaRadius(int hexRadius)
        {
            float size = _grid != null ? _grid.HexSize : 1f;
            return (hexRadius + 0.5f) * Mathf.Sqrt(3f) * size;
        }

        private GameObject Sphere(Vector3 pos, float scale, Color color, bool emissive = false,
                                  bool transparent = false)
            => Prim(PrimitiveType.Sphere, pos, Vector3.one * scale, color, emissive, transparent);

        private GameObject Cube(Vector3 pos, float scale, Color color, bool emissive = false)
            => Prim(PrimitiveType.Cube, pos, Vector3.one * scale, color, emissive, false);

        private GameObject Cone(Vector3 pos, float width, float height, Color color)
        {
            GameObject go = Prim(PrimitiveType.Cylinder, pos + Vector3.up * height * 0.5f,
                                 new Vector3(width, height * 0.5f, width), color, true, false);
            return go;
        }

        private GameObject Prim(PrimitiveType type, Vector3 pos, Vector3 scale, Color color,
                                bool emissive, bool transparent)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);                    // efekt tıklamayı YUTMASIN
            go.transform.SetParent(_root, false);
            go.transform.position   = pos;
            go.transform.localScale = scale;

            Material m = transparent ? SkillAreaIndicator.TransparentMaterial() : LitMaterial();
            SetColor(m, color);
            if (emissive && m.HasProperty(EmissionId))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionId, color * 2.2f);
            }
            _mats.Add(m);
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial    = m;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private GameObject Ring(Vector3 center, float radius, Color color, float alpha, float width)
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(_root, false);
            go.transform.position = center;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace     = false;                     // dönebilsin (kasırga halkaları)
            lr.loop              = true;
            lr.widthMultiplier   = width;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Material m = SkillAreaIndicator.TransparentMaterial();
            _mats.Add(m);
            lr.sharedMaterial = m;

            SetRingRadius(lr, radius);
            SetRingColor(go, color, alpha);
            return go;
        }

        private static void SetRingRadius(LineRenderer lr, float radius)
        {
            const int segments = 48;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }

        private static void ScaleRing(GameObject ring, Vector3 center, float radius)
        {
            if (ring == null) return;
            var lr = ring.GetComponent<LineRenderer>();
            if (lr != null) SetRingRadius(lr, radius);
        }

        private static void SetRingColor(GameObject ring, Color color, float alpha)
        {
            if (ring == null) return;
            var lr = ring.GetComponent<LineRenderer>();
            if (lr == null) return;
            Color c = color; c.a = Mathf.Clamp01(alpha);
            lr.startColor = lr.endColor = c;
            SetColor(lr.sharedMaterial, c);
        }

        private static void SetAlpha(GameObject go, float alpha)
        {
            if (go == null) return;
            var r = go.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) return;
            Material m = r.sharedMaterial;
            Color c = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
            c.a = Mathf.Clamp01(alpha);
            SetColor(m, c);
        }

        private static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId))     m.SetColor(ColorId, c);
        }

        private static Material LitMaterial()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(sh);
        }

        private void AddTrail(GameObject go, Color color, float width)
        {
            var trail = go.AddComponent<TrailRenderer>();
            trail.time             = 0.45f;
            trail.startWidth       = width;
            trail.endWidth         = 0.05f;
            trail.numCapVertices   = 4;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Material m = SkillAreaIndicator.TransparentMaterial();
            _mats.Add(m);
            SetColor(m, color);
            trail.sharedMaterial = m;
            trail.startColor = color;
            trail.endColor   = new Color(color.r, color.g, color.b, 0f);
        }

        private static float EaseOut(float k) => 1f - (1f - Mathf.Clamp01(k)) * (1f - Mathf.Clamp01(k));
    }
}
