using UnityEngine;

namespace TacticalRPG.Data
{
    [CreateAssetMenu(fileName = "BiomeBoundaries", menuName = "TacticalRPG/Biome Boundaries")]
    public sealed class BiomeBoundaryConfigSO : ScriptableObject
    {
        [SerializeField] private Material _material;
        [SerializeField, Range(.1f, .8f)] private float _halfWidth = .10f;
        [SerializeField, Range(0, .15f)] private float _irregularity = .055f;
        [SerializeField, Min(0)] private float _surfaceLift = .045f;
        [SerializeField, Range(0, 1)] private float _regionTint = .35f;
        [SerializeField, Range(0, 1)] private float _opacity = .45f;
        [SerializeField, Min(0)] private float _maxHeightDifference = .8f;
        public Material Material => _material;
        public float HalfWidth => _halfWidth;
        public float Irregularity => _irregularity;
        public float SurfaceLift => _surfaceLift;
        public float RegionTint => _regionTint;
        public float Opacity => _opacity;
        public float MaxHeightDifference => _maxHeightDifference;
    }
}
