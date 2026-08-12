using UnityEngine;
using TacticalRPG.Core;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Savaş formülü ayarları — Inspector'dan tweaklenir, açılışta <see cref="CombatMath"/>'e yüklenir.
    /// Formülün kendisi <see cref="CombatMath"/>'te (UnityEngine'siz, taranabilir).
    /// </summary>
    [CreateAssetMenu(fileName = "CombatFormula", menuName = "TacticalRPG/Config/CombatFormula")]
    public class CombatFormulaSO : ScriptableObject
    {
        [Header("Savunma")]
        [Tooltip("Savunmanın azalan getiri katsayısı.\n" +
                 "hasar = ATK × 100 / (100 + DEF × bu değer)\n\n" +
                 "15 → DEF 1 ≈ %13, DEF 3 ≈ %31, DEF 5 ≈ %43 indirim.\n" +
                 "Büyütürsen tank sınıflar güçlenir; küçültürsen savunma önemsizleşir.")]
        [SerializeField, Range(0, 60)] private int _defenseScale = 15;

        [Tooltip("Hasarın düşemeyeceği taban. 1 = her vuruş en az 1 hasar verir " +
                 "(eski düz-çıkarma formülünde 0 hasar olabiliyordu → ölümsüz birimler).")]
        [SerializeField, Range(0, 5)] private int _minimumDamage = 1;

        [Header("Kritik")]
        [Tooltip("Kritik vuruş hasar yüzdesi. 150 = %50 fazla.")]
        [SerializeField, Range(100, 300)] private int _critPercent = 150;

        public int DefenseScale  => _defenseScale;
        public int MinimumDamage => _minimumDamage;
        public int CritPercent   => _critPercent;

        /// <summary>Değerleri statik formül sınıfına yükler.</summary>
        public void Apply() => CombatMath.Configure(_defenseScale, _minimumDamage, _critPercent);

        // Inspector'da değer değişince Play modda anında etkisini gör.
        private void OnValidate() { if (Application.isPlaying) Apply(); }
    }
}
