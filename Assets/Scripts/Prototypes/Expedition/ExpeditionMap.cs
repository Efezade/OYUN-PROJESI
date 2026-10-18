using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Prototypes.Expedition
{
    public enum GroundKind { Grass, Mud, Water, Ramp, High }
    // Three connected radius-seven arenas. Terrain has one shared height/LOS/movement model.
    public sealed class ExpeditionMap
    {
        public static readonly LabHex[] Directions = { new LabHex(1,0), new LabHex(1,-1), new LabHex(0,-1), new LabHex(-1,0), new LabHex(-1,1), new LabHex(0,1) };
        private readonly ExpeditionConfig _config;
        private readonly HashSet<LabHex> _cells = new HashSet<LabHex>();
        private readonly List<LabHex> _ordered = new List<LabHex>();
        private readonly List<Vector2> _cover = new List<Vector2>();
        private readonly Queue<LabHex> _queue = new Queue<LabHex>();
        private readonly Dictionary<LabHex,LabHex> _parents = new Dictionary<LabHex,LabHex>();
        public IReadOnlyList<LabHex> Cells => _ordered;
        private readonly List<LabHex> _platforms=new List<LabHex>();
        public IReadOnlyList<LabHex> Platforms=>_platforms;
        public HybridBattleLayout Layout=>_config.HybridLayout;
        public float EncounterEdge(int stage)=>Layout!=null?Layout.RiseStart(stage):Center(stage).x+10;
        public IReadOnlyList<Vector2> Cover => _cover;
        public ExpeditionMap(ExpeditionConfig config)
        {
            _config = config;
            if(Layout!=null)
            {
                for(int stage=0;stage<3;stage++)
                {
                    int radius=Layout.Radius(stage);
                    for(int q=-radius;q<=radius;q++)for(int r=-radius;r<=radius;r++)
                        if(new LabHex(q,r).Distance(default)<=radius){var h=new LabHex(q+Layout.Spacing*stage,r);Add(h);_platforms.Add(h);}
                    foreach(var offset in Layout.CoverOffsets)_cover.Add(Center(stage)+offset);
                }
                for(int stage=0;stage<2;stage++)for(int q=0;q<=Layout.Spacing;q++)for(int r=-1;r<=1;r++)
                {var h=new LabHex(q+Layout.Spacing*stage,r);if(Layout.OnBridge(h.World(config.Radius),stage))Add(h);}
                return;
            }
            for (int stage=0;stage<3;stage++)
            {
                for(int q=-7;q<=7;q++) for(int r=-7;r<=7;r++)
                    if(new LabHex(q,r).Distance(default)<=7) Add(new LabHex(q+16*stage,r));
                Vector2 c=Center(stage);
                // Interrupted cover lines form a choke and leave two escape routes.
                foreach(var p in new[]{new Vector2(-.7f,-3.5f),new Vector2(-.7f,-2.1f),new Vector2(-.7f,1.3f),new Vector2(-.7f,2.7f),new Vector2(4.8f,-3.8f)}) _cover.Add(c+p);
            }
            for(int s=0;s<2;s++) for(int q=7+16*s;q<=9+16*s;q++) for(int r=-1;r<=1;r++) Add(new LabHex(q,r));
        }
        private void Add(LabHex h) { if(_cells.Add(h)) _ordered.Add(h); }
        public Vector2 Center(int stage) => new LabHex(stage*(Layout!=null?Layout.Spacing:16),0).World(_config.Radius);
        public Vector2 Entrance(int stage) => Center(stage)+new LabHex(-4,0).World(_config.Radius);
        public Vector2 Exit(int stage) => Center(stage)+new LabHex(6,0).World(_config.Radius);
        public int Region(Vector2 p) => Mathf.Clamp(Mathf.RoundToInt(p.x / Center(1).x),0,2);
        public LabHex Hex(Vector2 p)
        {
            float r=p.y/(_config.Radius*1.5f),q=p.x/(Mathf.Sqrt(3)*_config.Radius)-r*.5f;
            float x=q,z=r,y=-x-z; int rx=Mathf.RoundToInt(x),ry=Mathf.RoundToInt(y),rz=Mathf.RoundToInt(z);
            float dx=Mathf.Abs(rx-x),dy=Mathf.Abs(ry-y),dz=Mathf.Abs(rz-z);
            if(dx>dy&&dx>dz)rx=-ry-rz;else if(dy<=dz)rz=-rx-ry;
            return new LabHex(rx,rz);
        }
        public bool Contains(Vector2 p)
        {
            if(Layout==null)return _cells.Contains(Hex(p));
            for(int i=0;i<2;i++)if(Layout.OnBridge(p,i))return true;
            var h=Hex(p);int stage=Layout.Region(p);
            return new LabHex(h.Q-stage*Layout.Spacing,h.R).Distance(default)<=Layout.Radius(stage);
        }
        public float Height(Vector2 p)
        {
            if(Layout!=null)return Layout.Height(p);
            var v=p-Center(Region(p));
            float ramp=Mathf.Clamp01((v.x-1f)/3.2f);
            float edge=Mathf.Min(Mathf.Clamp01((7.2f-v.x)/.5f),Mathf.Min(Mathf.Clamp01((v.y-1.2f)/.5f),Mathf.Clamp01((6.8f-v.y)/.5f)));
            return .3f+_config.Height*Mathf.Min(ramp,edge);
        }
        public GroundKind Ground(Vector2 p)
        {
            if(Layout!=null)return Layout.IsSlope(p)?GroundKind.Ramp:Layout.Region(p)>0?GroundKind.High:GroundKind.Grass;
            float h=Height(p)-.3f;if(h>_config.Height*.85f)return GroundKind.High;if(h>.08f)return GroundKind.Ramp;
            var v=p-Center(Region(p));
            if(v.x>1&&v.x<5&&v.y>-3.5f&&v.y<.2f)return GroundKind.Water;
            if(v.x>-5&&v.x<-1.7f&&v.y>1&&v.y<4.6f)return GroundKind.Mud;
            return GroundKind.Grass;
        }
        public bool Walkable(Vector2 p)
        {
            if(!Contains(p)||Layout!=null&&Layout.GateBlocks(p))return false;
            foreach(var c in _cover)if((p-c).sqrMagnitude<.68f*.68f)return false;
            return true;
        }
        public bool CanStep(Vector2 a,Vector2 b)
        {
            if(!Walkable(b))return false;
            float distance=Vector2.Distance(a,b);
            int count=Mathf.Max(1,Mathf.CeilToInt(distance/.12f));var last=a;
            for(int i=1;i<=count;i++) {var p=Vector2.Lerp(a,b,(float)i/count);if(!Walkable(p)||Mathf.Abs(Height(p)-Height(last))>Vector2.Distance(p,last)*.9f+.005f)return false;last=p;}
            return true;
        }
        public bool HasLine(Vector2 a,Vector2 b)
        {
            var delta=b-a;float distance=delta.magnitude;
            if(Layout!=null)for(float t=0;t<=distance;t+=.1f)if(Layout.GateBlocks(a+delta*(t/Mathf.Max(.001f,distance))))return false;
            float ah=Height(a)+.85f,bh=Height(b)+.85f;
            foreach(var c in _cover)
            {
                float t=Mathf.Clamp01(Vector2.Dot(c-a,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                if(Vector2.Distance(c,a+delta*t)<.65f&&Mathf.Lerp(ah,bh,t)<Height(c)+1.65f)return false;
            }
            int n=Mathf.CeilToInt(distance/.4f);
            for(int i=1;i<n;i++) {float t=(float)i/n;if(Height(a+delta*t)>.15f+Mathf.Lerp(ah,bh,t))return false;}
            return true;
        }
        public Vector2 NextStep(Vector2 from,Vector2 goal)
        {
            if(CanStep(from,goal))return goal;
            var start=Hex(from);var end=Hex(goal);_queue.Clear();_parents.Clear();_queue.Enqueue(start);_parents[start]=start;
            LabHex best=start;float score=(start.World(_config.Radius)-goal).sqrMagnitude;
            while(_queue.Count>0)
            {
                var at=_queue.Dequeue();float d=(at.World(_config.Radius)-goal).sqrMagnitude;if(d<score){best=at;score=d;}
                if(at.Equals(end)){best=at;break;}
                foreach(var dir in Directions)
                {
                    var to=at+dir;var origin=at.Equals(start)?from:at.World(_config.Radius);
                    if(!_cells.Contains(to)||_parents.ContainsKey(to)||!CanStep(origin,to.World(_config.Radius)))continue;
                    _parents[to]=at;_queue.Enqueue(to);
                }
            }
            if(best.Equals(start)) return from;
            while(!_parents[best].Equals(start))best=_parents[best];
            return best.World(_config.Radius);
        }
        public string Description(Vector2 p) => Layout!=null?(Layout.IsSlope(p)?"KÖPRÜ RAMPASI · Kenarlar geçilemez; yükseltiye bağlı geçiş":Layout.Region(p)>0?"YÜKSEK TERAS · Menzil avantajı; sütunlar görüşü keser":"TAŞ AVLU · Kapı ayakları ve sütunlar geçilemez"):Ground(p) switch {
            GroundKind.Mud=>"ÇAMUR · herkes yavaşlar; düşmanı burada tut",
            GroundKind.Water=>"SU · iletim gücünü besler; nişancı elektriği +2 hasar",
            GroundKind.Ramp=>"RAMPA · yükseltiye güvenli çıkış",
            GroundKind.High=>"YÜKSELTİ · menzil +1,4; düşman da rampayı kullanır",
            _=>"ÇAYIR · normal hareket; sütunlar görüşü keser"};
    }
}
