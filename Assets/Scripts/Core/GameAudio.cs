using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
namespace TacticalRPG.Core
{
    /// <summary>Bounded Foley voices and crossfaded regional music/ambience.
    /// Gain changes affect playing voices; assets remain Inspector-editable.</summary>
    [DefaultExecutionOrder(-120),DisallowMultipleComponent]
    public class GameAudio : MonoBehaviour
    {
        [SerializeField] private SoundLibrarySO _library;
        [SerializeField] private AudioClip _backgroundMusic;
        [SerializeField] private AudioSource _musicSource;
        [SerializeField,Range(0,1)] private float _defaultMaster=.8f;
        [SerializeField,Range(0,1)] private float _defaultMusic=.42f;
        [SerializeField,Range(0,1)] private float _defaultSfx=.8f;
        [SerializeField,Range(0,1)] private float _defaultAmbience=.6f;
        [SerializeField,Range(0,1)] private float _defaultInterface=.65f;
        private sealed class Voice
        { public AudioSource source; public SoundLibrarySO.Cue cue; public float gain; public double started; }
        private readonly Dictionary<string,SoundLibrarySO.Cue> _cues=new();
        private readonly Dictionary<string,float> _last=new();
        private readonly Dictionary<string,int> _variant=new();
        private Voice[] _voices;
        private readonly AudioSource[] _music=new AudioSource[2],_ambience=new AudioSource[2];
        private int _active;
        private float _blend=1,_duck=1,_duckTarget=1;
        private string _theme;
        private bool _menu,_cinematic;
        public static float SfxVolume{get;private set;}=1;
        public float Master{get;private set;}
        public float Music{get;private set;}
        public float Sfx{get;private set;}
        public float Ambience{get;private set;}
        public float Interface{get;private set;}
        public string CurrentTheme=>_theme;
        public SoundLibrarySO Library=>_library;
        public int PlayedCount{get;private set;}
        public int ActiveVoiceCount
        { get {int n=0;if(_voices!=null)foreach(var v in _voices)if(v.source.isPlaying)n++;return n;} }
        private AudioSource Source(string label,bool loop)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var s=go.AddComponent<AudioSource>();s.playOnAwake=false;s.loop=loop;s.spatialBlend=0;
            s.dopplerLevel=0;s.rolloffMode=AudioRolloffMode.Linear;s.minDistance=6;s.maxDistance=65;
            return s;
        }
        private void Awake()
        {
            Master=Pref("master",_defaultMaster);Music=Pref("music",_defaultMusic);Sfx=Pref("sfx",_defaultSfx);
            Ambience=Pref("ambience",_defaultAmbience);Interface=Pref("interface",_defaultInterface);
            AudioListener.volume=Master;SfxVolume=Sfx;
            _music[0]=_musicSource!=null?_musicSource:Source("Score A",true);
            _music[0].Stop();_music[0].playOnAwake=false;_music[0].loop=true;_music[0].spatialBlend=0;
            _music[1]=Source("Score B",true);_ambience[0]=Source("World A",true);_ambience[1]=Source("World B",true);
            _voices=new Voice[_library!=null?_library.Voices:16];
            for(int i=0;i<_voices.Length;i++)_voices[i]=new Voice{source=Source("Foley "+i,false)};
            if(_library!=null)foreach(var cue in _library.Cues)if(!string.IsNullOrEmpty(cue.id))_cues[cue.id]=cue;
        }
        private void OnEnable()=>SoundSignals.Requested+=Play;
        private void OnDisable()
        {SoundSignals.Requested-=Play;if(_voices!=null)foreach(var v in _voices)v.source.Stop();}
        private void Start()
        {
            if(_theme!=null)return;
            if(_library!=null)SetTheme("menu");
            else if(_backgroundMusic!=null){_music[0].clip=_backgroundMusic;_music[0].Play();}
        }
        private static float Pref(string key,float fallback)=>Mathf.Clamp01(PlayerPrefs.GetFloat("audio."+key,fallback));
        private void Save(string key,float value){PlayerPrefs.SetFloat("audio."+key,value);ApplyVolumes();}
        public void SetMaster(float v){Master=Mathf.Clamp01(v);AudioListener.volume=Master;Save("master",Master);}
        public void SetMusic(float v){Music=Mathf.Clamp01(v);Save("music",Music);}
        public void SetSfx(float v){Sfx=Mathf.Clamp01(v);SfxVolume=Sfx;Save("sfx",Sfx);}
        public void SetAmbience(float v){Ambience=Mathf.Clamp01(v);Save("ambience",Ambience);}
        public void SetInterface(float v){Interface=Mathf.Clamp01(v);Save("interface",Interface);}
        public void SetMenuDucking(bool on){_menu=on;UpdateDuck();}
        public void SetCinematicDucking(bool on){_cinematic=on;UpdateDuck();}
        private void UpdateDuck()=>_duckTarget=_cinematic ? .30f : _menu ? .70f : 1;
        public void SetTheme(string id)
        {
            if(_library==null || id==_theme)return;
            var theme=_library.FindTheme(id)??_library.FindTheme("kok_ahdi_korulari");if(theme==null)return;
            _theme=id;_active=1-_active;_blend=0;
            _music[_active].clip=theme.music;_ambience[_active].clip=theme.ambience;
            _music[_active].volume=0;_ambience[_active].volume=0;
            if(theme.music!=null)_music[_active].Play();else _music[_active].Stop();
            if(theme.ambience!=null)_ambience[_active].Play();else _ambience[_active].Stop();
        }
        private void Update()
        {
            if(_blend>=1 && Mathf.Approximately(_duck,_duckTarget))return;
            _blend=Mathf.MoveTowards(_blend,1,Time.unscaledDeltaTime/(_library!=null?_library.CrossfadeSeconds:2));
            _duck=Mathf.MoveTowards(_duck,_duckTarget,Time.unscaledDeltaTime*1.7f);ApplyVolumes();
            if(_blend>=1){_music[1-_active].Stop();_ambience[1-_active].Stop();}
        }
        private void ApplyVolumes()
        {
            for(int i=0;i<2;i++)
            {
                float fade=i==_active?Mathf.Sin(_blend*Mathf.PI*.5f):Mathf.Cos(_blend*Mathf.PI*.5f);
                if(_music[i]!=null)_music[i].volume=Music*_duck*fade;
                if(_ambience[i]!=null)_ambience[i].volume=Ambience*(_menu ? .7f : 1)*fade;
            }
            if(_voices!=null)foreach(var voice in _voices)
                if(voice.cue!=null)voice.source.volume=voice.gain*(voice.cue.bus==SoundBus.Interface?Interface:Sfx);
        }
        private void Play(SoundSignals.Request request)
        {
            if(_voices==null || !_cues.TryGetValue(request.Id,out var cue) || cue.variants==null || cue.variants.Length==0)return;
            float now=Time.unscaledTime;
            if(_last.TryGetValue(request.Id,out float last) && now-last<cue.cooldown)return;
            Voice selected=null;int count=0;double oldest=double.MaxValue;
            foreach(var v in _voices)
            {
                if(v.source.isPlaying && v.cue==cue)count++;
                if(!v.source.isPlaying && selected==null)selected=v;
            }
            if(count>=cue.voiceLimit)return;
            if(selected==null)foreach(var v in _voices)if(v.started<oldest){oldest=v.started;selected=v;}
            _variant.TryGetValue(request.Id,out int previous);
            int next=(previous+Random.Range(1,Mathf.Max(2,cue.variants.Length)))%cue.variants.Length;
            _variant[request.Id]=next;var clip=cue.variants[next];if(clip==null)return;
            _last[request.Id]=now;selected.source.Stop();selected.cue=cue;selected.gain=cue.volume*request.Gain;
            selected.started=AudioSettings.dspTime;selected.source.clip=clip;
            selected.source.pitch=1+Random.Range(-cue.pitchVariation,cue.pitchVariation);
            selected.source.transform.position=request.Position;
            selected.source.spatialBlend=request.Spatial ? .55f : 0;
            selected.source.volume=selected.gain*(cue.bus==SoundBus.Interface?Interface:Sfx);
            selected.source.Play();PlayedCount++;
        }
    }
}