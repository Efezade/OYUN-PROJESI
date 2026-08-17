using System.Collections.Generic;
using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Karakterin gövde animasyonunu HAREKETİNDEN türetir: Transform'un yatay yer
    /// değiştirmesini her kare ölçer (hafif polling), Animator'daki "IsMoving"
    /// parametresini sürer ve gövdeyi yürüme yönüne döndürür.
    /// PlayerController / Unit koduna DOKUNMAZ (bağımsız gözlemci) — bu sayede aynı
    /// bileşen hem overworld oyuncusunda hem savaş birimlerinde çalışır ve hareket
    /// kodu ileride değişse bile animasyon bozulmaz.
    /// SAVAŞTA: aynı GameObject'te <see cref="Unit"/> varsa onun OLAYLARINA abone olur —
    /// saldırınca "Attack", ölünce "Death" trigger'ı (klip/parametre yoksa sessizce atlanır).
    /// Işınlanmalar (harita geçişi, PlaceAt) yürüme sayılmaz (_teleportDistance eşiği).
    /// Whitebox: eşikler/dönüş hızı Inspector'dan ayarlanır, koda gömülü değer yok.
    /// </summary>
    public class CharacterAnimationDriver : MonoBehaviour
    {
        [Header("Animator")]
        [Tooltip("Boş bırakılırsa çocuklardan otomatik bulunur (model runtime'da takıldığında).")]
        [SerializeField] private Animator _animator;

        [Header("Hareket Algısı")]
        [Tooltip("Bu yatay hızın (m/sn) üstü 'yürüyor' sayılır.")]
        [SerializeField, Min(0.01f)] private float _moveSpeedThreshold = 0.5f;
        [Tooltip("Tek karede bundan uzun yer değiştirme = ışınlanma adayı (harita geçişi vb.).")]
        [SerializeField, Min(0.1f)] private float _teleportDistance = 1.5f;
        [Tooltip("Işınlanma sayılmak için gereken HIZ (m/sn). Hiçbir yürüyüş bu kadar hızlı " +
                 "olamaz; en hızlı harita seyahati bile ~40 m/sn. Mesafe eşiğiyle BİRLİKTE aranır.")]
        [SerializeField, Min(10f)] private float _teleportSpeed = 80f;
        [Tooltip("Hareket bittikten sonra yürüme pozundan çıkmadan önce beklenecek süre (sn) — adım aralarında titremesin. Kısa: durunca ayaklar hemen dursun.")]
        [SerializeField, Min(0f)] private float _stopDelay = 0.04f;

        [Header("Yön")]
        [Tooltip("Karakter yürürken gövdesi hareket yönüne dönsün mü.")]
        [SerializeField] private bool _faceMovement = true;
        [SerializeField, Min(1f)] private float _turnSpeed = 720f;
        [Tooltip("Modelin ileri ekseni +Z değilse düzeltme açısı (derece).")]
        [SerializeField] private float _yawOffset = 0f;

        [Header("Adım uyumu")]
        [Tooltip("Yürüme klibinin temposu GERÇEK hıza göre ölçeklensin mi. Haritadan seyahatte " +
                 "karakter kat kat hızlı yürüyor; klip sabit kalsaydı ayaklar yerde kayardı.")]
        [SerializeField] private bool _matchClipToSpeed = true;
        [Tooltip("Klip temposunun 1.0 sayıldığı referans yürüme hızı (m/sn). Normal yürüyüşte " +
                 "oran 1 çıkar → mevcut görüntü hiç değişmez.")]
        [SerializeField, Min(0.1f)] private float _referenceSpeed = 3.5f;
        [Tooltip("Klip temposunun alt/üst sınırı — çok hızlı seyahatte bacaklar komikleşmesin.")]
        [SerializeField] private Vector2 _clipSpeedRange = new(0.75f, 3f);

        [Header("Yerinde tutma (kök hareketi)")]
        [Tooltip("Klip karakteri ileri TAŞIYORSA (Mixamo 'In Place' işaretlenmeden indirilmiş) " +
                 "iskelet kökü yatayda sabitlenir. Kapatılırsa ham klip oynar.")]
        [SerializeField] private bool _keepInPlace = true;
        [Tooltip("Kalça yüksekliğinin kaç katı yatay kayma 'bu klip yer değiştiriyor' sayılsın. " +
                 "Doğal yürüyüş salınımı bunun çok altındadır; ölçek bağımsız olsun diye ORAN.")]
        [SerializeField, Min(0.02f)] private float _driftRatio = 0.15f;

        private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");
        private static readonly int AttackParam   = Animator.StringToHash("Attack");
        private static readonly int DeathParam    = Animator.StringToHash("Death");

        private Vector3 _lastPos;
        private bool    _hasLastPos;
        private float   _lastMoveTime = -999f;
        private bool    _isMoving;     // Animator'a en son yazılan değer (yalnız değişince yazılır)
        private bool    _dead;         // ölüm klibi başladı → hareket/yön sürüşü durur
        private Unit    _unit;         // aynı GO'da savaş birimi varsa (overworld oyuncusunda YOK)
        private bool    _hasIsMoving, _hasAttack, _hasDeath;   // controller'da var mı (bir kez okunur)

        private void OnEnable()
        {
            ResolveAnimator();
            _hasLastPos = false;
        }

        // Savaş birimine bağlan. Start'ta yapılır çünkü spawner sürücüyü Unit'ten ÖNCE ekliyor
        // (OnEnable'da Unit henüz yok). Unit yoksa (overworld oyuncusu) sessizce atlanır.
        private void Start()
        {
            _unit = GetComponent<Unit>();
            if (_unit == null) return;
            _unit.OnAttackPerformed += HandleAttackPerformed;
            _unit.OnDied            += HandleDied;
        }

        private void OnDestroy()
        {
            if (_unit == null) return;
            _unit.OnAttackPerformed -= HandleAttackPerformed;
            _unit.OnDied            -= HandleDied;
        }

        private void HandleAttackPerformed(Unit target)
        {
            if (target != null) FaceTowards(target.transform.position);
            PlayAttack();
        }

        private void HandleDied(Unit _) => PlayDeath();

        /// <summary>Controller'da gerçek bir ölüm klibi/trigger'ı var mı (yoksa ölüm anında bekleme gereksiz).</summary>
        public bool HasDeathAnimation => _hasDeath;

        /// <summary>Saldırı klibini oynatır (controller'da "Attack" trigger'ı varsa).</summary>
        public void PlayAttack()
        {
            if (_dead || _animator == null || !_hasAttack) return;
            _animator.SetTrigger(AttackParam);
        }

        /// <summary>Ölüm klibini oynatır ve sürücüyü durdurur (controller'da "Death" trigger'ı varsa).</summary>
        public void PlayDeath()
        {
            if (_dead) return;
            _dead = true;
            if (_animator == null) return;

            // Hızlanmış klip temposunu SIFIRLA: hızlı yürürken ölen birimin devrilmesi de
            // hızlanmış oynardı (Update artık _dead'de erken çıktığı için kendiliğinden düzelmez).
            _clipSpeed      = 1f;
            _animator.speed = 1f;

            if (_hasIsMoving)
            {
                _isMoving = false;
                _animator.SetBool(IsMovingParam, false);
            }
            if (_hasDeath) _animator.SetTrigger(DeathParam);
        }

        /// <summary>Gövdeyi anında hedefe çevirir (saldırırken sırtı dönük kalmasın).</summary>
        public void FaceTowards(Vector3 worldPoint)
        {
            if (!_faceMovement) return;
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + _yawOffset, 0f);
        }

        /// <summary>
        /// Animator referansını (yeniden) çözer. Model runtime'da sonradan takılırsa
        /// (CharacterModelBinder.Apply) bu bileşen ondan SONRA eklenmeli — OnEnable bulur.
        /// </summary>
        public void ResolveAnimator()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            // Konum her zaman koddan gelir (PlayerController/Unit); animasyon yerinde oynar.
            if (_animator != null) _animator.applyRootMotion = false;
            CacheParameters();
            ResolveMotionRoot();
        }

        // Klip temposunu gerçek hıza oranlar. Yalnız DEĞİŞTİĞİNDE yazılır — Animator.speed her
        // karede yazmak gereksiz iş. Duruşta 1'e döner (saldırı/ölüm klipleri normal oynasın).
        private float _clipSpeed = 1f;

        private void ApplyClipSpeed(float speed)
        {
            if (_animator == null) return;

            float target = speed <= 0.01f
                ? 1f
                : Mathf.Clamp(speed / _referenceSpeed, _clipSpeedRange.x, _clipSpeedRange.y);

            if (Mathf.Abs(target - _clipSpeed) < 0.02f) return;
            _clipSpeed      = target;
            _animator.speed = target;
        }

        // ── Yerinde tutma ────────────────────────────────────────────────────
        //
        // NEDEN VAR (2026-08-17 hata raporu): "Kam seçilen karonun sonuna yürüyüp bir anda
        // ortasına ışınlanıyor". Sebep animasyon değil, KLİBİN KENDİSİ: Mixamo'dan "In Place"
        // işaretlenmeden indirilen yürüyüş klibi kalçayı metrelerce ileri taşır. `applyRootMotion`
        // kapalı olsa bile Generic rig'te bu hareket kök hareketi olarak AYRIŞTIRILMAZ — sıradan
        // bir transform eğrisi gibi kalçaya uygulanır. Sonuç: gövde GameObject'inden ileri kayar
        // (karonun kenarına kadar), yürüyüş bitip Idle'a geçince klip 0. kareye döner ve gövde
        // karonun ortasına GERİ SIÇRAR. Görünen tam olarak "ışınlanma".
        //
        // Doğru çözüm import ayarıdır (CharacterAnimationImporter kök hareketini poza gömer), ama
        // bu ayar YENİDEN IMPORT ister. Burada aynı sonucu ÇALIŞMA ZAMANINDA garantiye alıyoruz:
        // iskelet kökünün yatay kayması ölçülür, doğal salınımı AŞARSA kök yatayda sabitlenir.
        // Zaten yerinde olan klipler (Quaternius düşmanları) eşiği hiç geçmez → hiç ellenmez.
        // İzlenen adaylar. TEK bir düğüme bakmak yetmiyor: yer değiştirmeyi taşıyan düğüm FBX'ten
        // FBX'e değişir (Mixamo'da "mixamorig:Hips", bazı ihracatlarda modelin kök nesnesinin
        // KENDİSİ). Yanlış düğüme bakılırsa düzeltme sessizce hiçbir şey yapmaz — bu yüzden
        // birkaç aday birden izlenir, eşiği hangisi aşarsa O sabitlenir.
        private Transform[] _pinCandidates = System.Array.Empty<Transform>();
        private Vector3[]   _pinBase;
        private bool[]      _pinned;
        private float       _driftLimitSqr;

        private void ResolveMotionRoot()
        {
            _pinCandidates = System.Array.Empty<Transform>();
            if (!_keepInPlace || _animator == null) return;

            var list = new List<Transform> { _animator.transform };

            Transform hips = _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            hips ??= FindSkeletonRoot(_animator.transform);
            if (hips != null && hips != _animator.transform) list.Add(hips);

            _pinCandidates = list.ToArray();
            _pinBase       = new Vector3[_pinCandidates.Length];
            _pinned        = new bool[_pinCandidates.Length];

            // OnEnable/Start'ta çağrıldığı için animatör HENÜZ değerlendirmedi → bunlar bind pozudur.
            for (int i = 0; i < _pinCandidates.Length; i++)
                _pinBase[i] = _pinCandidates[i].localPosition;

            // Eşik kalça YÜKSEKLİĞİNE oranlı: model santimetre ölçeğinde de (kalça y=100) doğru
            // çalışsın. Kalça yüksekliği okunamazsa mutlak değere düşülür.
            float hipHeight = hips != null ? Mathf.Abs(hips.localPosition.y) : 0f;
            float limit     = _driftRatio * (hipHeight > 0.01f ? hipHeight : 1f);
            _driftLimitSqr  = limit * limit;
        }

        /// <summary>İskelet kökü: adı kalça/pelvis/root içeren ilk kemik; yoksa çocuğu olan ilk
        /// alt nesne (mesh'ler yaprak olur, iskelet kökünün çocukları vardır).</summary>
        private static Transform FindSkeletonRoot(Transform animatorRoot)
        {
            Transform fallback = null;
            foreach (Transform t in animatorRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == animatorRoot) continue;
                string n = t.name.ToLowerInvariant();
                if (n.Contains("hips") || n.Contains("pelvis") || n.EndsWith("root")) return t;
                if (fallback == null && t.parent == animatorRoot && t.childCount > 0) fallback = t;
            }
            return fallback;
        }

        // Animasyon değerlendirildikten SONRA çalışmalı → LateUpdate.
        private void LateUpdate()
        {
            if (_pinCandidates.Length == 0) return;

            // Ölüm klibi serbest bırakılır: devrilme/geriye düşme GERÇEKTEN gövdeyi taşır ve
            // sabitlenirse ceset olduğu yerde çöker. Klip 0. karesinden başladığı için bırakma
            // anında sıçrama olmaz.
            if (_dead) return;

            for (int i = 0; i < _pinCandidates.Length; i++)
            {
                Transform t = _pinCandidates[i];
                if (t == null) continue;

                Vector3 p  = t.localPosition;
                float   dx = p.x - _pinBase[i].x;
                float   dz = p.z - _pinBase[i].z;

                if (!_pinned[i])
                {
                    if (dx * dx + dz * dz < _driftLimitSqr) continue;   // doğal salınım — dokunma
                    _pinned[i] = true;
                    Debug.Log($"[Anim] {name}: klip yer degistiriyor " +
                              $"({Mathf.Sqrt(dx * dx + dz * dz):F2} birim, dugum '{t.name}') → " +
                              "govde yatayda SABITLENDI.");
                }

                t.localPosition = new Vector3(_pinBase[i].x, p.y, _pinBase[i].z);
            }
        }

        // Hangi parametreler var? (Klip yoksa importer parametreyi de kurmaz.) Animator.parameters
        // her çağrıda dizi ayırdığı için BİR KEZ okunur — saldırı/ölüm anında tekrar sorulmaz.
        private void CacheParameters()
        {
            _hasIsMoving = _hasAttack = _hasDeath = false;
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            foreach (AnimatorControllerParameter p in _animator.parameters)
            {
                if      (p.nameHash == IsMovingParam) _hasIsMoving = true;
                else if (p.nameHash == AttackParam)   _hasAttack   = true;
                else if (p.nameHash == DeathParam)    _hasDeath    = true;
            }
        }

        private void Update()
        {
            if (_animator == null || _dead) return;   // öldüyse yürüme/yön sürüşü yok

            Vector3 pos = transform.position;
            if (!_hasLastPos) { _lastPos = pos; _hasLastPos = true; return; }

            Vector3 delta = pos - _lastPos;
            _lastPos = pos;
            delta.y  = 0f; // yalnız yatay hareket (engebe inişi/çıkışı yürüme sayılır zaten)

            float dist  = delta.magnitude;
            float dt    = Time.deltaTime;
            float speed = dt > 0f ? dist / dt : 0f;

            // IŞINLANMA AYIRIMI: yalnız mesafeye bakmak YETMEZ. Haritadan seyahatte karakter
            // kat kat hızlı yürüyor; bir kare takılırsa (dt büyürse) tek karede alınan yol
            // eşiği aşar ve gerçek yürüyüş "ışınlanma" sanılıp animasyon donardı. Işınlanma
            // ANLIK bir sıçramadır → hem uzun hem de İMKÂNSIZ HIZLI olmalı.
            if (dist > _teleportDistance && speed > _teleportSpeed) return;
            bool movingNow = speed > _moveSpeedThreshold;

            if (_matchClipToSpeed) ApplyClipSpeed(movingNow ? speed : 0f);

            if (movingNow)
            {
                _lastMoveTime = Time.time;

                if (_faceMovement && dist > 0.0001f)
                {
                    float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg + _yawOffset;
                    Quaternion target = Quaternion.Euler(0f, yaw, 0f);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * dt);
                }
            }

            bool moving = movingNow || Time.time - _lastMoveTime < _stopDelay;
            if (!_hasIsMoving) return;
            if (moving != _isMoving)
            {
                _isMoving = moving;
                _animator.SetBool(IsMovingParam, moving);
            }
        }
    }
}
