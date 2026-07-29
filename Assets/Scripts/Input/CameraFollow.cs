using UnityEngine;

// DİKKAT: namespace `TacticalRPG.Input` OLMAMALI. Öyle olursa `TacticalRPG.*` altındaki her dosyada
// `Input.GetKeyDown(...)` bu namespace'e çözülür ve `UnityEngine.Input` gölgelenir → proje derlenmez.
// (Bir kez denendi, 9 dosya birden kırıldı.) Dosya Input/ klasöründe duruyor, ama namespace
// projedeki diğer sahne bileşenleriyle aynı: TacticalRPG.Core (bkz MapInputHandler).
namespace TacticalRPG.Core
{
    /// <summary>
    /// İzometrik kamerayı hedefin (Kam) üstünde tutar — harita 22×25'e büyüyünce sabit kamera
    /// yetmiyordu, karakter haritanın üst kısmında ekrandan çıkıyordu.
    ///
    /// Kamera AÇISINA dokunmaz: konumu her zaman `hedef − forward × mesafe` olarak hesaplanır, yani
    /// rotasyon ne olursa olsun hedef ekranın TAM ORTASINDA kalır. İzometrik görünüm (30°/45°,
    /// ortografik) aynen korunur; kurulumdaki açıyı değiştirirsen takip de ona uyar.
    ///
    /// Savaşta oyuncu GameObject'i gizlendiği için (bkz GameStateManager) hedef pasifken kamera
    /// OLDUĞU YERDE DURUR — savaş haritasında görünmez bir hedefi takip etmeye çalışmaz.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [Tooltip("Takip edilecek hedef (Player). Boşsa kamera hiç hareket etmez.")]
        [SerializeField] private Transform _target;

        [Tooltip("Kameranın hedeften geriye doğru uzaklığı. Ortografik kamerada yakınlık/uzaklık " +
                 "GÖRÜNTÜ ÖLÇEĞİNİ değiştirmez (onu Camera.orthographicSize belirler) — bu sadece " +
                 "kırpma düzlemleri için yeterli mesafeyi verir.")]
        [SerializeField, Min(1f)] private float _distance = 22f;

        [Tooltip("Hedefin biraz ÜSTÜNE bakmak için dünya-uzayı kaydırma (karakterin ayağı değil " +
                 "gövdesi merkezde olsun).")]
        [SerializeField] private Vector3 _lookOffset = new(0f, 0.8f, 0f);

        [Tooltip("Yumuşatma süresi (saniye). 0 = anında yapış (sert takip).")]
        [SerializeField, Min(0f)] private float _smoothTime = 0.15f;

        private Vector3 _velocity;

        private void Start() => SnapToTarget();

        /// <summary>Hedefi çalışma zamanında değiştir (ör. farklı bir karakteri takip et).</summary>
        public void SetTarget(Transform target)
        {
            _target = target;
            SnapToTarget();
        }

        /// <summary>Yumuşatmayı atlayıp hedefe ANINDA otur (harita/bölüm değişiminde kaymasın).</summary>
        public void SnapToTarget()
        {
            if (_target == null) return;
            transform.position = DesiredPosition();
            _velocity = Vector3.zero;
        }

        // Hareket LateUpdate'te uygulanır: oyuncu o kare hareket ettikten SONRA kamera oturur,
        // yoksa bir kare geriden gelir (titreme).
        private void LateUpdate()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy) return;

            Vector3 desired = DesiredPosition();
            transform.position = _smoothTime <= 0f
                ? desired
                : Vector3.SmoothDamp(transform.position, desired, ref _velocity, _smoothTime);
        }

        private Vector3 DesiredPosition() => _target.position + _lookOffset - transform.forward * _distance;
    }
}
