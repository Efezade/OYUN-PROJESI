using System;
using UnityEngine;
namespace TacticalRPG.Data
{
    public enum SoundBus { Effects, Interface }
    [CreateAssetMenu(menuName="TacticalRPG/Presentation/Sound Library")]
    public sealed class SoundLibrarySO : ScriptableObject
    {
        [Serializable] public sealed class Cue
        {
            public string id;
            public AudioClip[] variants;
            [Range(0,1)] public float volume=.5f;
            [Min(0)] public float cooldown=.08f;
            public SoundBus bus;
            [Range(0,1)] public float pitchVariation=.055f;
            [Range(1,4)] public int voiceLimit=3;
        }
        [Serializable] public sealed class Theme { public string id; public AudioClip music, ambience; }
        [SerializeField] private Cue[] _cues=Array.Empty<Cue>();
        [SerializeField] private Theme[] _themes=Array.Empty<Theme>();
        [SerializeField,Range(.2f,8)] private float _crossfadeSeconds=3.2f;
        [SerializeField,Range(8,48)] private int _voices=24;
        public Cue[] Cues=>_cues;
        public Theme[] Themes=>_themes;
        public float CrossfadeSeconds=>_crossfadeSeconds;
        public int Voices=>_voices;
        public Theme FindTheme(string id)
        { foreach(var theme in _themes)if(theme.id==id)return theme;return null; }
    }
}
