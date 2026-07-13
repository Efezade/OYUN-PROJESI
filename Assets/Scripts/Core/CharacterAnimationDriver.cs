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
        [Tooltip("Tek karede bundan uzun yer değiştirme = ışınlanma (harita geçişi vb.) → yürüme sayılmaz.")]
        [SerializeField, Min(0.1f)] private float _teleportDistance = 1.5f;
        [Tooltip("Hareket bittikten sonra yürüme pozundan çıkmadan önce beklenecek süre (sn) — adım aralarında titremesin. Kısa: durunca ayaklar hemen dursun.")]
        [SerializeField, Min(0f)] private float _stopDelay = 0.04f;

        [Header("Yön")]
        [Tooltip("Karakter yürürken gövdesi hareket yönüne dönsün mü.")]
        [SerializeField] private bool _faceMovement = true;
        [SerializeField, Min(1f)] private float _turnSpeed = 720f;
        [Tooltip("Modelin ileri ekseni +Z değilse düzeltme açısı (derece).")]
        [SerializeField] private float _yawOffset = 0f;

        private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");

        private Vector3 _lastPos;
        private bool    _hasLastPos;
        private float   _lastMoveTime = -999f;
        private bool    _isMoving;     // Animator'a en son yazılan değer (yalnız değişince yazılır)

        private void OnEnable()
        {
            ResolveAnimator();
            _hasLastPos = false;
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
        }

        private void Update()
        {
            if (_animator == null) return;

            Vector3 pos = transform.position;
            if (!_hasLastPos) { _lastPos = pos; _hasLastPos = true; return; }

            Vector3 delta = pos - _lastPos;
            _lastPos = pos;
            delta.y  = 0f; // yalnız yatay hareket (engebe inişi/çıkışı yürüme sayılır zaten)

            float dist = delta.magnitude;
            if (dist > _teleportDistance) return; // ışınlanma — bu kareyi yok say

            float dt = Time.deltaTime;
            bool movingNow = dt > 0f && dist / dt > _moveSpeedThreshold;

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
            if (moving != _isMoving)
            {
                _isMoving = moving;
                _animator.SetBool(IsMovingParam, moving);
            }
        }
    }
}
