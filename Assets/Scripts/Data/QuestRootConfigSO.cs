using UnityEngine;

namespace TacticalRPG.Data
{
    [CreateAssetMenu(menuName = "TacticalRPG/Bayterek Quest Roots", fileName = "BayterekRoots")]
    public class QuestRootConfigSO : ScriptableObject
    {
        [SerializeField, Min(.02f)] private float _growSecondsPerTile = .18f;
        [SerializeField, Min(.02f)] private float _retractSecondsPerTile = .12f;
        [SerializeField, Min(.03f)] private float _radius = .22f;
        [SerializeField, Min(0)] private float _surfaceLift = .10f;
        [SerializeField] private Material _barkMaterial;
        [SerializeField] private Color _minimapColor = new(.43f, .24f, .15f, 1f);
        [Header("Zemini yaran kökler")]
        [SerializeField, Min(.1f)] private float _burrowAmplitude = 1.05f;
        [SerializeField] private float _burrowOffset = -.42f;
        [SerializeField, Min(1)] private float _waveLengthTiles = 3.1f;
        [SerializeField, Range(.1f,.8f)] private float _trenchHalfWidth = .34f;
        [SerializeField, Min(.05f)] private float _bankLift = .23f;
        [SerializeField, Min(.05f)] private float _eruptionHeight = .72f;
        [SerializeField, Min(.1f)] private float _settleSeconds = .8f;
        [SerializeField] private Material _earthMaterial;
        [SerializeField] private Color _earthTint = new(.28f,.19f,.10f,1);
        [SerializeField] private ParticleSystem _ruptureDustPrefab;
        public float BurrowAmplitude => _burrowAmplitude;
        public float BurrowOffset => _burrowOffset;
        public float WaveLengthTiles => _waveLengthTiles;
        public float TrenchHalfWidth => _trenchHalfWidth;
        public float BankLift => _bankLift;
        public float EruptionHeight => _eruptionHeight;
        public float SettleSeconds => _settleSeconds;
        public Material EarthMaterial => _earthMaterial;
        public Color EarthTint => _earthTint;
        public ParticleSystem RuptureDustPrefab => _ruptureDustPrefab;
        public float GrowSeconds => Mathf.Max(.02f, _growSecondsPerTile);
        public float RetractSeconds => Mathf.Max(.02f, _retractSecondsPerTile);
        public float Radius => _radius;
        public float SurfaceLift => _surfaceLift;
        public Material BarkMaterial => _barkMaterial;
        public Color MinimapColor => _minimapColor;
    }
}
