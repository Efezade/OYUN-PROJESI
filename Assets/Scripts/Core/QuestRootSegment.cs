using UnityEngine;
namespace TacticalRPG.Core
{
    /// <summary>Pooled buttress root: three twisted strands extrude toward their growing tip.</summary>
    public sealed class QuestRootSegment : MonoBehaviour
    {
        private const int Rings=25,Sides=14,Strands=3;
        private Mesh _mesh; private MeshRenderer _renderer;
        private Vector3[] _vertices,_normals;
        private Vector3 _start,_end,_a,_b; private float _radius,_endRadius,_amount=-1; private int _seed;
        private float _phase,_phaseSpan,_amplitude,_burrowOffset;
        public float GrowthAmount=>_amount;
        public Vector3 Tip=>Curve(Mathf.Clamp01(_amount));
        public void Build(Vector3 start,Vector3 end,Vector3 incoming,Vector3 outgoing,float radius,Material material,int seed,float endRadius=-1,
            float phase=0,float phaseSpan=0,float amplitude=0,float burrowOffset=0)
        {
            if(_mesh==null)
            {
                _mesh=new Mesh{name="Braided Bayterek buttress"};_mesh.MarkDynamic();
                gameObject.AddComponent<MeshFilter>().sharedMesh=_mesh;_renderer=gameObject.AddComponent<MeshRenderer>();
                _vertices=new Vector3[Rings*(Sides+1)*Strands];_normals=new Vector3[_vertices.Length];
                var uv=new Vector2[_vertices.Length];var triangles=new int[Strands*(Rings-1)*Sides*6];int k=0;
                for(int strand=0;strand<Strands;strand++)for(int r=0;r<Rings;r++)for(int s=0;s<=Sides;s++)
                {
                    int i=strand*Rings*(Sides+1)+r*(Sides+1)+s;uv[i]=new Vector2(s/(float)Sides,r/(float)(Rings-1));
                    if(r==Rings-1||s==Sides)continue;
                    triangles[k++]=i;triangles[k++]=i+Sides+1;triangles[k++]=i+1;
                    triangles[k++]=i+1;triangles[k++]=i+Sides+1;triangles[k++]=i+Sides+2;
                }
                _mesh.vertices=_vertices;_mesh.uv=uv;_mesh.triangles=triangles;
            }
            transform.position=start;transform.rotation=Quaternion.identity;transform.localScale=Vector3.one;
            _start=start;_end=end;float handle=Vector3.Distance(start,end)/3;
            _a=start+incoming.normalized*handle;_b=end-outgoing.normalized*handle;
            _radius=radius;_endRadius=endRadius<0?radius:endRadius;_seed=seed;_renderer.sharedMaterial=material;_amount=-1;
            _phase=phase;_phaseSpan=phaseSpan;_amplitude=amplitude;_burrowOffset=burrowOffset;
            Show(0,false);
        }
        public Vector3 CenterAt(float t) => Curve(Mathf.Clamp01(t));
        private Vector3 Curve(float t)
        {
            float u=1-t;
            Vector3 p=u*u*u*_start+3*u*u*t*_a+3*u*t*t*_b+t*t*t*_end;
            float depth=_burrowOffset+Mathf.Sin(_phase+t*_phaseSpan)*_amplitude;
            if(_amplitude>0 && _endRadius<.2f)depth=Mathf.Lerp(depth,-Mathf.Max(.7f,_radius),Mathf.SmoothStep(.55f,1,t));
            p.y+=depth;
            return p;
        }
        public void Show(float amount,bool visible)
        {
            if(_renderer==null)return;_renderer.enabled=visible&&amount>.001f;
            if(Mathf.Approximately(_amount,amount))return;_amount=amount;
            for(int strand=0;strand<Strands;strand++)for(int r=0;r<Rings;r++)
            {
                float original=r/(float)(Rings-1),t=original*amount;
                Vector3 p=Curve(t),tangent=(Curve(Mathf.Min(1,t+.01f))-Curve(Mathf.Max(0,t-.01f))).normalized;
                Vector3 side=Vector3.Cross(tangent,Vector3.up).normalized,up=Vector3.Cross(side,tangent).normalized;
                float twist=t*3.8f+strand*2.0944f;
                // End rings meet exactly. Secondary strands merge into the trunk at each seam.
                float braid=Mathf.Sin(t*Mathf.PI)*_radius*.4f;
                Vector3 offset=strand==0?Vector3.zero:(side*Mathf.Cos(twist)+up*Mathf.Sin(twist)*.55f)*braid;
                float tip=amount<.999f?Mathf.Lerp(1,.015f,Mathf.SmoothStep(.65f,1,original)):1;
                float thickness=Mathf.Lerp(_radius,_endRadius,t)*(strand==0?.9f:.43f)*tip;
                for(int s=0;s<=Sides;s++)
                {
                    float a=s*Mathf.PI*2/Sides;
                    float ridge=1+.19f*Mathf.Cos(a*5+t*5)*Mathf.Sin(t*Mathf.PI);
                    var normal=side*Mathf.Cos(a)+up*Mathf.Sin(a);
                    int i=strand*Rings*(Sides+1)+r*(Sides+1)+s;
                    _vertices[i]=p-_start+offset+thickness*ridge*(side*Mathf.Cos(a)+up*Mathf.Sin(a)*.74f);
                    _normals[i]=normal;
                }
            }
            _mesh.vertices=_vertices;_mesh.normals=_normals;_mesh.RecalculateBounds();
        }
        private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);}
    }
}
