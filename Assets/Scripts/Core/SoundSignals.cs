using System;
using UnityEngine;
namespace TacticalRPG.Core
{
    /// <summary>Presentation event channel; gameplay never depends on an audio player.</summary>
    public static class SoundSignals
    {
        public readonly struct Request
        {
            public readonly string Id;
            public readonly Vector3 Position;
            public readonly bool Spatial;
            public readonly float Gain;
            public Request(string id,Vector3 position,bool spatial,float gain)
            {Id=id;Position=position;Spatial=spatial;Gain=gain;}
        }
        public static event Action<Request> Requested;
        public static void Play(string id,float gain=1)=>Requested?.Invoke(new Request(id,default,false,gain));
        public static void At(string id,Vector3 position,float gain=1)=>Requested?.Invoke(new Request(id,position,true,gain));
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()=>Requested=null;
    }
}
