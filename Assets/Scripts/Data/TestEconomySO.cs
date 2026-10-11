using UnityEngine;
namespace TacticalRPG.Data
{
    [CreateAssetMenu(menuName="TacticalRPG/Test Economy",fileName="TestEconomy")]
    public sealed class TestEconomySO : ScriptableObject
    {
        [Tooltip("Geçici test modu: tüm öz bedelleri ücretsiz, sahip olunan potlar ve yol taşları tükenmez. Görev ilerlemesi gerçek kazanımları izlemeye devam eder.")]
        [SerializeField] private bool _unlimited = true;
        public bool Unlimited => _unlimited;
    }
}
