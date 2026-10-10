using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Grid;

namespace TacticalRPG.Data
{
    /// <summary>
    /// Bir bölümün BÖLGE SETİ — hangi bölgeler var ve nasıl dağılırlar. Bölümün kural seti
    /// (<see cref="ChapterRulesSO"/>) bunu gösterir; boşsa harita bölgesiz (eski iklim kuralı) üretilir.
    /// </summary>
    [CreateAssetMenu(fileName = "BolgeSeti", menuName = "TacticalRPG/Region Set")]
    public class RegionSetSO : ScriptableObject
    {
        [SerializeField] private List<RegionSO> _regions = new();

        [Header("Dağılım")]
        [Tooltip("Sınırın ne kadar girintili olduğu (0 = düz çizgi).")]
        [SerializeField, Range(0f, 2f)] private float _borderNoise = 0.9f;
        [Tooltip("Sınır karosunun komşu bölgeden karo çekme olasılığı (geçiş yumuşar).")]
        [SerializeField, Range(0f, 0.6f)] private float _borderMix = 0.25f;
        [Tooltip("Oyuncu giriş (Start) bölgesinde doğsun.")]
        [SerializeField] private bool _startInStartRegion = true;

        public IReadOnlyList<RegionSO> Regions => _regions;
        public int Count => _regions != null ? _regions.Count : 0;

        public RegionSO Get(int index)
            => _regions != null && index >= 0 && index < _regions.Count ? _regions[index] : null;

        /// <summary>Üretici planı. Boş giriş varsa (null RegionSO) atlanır.</summary>
        public RegionPlan ToPlan(int layoutSeed, List<RegionSO> usedOrder)
        {
            usedOrder?.Clear();
            var defs = new List<RegionDef>();
            if (_regions != null)
                foreach (var r in _regions)
                {
                    if (r == null) continue;
                    defs.Add(r.ToDef());
                    usedOrder?.Add(r);
                }
            return new RegionPlan
            {
                Regions = defs.ToArray(), BorderNoise = _borderNoise, BorderMix = _borderMix,
                StartInStartRegion = _startInStartRegion, LayoutSeed = layoutSeed
            };
        }
    }
}
