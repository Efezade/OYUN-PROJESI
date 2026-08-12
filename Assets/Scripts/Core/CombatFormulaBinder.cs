using UnityEngine;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Savaş formülü ayarlarını (<see cref="CombatFormulaSO"/>) statik <see cref="CombatMath"/>'e
    /// yükler. Tek sorumluluk: config → formül bağlama.
    ///
    /// Neden ayrı bileşen: formülün kendisi UnityEngine'siz olmak zorunda (Unity açmadan denge
    /// taraması yapabilmek için), dolayısıyla ScriptableObject'i doğrudan okuyamıyor. Bu köprü
    /// herkesten ÖNCE (-200) çalışır, böylece ilk hasar hesabından önce değerler yerindedir.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class CombatFormulaBinder : MonoBehaviour
    {
        [Tooltip("Atanmazsa CombatMath kendi varsayılanlarıyla çalışır (DEF ölçeği 15, min hasar 1).")]
        [SerializeField] private CombatFormulaSO _formula;

        private void Awake()
        {
            if (_formula == null)
            {
                Debug.LogWarning("[Savas] CombatFormula atanmamis — varsayilan formul degerleri kullanilacak.");
                return;
            }
            _formula.Apply();
            Debug.Log($"[Savas] Formul yuklendi — DEF olcegi {CombatMath.DefenseScale}, " +
                      $"min hasar {CombatMath.MinimumDamage}, kritik %{CombatMath.CritPercent}.");
        }
    }
}
