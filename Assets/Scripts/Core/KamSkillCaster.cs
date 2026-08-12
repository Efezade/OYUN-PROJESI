using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TacticalRPG.Grid;
using TacticalRPG.UI;   // CameraZoomSettings (kamerayı sahiplenen bileşen)

namespace TacticalRPG.Core
{
    /// <summary>
    /// KAM'IN BÜYÜLERİNİ HEDEFLER VE ÇÖZÜMLER.
    ///
    /// AKIŞ (kullanıcı kuralı 2026-08-13):
    ///   kart seçilir → kamera UZAKLAŞIR ("ilahi bakış") → şeffaf etki alanı FARE ile birlikte
    ///   hareket eder → aynı karoya ÇİFT TIK → büyü patlar → kamera geri gelir.
    /// Escape iptal eder (kart geri döner) — yanlış tıkla bir vuruş harcanmasın.
    ///
    /// SORUMLULUK SINIRI:
    ///   • Burası: hedefleme girdisi + kuralların çözümü (hasar/can/itme/çekme/sersemletme).
    ///   • <see cref="KamSkillVfx"/>: gösterinin kendisi. Efektin "vurduğu an"da bize geri döner;
    ///     sayılar tam o karede değişir, meteor daha havadayken değil.
    ///   • <see cref="SkillAreaIndicator"/>: fareyi takip eden şeffaf alan.
    ///   • <see cref="CameraZoomSettings"/>: kamerayı O yönetir; biz yalnız çarpan veririz.
    /// </summary>
    [DefaultExecutionOrder(-25)]
    public class KamSkillCaster : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private HexGridManager     _grid;
        [SerializeField] private UnitManager        _units;
        [SerializeField] private TurnManager        _turns;
        [SerializeField] private Camera             _camera;
        [SerializeField] private SkillAreaIndicator _indicator;
        [SerializeField] private KamSkillVfx        _vfx;
        [Tooltip("Opsiyonel — yükselen yazı (hasar/can) için.")]
        [SerializeField] private AugmentFeedback    _feedback;
        [Tooltip("Opsiyonel — kamera uzaklaştırma/sarsıntı.")]
        [SerializeField] private CameraZoomSettings _zoom;
        [Tooltip("Opsiyonel — birimler itilince/çekilince karo auraları yeniden hesaplansın.")]
        [SerializeField] private AugmentTileManager _augments;

        [Header("Hedefleme")]
        [Tooltip("Hedefleme sırasında kamera bu çarpanla uzaklaşır (1 = değişmez).")]
        [SerializeField, Range(1f, 3f)] private float _cinematicZoom = 1.55f;
        [Tooltip("Çift tık için iki tık arası en fazla süre (sn).")]
        [SerializeField, Min(0.1f)] private float _doubleClickSeconds = 0.5f;

        [Header("Savrulma / çekilme")]
        [Tooltip("İtilen/çekilen birimin yeni karoya kayma süresi (sn).")]
        [SerializeField, Min(0.05f)] private float _forceMoveSeconds = 0.45f;
        [Tooltip("Savrulurken havaya kalkma yüksekliği (yay hissi).")]
        [SerializeField] private float _forceMoveArc = 0.9f;

        [Header("Renkler")]
        [SerializeField] private Color _damageColor = new(1f, 0.35f, 0.28f);
        [SerializeField] private Color _healColor   = new(1f, 0.94f, 0.68f);
        [SerializeField] private Color _stunColor   = new(0.78f, 0.76f, 0.72f);

        /// <summary>Hedefleme (fare + çift tık) bekleniyor mu?</summary>
        public bool IsAiming  { get; private set; }
        /// <summary>Gösteri oynuyor mu? (Girdi kilitli.)</summary>
        public bool IsCasting { get; private set; }
        /// <summary>Hedefleme ya da gösteri sürüyor mu — diğer girdi sistemleri buna bakar.</summary>
        public bool Busy => IsAiming || IsCasting;

        public KamSkillCatalog.Entry Current { get; private set; }

        /// <summary>Büyü tamamlandı (ya da iptal edildi). Argüman: gerçekten atıldı mı.</summary>
        public event System.Action<KamSkillCatalog.Entry, bool> OnSkillFinished;

