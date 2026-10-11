using System.Collections.Generic;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>A pooled replacement tile: six lifted banks, a sunken soil bed and ballistic clods.
    /// One mesh/renderer per tile, no physics bodies and no per-frame allocations.</summary>
    public sealed class RootRuptureTile : MonoBehaviour
    {
        private struct Chunk
        {
            public int First, Count;
            public Vector3 Pivot, Push;
            public float Seed;
            public bool Debris;
        }
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Vector3[] _rest, _vertices;
        private Chunk[] _chunks;
        private QuestRootConfigSO _config;
        private ParticleSystem _dust;
        private HexCell _cell;
        private float _age, _lastAmount = -1, _size;
        private bool _settled, _started;
        public float Amount { get; private set; }
        public HexCoordinate Coordinate => _cell != null ? _cell.Coordinate : default;

        public void Configure(HexCell cell, Vector3 direction, float size, QuestRootConfigSO config, Color ground)
        {
            Release();
            _config = config; _size = size; _cell = cell;
            transform.SetPositionAndRotation(cell.WorldPosition + Vector3.up * cell.SurfaceHeight, Quaternion.identity);
            transform.localScale = Vector3.one;
            _age = 0; _started = _settled = false; _lastAmount = -1;
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Fractured hex banks and soil" }; _mesh.MarkDynamic();
                gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
                _renderer = gameObject.AddComponent<MeshRenderer>();
            }
            _renderer.sharedMaterial = config.EarthMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", ground);
            block.SetColor("_SoilColor", Color.Lerp(ground, config.EarthTint, .65f));
            _renderer.SetPropertyBlock(block);
            if (_dust == null && config.RuptureDustPrefab != null)
                _dust = Instantiate(config.RuptureDustPrefab, transform);
            if (_dust != null) _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Build(direction, cell.Coordinate.VariantHash());
            Show(0, false, 0);
        }

        public void Bind(HexCell cell)
        {
            if (_cell == cell) return;
            if (_cell != null) _cell.SetVisualSuppressed(this, false);
            _cell = cell;
            if (_cell != null) _cell.SetVisualSuppressed(this, Amount > 0);
        }

        public void Agitate(bool visible)
        {
            _age = 0; _settled = false;
            if (visible && _dust != null) _dust.Play(true);
        }

        public void Show(float amount, bool visible, float delta)
        {
            Amount = amount;
            if (_renderer == null) return;
            bool active = amount > .001f;
            _renderer.enabled = active && visible;
            _cell?.SetVisualSuppressed(this, active);
            if (active && !_started)
            {
                _started = true;
                if (visible && _dust != null) _dust.Play(true);
            }
            if (!visible && _dust != null && _dust.isPlaying)
                _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _age += delta;
            bool moving = _age < _config.SettleSeconds;
            if (Mathf.Approximately(amount, _lastAmount) && _settled) return;
            _lastAmount = amount; _settled = !moving;
            float open = Mathf.SmoothStep(0, 1, Mathf.Clamp01(amount * 3));
            // Retraction heals the fracture only as that particular tile's root disappears.
            float life = Mathf.Clamp01(_age / _config.SettleSeconds);
            foreach (var c in _chunks)
            {
                float burst = Mathf.Sin(life * Mathf.PI) * (1 - life);
                Vector3 offset = c.Push * open * (c.Debris ? .17f + life * .32f : .085f);
                offset.y = open * (c.Debris ? .035f + 4 * life * (1-life) * _config.EruptionHeight * (.65f+c.Seed*.65f)
                    : _config.BankLift * (.75f+c.Seed*.5f) + burst * _config.EruptionHeight * .6f);
                var rotation = Quaternion.AngleAxis(open * (c.Debris ? 35 + life * 240 : 12 + burst * 20) * (c.Seed+.6f),
                    Vector3.Cross(c.Push, Vector3.up).normalized);
                for (int i = c.First; i < c.First+c.Count; i++)
                    _vertices[i] = c.Pivot + rotation * (_rest[i]-c.Pivot) + offset;
            }
            _mesh.vertices = _vertices; _mesh.RecalculateNormals(); _mesh.RecalculateBounds();
        }

        public void Release()
        {
            if (_cell != null) _cell.SetVisualSuppressed(this, false);
            _cell = null; Amount = 0;
            if (_dust != null) _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private void OnDisable() => Release();
        private void OnDestroy() { if (_mesh != null) Destroy(_mesh); }

        private void Build(Vector3 direction, int seed)
        {
            direction.y = 0; direction.Normalize();
            if (direction.sqrMagnitude < .5f) direction = Vector3.forward;
            Vector3 cross = Vector3.Cross(direction, Vector3.up);
            var vertices = new List<Vector3>(); var colors = new List<Color>();
            var indices = new List<int>(); var chunks = new List<Chunk>();
            var hex = new List<Vector3>(); foreach (var corner in HexMetrics.Corners) hex.Add(corner * _size * .98f);
            // Intact earth below the trench occludes submerged roots; no black hole under a root.
            Prism(hex, -.30f, -.82f, 0, vertices, colors, indices);
            var random = new System.Random(seed);
            for (int sign = -1; sign <= 1; sign += 2)
                for (int strip = 0; strip < 3; strip++)
                {
                    float noise = (float)random.NextDouble();
                    var polygon = Clip(hex, cross * sign, _config.TrenchHalfWidth * _size * (.85f+noise*.22f));
                    polygon = Clip(polygon, direction, (-1.25f + strip * .83f) * _size + .018f);
                    polygon = Clip(polygon, -direction, -(-1.25f + (strip+1)*.83f) * _size + .018f);
                    if (polygon.Count < 3) continue;
                    int first = vertices.Count;
                    Prism(polygon, 0, -.38f, 1, vertices, colors, indices);
                    var pivot = Vector3.zero; foreach (var p in polygon) pivot += p; pivot /= polygon.Count;
                    chunks.Add(new Chunk { First=first, Count=vertices.Count-first, Pivot=pivot,
                        Push=cross*sign, Seed=noise });
                }
            for (int n = 0; n < 10; n++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2;
                Vector3 push = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 pivot = cross * (n%2==0 ? -1 : 1) * _config.TrenchHalfWidth * _size + direction * ((float)random.NextDouble()-.5f) * _size*1.5f;
                float r = (.06f + (float)random.NextDouble()*.11f) * _size;
                var polygon = new List<Vector3>();
                for (int p=0;p<5;p++) { float angle=p*Mathf.PI*.4f;polygon.Add(pivot+new Vector3(Mathf.Cos(angle)*r,0,Mathf.Sin(angle)*r)); }
                int first=vertices.Count;Prism(polygon,r*.7f,-r*.4f,0,vertices,colors,indices);
                chunks.Add(new Chunk {First=first,Count=vertices.Count-first,Pivot=pivot,Push=push,Seed=(float)random.NextDouble(),Debris=true});
            }
            _chunks=chunks.ToArray();_rest=vertices.ToArray();_vertices=(Vector3[])_rest.Clone();
            _mesh.Clear();_mesh.vertices=_vertices;_mesh.SetColors(colors);_mesh.SetTriangles(indices,0);_mesh.RecalculateNormals();_mesh.RecalculateBounds();
        }

        private static List<Vector3> Clip(List<Vector3> polygon, Vector3 normal, float distance)
        {
            var result=new List<Vector3>();if(polygon.Count==0)return result;
            Vector3 previous=polygon[polygon.Count-1];float before=Vector3.Dot(previous,normal)-distance;
            foreach(var current in polygon)
            {
                float after=Vector3.Dot(current,normal)-distance;
                if((before>=0)!=(after>=0))result.Add(Vector3.Lerp(previous,current,before/(before-after)));
                if(after>=0)result.Add(current);previous=current;before=after;
            }
            return result;
        }
        private static void Prism(List<Vector3> polygon,float top,float bottom,float turf,List<Vector3> v,List<Color> c,List<int> tris)
        {
            for(int i=1;i<polygon.Count-1;i++)
                Triangle(polygon[0]+Vector3.up*top,polygon[i+1]+Vector3.up*top,polygon[i]+Vector3.up*top,new Color(turf,1,0,1),v,c,tris);
            for(int i=0;i<polygon.Count;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Count];var colour=new Color(0,.84f,0,1);
                Triangle(a+Vector3.up*top,b+Vector3.up*top,b+Vector3.up*bottom,colour,v,c,tris);
                Triangle(a+Vector3.up*top,b+Vector3.up*bottom,a+Vector3.up*bottom,colour,v,c,tris);
            }
        }
        private static void Triangle(Vector3 a,Vector3 b,Vector3 d,Color color,List<Vector3> v,List<Color> c,List<int> tris)
        {int start=v.Count;v.Add(a);v.Add(b);v.Add(d);for(int n=0;n<3;n++){c.Add(color);tris.Add(start+n);}}
    }
}
