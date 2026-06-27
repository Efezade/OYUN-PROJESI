using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Bir GameObject'in placeholder görselini (turuncu kapsül) gerçek bir karakter MODELİYLE
    /// değiştirir. Modeli child olarak instantiate eder, hedef DÜNYA boyuna AUTO-SCALE eder
    /// (FBX ölçeği ne olursa olsun — karolar 1900 birimdi), placeholder renderer'ı gizler.
    /// Hem overworld oyuncusu (PlayerController GO) hem savaş birimi (Unit kapsülü) için kullanılır:
    /// Inspector'da _modelPrefab atanırsa Awake'te; runtime spawn'da Apply(...) ile.
    /// Whitebox: yön (euler) / yükseklik / dikey ofset Inspector'dan ayarlanır, koda gömülmez.
    /// </summary>
    public class CharacterModelBinder : MonoBehaviour
    {
        [Header("Model")]
        [Tooltip("Atanırsa Awake'te otomatik takılır. Runtime spawn'da Apply(...) ile de verilebilir.")]
        [SerializeField] private GameObject _modelPrefab;
        [Tooltip("Modelin hedef DÜNYA yüksekliği (m). 0 = auto-scale kapalı (FBX orijinal boyutu).")]
        [SerializeField] private float _targetHeight = 1.6f;
        [Tooltip("Yön düzeltmesi — FBX ters/yan gelirse (ör. (0,180,0) ya da (-90,0,0)).")]
        [SerializeField] private Vector3 _euler = Vector3.zero;
        [Tooltip("Modeli dikey kaydır — ayağı zemine otursun.")]
        [SerializeField] private float _yOffset = 0f;
        [Tooltip("Açıkken alttaki kapsül mesh'i gizlenir (model onun yerine görünür).")]
        [SerializeField] private bool _hidePlaceholder = true;

        /// <summary>Takılan modelin ana renderer'ı (Unit hasar-flaşını buna yöneltebilir).</summary>
        public Renderer ModelRenderer { get; private set; }

        private GameObject _instance;

        private void Awake()
        {
            if (_modelPrefab != null && _instance == null)
                Apply(_modelPrefab, _targetHeight, _euler, _yOffset, _hidePlaceholder);
        }

        /// <summary>Modeli takar (runtime spawn için). Tekrar çağrılırsa öncekini değiştirir.</summary>
        public void Apply(GameObject modelPrefab, float targetHeight, Vector3 euler, float yOffset, bool hidePlaceholder = true)
        {
            if (modelPrefab == null) return;
            if (_instance != null) Destroy(_instance);

            if (hidePlaceholder)
                foreach (var r in GetComponents<Renderer>()) r.enabled = false; // kapsül mesh'i gizle

            _instance = Instantiate(modelPrefab, transform);
            Transform t = _instance.transform;
            t.localRotation = Quaternion.Euler(euler);
            t.localPosition = Vector3.zero;
            t.localScale    = Vector3.one;

            // Auto-scale: dünya bounds yüksekliğini ölç (parent ölçeği dahil) → hedefe ölçekle.
            if (targetHeight > 0.0001f)
            {
                Bounds b = WorldBounds(_instance);
                if (b.size.y > 0.0001f) t.localScale = Vector3.one * (targetHeight / b.size.y);
            }

            t.localPosition = new Vector3(0f, yOffset, 0f);
            ModelRenderer = _instance.GetComponentInChildren<Renderer>();
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