        private System.Action<bool> _onFinished;
        private float               _lastClickTime = -99f;
        private HexCoordinate       _lastClickHex;
        private bool                _hasLastClick;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
        }

        // ── Hedeflemeye giriş / çıkış ────────────────────────────────────────

        /// <summary>Büyüyü hedefleme moduna alır. <paramref name="onFinished"/>(true) = atıldı.</summary>
        public void Begin(KamSkillCatalog.Entry skill, System.Action<bool> onFinished = null)
        {
            // Başlayamıyorsak çağıranı BEKLETME: davul "büyü atılıyor" durumunda kilitli kalır ve
            // draft bir daha açılmazdı. Hemen "atılmadı" de.
            if (skill == null || Busy) { onFinished?.Invoke(false); return; }

            Current      = skill;
            _onFinished  = onFinished;
            IsAiming     = true;
            _hasLastClick = false;

            if (_indicator != null) _indicator.Show(new Color(skill.R, skill.G, skill.B), skill.Radius);
            if (_zoom != null) _zoom.SetCinematicZoom(_cinematicZoom);

            Announce($"{skill.Name} — hedef seç, ÇİFT TIK ile at (Esc iptal).");
        }

        /// <summary>Hedeflemeyi iptal eder; kart harcanmaz.</summary>
        public void Cancel()
        {
            if (!IsAiming) return;
            var skill = Current;
            EndTargeting();
            Announce($"{skill.Name} iptal edildi.");
            Finish(skill, false);
        }

        private void EndTargeting()
        {
            IsAiming = false;
            if (_indicator != null) _indicator.Hide();
            if (_zoom != null) _zoom.SetCinematicZoom(1f);
        }

        private void Finish(KamSkillCatalog.Entry skill, bool cast)
        {
            Current = null;
            var cb = _onFinished;
            _onFinished = null;
            cb?.Invoke(cast);
            OnSkillFinished?.Invoke(skill, cast);
        }

        // ── Hedefleme girdisi ────────────────────────────────────────────────

        private void Update()
        {
            if (!IsAiming) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
            if (!TryMouseHex(out HexCoordinate hex)) return;

            // Alan HER KARE fareye bağlanır — kullanıcı isteği: "mouse ile eş zamanlı".
            if (_indicator != null) _indicator.MoveTo(hex);

            if (!UnityEngine.Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            bool doubleClick = _hasLastClick && hex == _lastClickHex &&
                               Time.time - _lastClickTime <= _doubleClickSeconds;

            _lastClickTime = Time.time;
            _lastClickHex  = hex;
            _hasLastClick  = true;

            if (doubleClick) StartCoroutine(CastRoutine(hex));
        }

        /// <summary>
        /// Farenin altındaki hex. Işın KAROYA değil, karo yüzeyi hizasındaki SONSUZ DÜZLEME
        /// atılır: böylece tahtanın dışına (denize/ormana) nişan alınırken de gösterge fareyi
        /// bırakmaz — karo collider'ına dayasaydık imleç tahtadan çıkınca alan donardı.
        /// </summary>
        private bool TryMouseHex(out HexCoordinate hex)
        {
            hex = default;
            if (_camera == null || _grid == null) return false;

            Ray ray = _camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, HexMetrics.TileHeight, 0f));
            if (!plane.Raycast(ray, out float enter)) return false;

            hex = _grid.WorldToHex(ray.GetPoint(enter));
            return true;
        }

        // ── Atış ─────────────────────────────────────────────────────────────

        private IEnumerator CastRoutine(HexCoordinate center)
        {
            var skill = Current;
            EndTargeting();
            IsCasting = true;

            // Kamera atış boyunca uzak kalsın (gösteri geniş) — bitince geri gelir.
            if (_zoom != null) _zoom.SetCinematicZoom(_cinematicZoom);

            List<Unit> affected = UnitsInArea(center, skill.Radius);
            Announce($"{skill.Name}! ({affected.Count} birim alanda)");

            bool applied = false;
            yield return _vfx != null
                ? _vfx.Play(skill, center, affected, () => { applied = true; ApplyEffect(skill, center, affected); })
                : null;

            if (!applied) ApplyEffect(skill, center, affected);   // VFX yoksa etki yine uygulanır

            if (_zoom != null) _zoom.SetCinematicZoom(1f);
            IsCasting = false;
            Finish(skill, true);
        }

        /// <summary>Kartta yazan ne ise TAM OLARAK o uygulanır (açıklama = sözleşme).</summary>
        private void ApplyEffect(KamSkillCatalog.Entry skill, HexCoordinate center, List<Unit> affected)
        {
            switch (skill.Effect)
            {
                case KamSkillEffect.Meteor:
                    foreach (var u in affected)
                    {
                        if (u == null || !u.IsAlive) continue;
                        u.TakeDamage(skill.Magnitude);
                        Float(u, $"-{skill.Magnitude} CAN", _damageColor);
                    }
                    break;

                case KamSkillEffect.Heal:
                    // İKİ TARAF DA iyileşir — kartın kendisi bunu söylüyor, sürpriz değil.
                    foreach (var u in affected)
                    {
                        if (u == null || !u.IsAlive) continue;
                        u.Heal(skill.Magnitude);
                        Float(u, $"+{skill.Magnitude} CAN", _healColor);
                    }
                    break;

                case KamSkillEffect.Petrify:
                    foreach (var u in affected)
                    {
                        if (u == null || !u.IsAlive) continue;
                        u.ApplyStun(Mathf.Max(1, skill.StunTurns));
                        Float(u, "TAŞ KESİLDİ", _stunColor);
                    }
                    break;

                case KamSkillEffect.Push:
                    Displace(center, skill.Radius, affected, outward: true, extra: skill.PushDistance);
                    break;

                case KamSkillEffect.Pull:
                    Displace(center, skill.Radius, affected, outward: false, extra: 0);
                    break;
            }

            // İtilen/çekilen birim başka karo aurasına girmiş olabilir.
            if (_augments != null) _augments.RefreshAuras();
        }

        // ── Yer değiştirme (itme / çekme) ────────────────────────────────────

        /// <summary>
        /// Alandaki birimleri merkezden DIŞA fırlatır ya da merkeze ÇEKER.
        ///
        /// Sıra önemli: itmede EN UZAKTAKİ önce gider (öndeki arkadakinin yolunu tıkamasın),
        /// çekmede EN YAKINDAKİ önce gelir (merkez içeriden dışa doğru dolsun). Hedefler ÖNCE
        /// hesaplanıp yer rezerve edilir, animasyon SONRA başlar — aksi halde iki birim aynı
        /// karoya kayardı.
        /// </summary>
        private void Displace(HexCoordinate center, int radius, List<Unit> units, bool outward, int extra)
        {
            if (_grid == null || units.Count == 0) return;

            var ordered = new List<Unit>(units);
            ordered.Sort((a, b) =>
            {
                int da = a.Coordinate.DistanceTo(center), db = b.Coordinate.DistanceTo(center);
                return outward ? db.CompareTo(da) : da.CompareTo(db);
            });

            // Tahtadaki TÜM birimlerin yeri doludur (alan dışındakiler de engel).
            var occupied = new HashSet<HexCoordinate>();
            if (_units != null)
                foreach (var u in _units.Units)
                    if (u != null && u.IsAlive) occupied.Add(u.Coordinate);

            float   size    = _grid.HexSize;
            Vector3 centerW = center.ToWorldPosition(size);

            foreach (var u in ordered)
            {
                if (u == null || !u.IsAlive) continue;

                Vector3 dir = u.Coordinate.ToWorldPosition(size) - centerW;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f)
                {
                    // Tam merkezdeki birim: rastgele bir yöne savrulur (itmede), çekmede yerinde kalır.
                    if (!outward) continue;
                    float a = Random.value * Mathf.PI * 2f;
                    dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                }
                if (!outward) dir = -dir;
                dir.Normalize();

                int dist  = u.Coordinate.DistanceTo(center);
                int steps = outward ? (radius - dist + 1) + extra : dist;
                if (steps <= 0) continue;

                occupied.Remove(u.Coordinate);
                HexCoordinate target = Slide(u.Coordinate, dir, steps, occupied);
                occupied.Add(target);

                if (target == u.Coordinate) continue;
                StartCoroutine(ForceMoveRoutine(u, target));
            }
        }

        /// <summary>Verilen yönde adım adım kayar; duvar/kenar/dolu karo yolu keserse durur.</summary>
        private HexCoordinate Slide(HexCoordinate from, Vector3 dir, int steps, HashSet<HexCoordinate> occupied)
        {
            float size = _grid.HexSize;
            HexCoordinate cur = from;

            for (int i = 0; i < steps; i++)
            {
                Vector3 curW = cur.ToWorldPosition(size);
                HexCoordinate best = cur;
                float bestDot = 0.15f;                     // geri/yana savrulma olmasın

                for (int d = 0; d < 6; d++)
                {
                    HexCoordinate n = cur.GetNeighbor(d);
                    if (!_grid.TryGetCell(n, out HexCell cell) || !cell.IsWalkable) continue;
                    if (occupied.Contains(n)) continue;

                    Vector3 delta = n.ToWorldPosition(size) - curW;
                    delta.y = 0f;
                    float dot = Vector3.Dot(delta.normalized, dir);
                    if (dot > bestDot) { bestDot = dot; best = n; }
                }

                if (best == cur) break;                    // tıkandı (duvar / tahta kenarı / birim)
                cur = best;
            }
            return cur;
        }

        private IEnumerator ForceMoveRoutine(Unit unit, HexCoordinate target)
        {
            if (unit == null) yield break;

            Vector3 from = unit.transform.position;
            Vector3 to   = from;
            if (_grid.TryGetCell(target, out HexCell cell))
                to = new Vector3(cell.WorldPosition.x, from.y, cell.WorldPosition.z);

            float t = 0f;
            while (t < _forceMoveSeconds)
            {
                if (unit == null) yield break;
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / _forceMoveSeconds);
                Vector3 p = Vector3.Lerp(from, to, k);
                p.y += Mathf.Sin(k * Mathf.PI) * _forceMoveArc;   // yay: yerde sürünmez, savrulur
                unit.transform.position = p;
                yield return null;
            }

            if (unit != null) unit.PlaceAt(target);               // mantıksal koordinat + yüzeye otur
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private List<Unit> UnitsInArea(HexCoordinate center, int radius)
        {
            var list = new List<Unit>();
            if (_units == null) return list;
            foreach (var u in _units.Units)
                if (u != null && u.IsAlive && u.Coordinate.DistanceTo(center) <= radius)
                    list.Add(u);
            return list;
        }

        private void Float(Unit u, string text, Color color)
        {
            if (_feedback != null && u != null) _feedback.FloatingText(u.transform.position, text, color);
        }

        private void Announce(string text)
        {
            if (_turns != null) _turns.Announce(text);
            else Debug.Log($"[Buyu] {text}");
        }
    }
}
