using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Same five original textures and timing as slam-peak-review.html v21.
    // Local X runs from the far endpoint back toward the sword; local Y always stays upright.
    public sealed class LayeredSlamEffect : MonoBehaviour
    {
        public const float PixelsToWorld = 0.02f;
        public const float GroundSeconds = 0.58f;
        public const float DebrisSeconds = 0.90f;
        public const float Duration = SlamLightEffect.Duration;
        public const string MainArtwork = "slam-main-v21";
        public const string MiddleArtwork = "slam-middle-v21";
        public const string EndArtwork = "slam-end-v21";
        public const string GroundArtwork = "slam-impact-v21";
        public const string DebrisArtwork = "slam-debris-v21";
        private enum Kind { Main, Middle, End, Ground, Debris }
        private sealed class Piece
        {
            public Kind kind;
            public Transform transform;
            public Mesh mesh;
            public MeshRenderer renderer;
            public MaterialPropertyBlock properties;
            public float x, y, width, height, delay;
            public int index, count;
        }
        private readonly List<Piece> pieces = new();
        private Transform source;
        private Health sourceHealth;
        private Vector3 previousSourcePosition;
        private float age, length, sign;
        private Rect room;
        private Renderer sortingSource;
        private Material material;
        private static readonly Rect[] DebrisCrops =
        {
            new Rect(203,218,331,336), new Rect(787,274,252,244),
            new Rect(257,806,217,237), new Rect(791,744,281,304)
        };

        public static GameObject Create(Vector2 center, float length, float height, float angle,
            Transform source, Renderer sortingSource, Rect room, bool reverse)
        {
            if (!Application.isPlaying || source == null || length <= 0.001f || height <= 0f) return null;
            Material material = CombatEffectArtwork.Material("LayeredSlam");
            if (material == null) return null;
            foreach (string name in new[] { MainArtwork, MiddleArtwork, EndArtwork, GroundArtwork, DebrisArtwork })
                if (Resources.Load<Texture2D>("CombatEffects/" + name) == null) return null;
            var owner = new GameObject("VFX layered slam v21");
            owner.transform.SetPositionAndRotation(center, Quaternion.Euler(0,0,angle));
            var effect = owner.AddComponent<LayeredSlamEffect>();
            effect.source = source;
            effect.sourceHealth = source.GetComponent<Health>();
            effect.previousSourcePosition = source.position;
            effect.length = length;
            effect.sign = reverse ? -1f : 1f;
            effect.room = room;
            effect.sortingSource = sortingSource;
            effect.material = material;
            effect.Build(height);
            effect.RenderAt(0f);
            return owner;
        }

        public static int MiddleCount(float length, float mainWidth) =>
            Mathf.Max(0, Mathf.CeilToInt((length-mainWidth*0.7f-4.8f*0.7f)/3.9f));

        private void Build(float height)
        {
            float mainWidth = height; // Original main is square; never stretch it along the path.
            float visibleWidth = Mathf.Min(mainWidth,length);
            var mainUV = new Vector4(1f-visibleWidth/mainWidth,0,visibleWidth/mainWidth,1);
            // Extremely short paths use a right-hand artwork window, preserving height and texel proportions.
            Add(Kind.Main,MainArtwork,0,0,visibleWidth,height,1120f/1254f,0,mainUV,
                visibleWidth<mainWidth ? Mathf.Min(0.05f,0.08f/visibleWidth) : 0f);
            int middleCount = MiddleCount(length,mainWidth);
            for (int i=0;i<middleCount;i++)
            {
                float width=6f*(i==0?1f:0.85f);
                float first=Mathf.Min(mainWidth*0.5f,Mathf.Max(0,length-width));
                float last=Mathf.Max(first,length-width-1f);
                float x=middleCount==1?first:Mathf.Lerp(first,last,i/(float)(middleCount-1));
                Add(Kind.Middle,MiddleArtwork,x,0,width,width*0.5f,760f/887f,
                    Mathf.Min(0.07f,0.018f*(i+1)));
            }
            float endWidth=Mathf.Min(4.8f,length);
            Add(Kind.End,EndArtwork,length-endWidth,0,endWidth,endWidth/3f,574f/724f,0.04f);

            float groundWidth=Mathf.Min(4.8f,Mathf.Max(0.02f,(length-0.04f)/1.08f));
            int groundCount=Mathf.Max(2,Mathf.CeilToInt(length/(groundWidth*0.78f)));
            float firstCenter=groundWidth*1.08f*0.5f+0.02f;
            for (int i=0;i<groundCount;i++)
            {
                float center=Mathf.Lerp(firstCenter,length-firstCenter,i/(float)(groundCount-1));
                Piece part=Add(Kind.Ground,GroundArtwork,center,-0.16f,groundWidth,
                    groundWidth/3f,0.5f,0.075f*(groundCount-1-i)/Mathf.Max(1,groundCount-1));
                part.index=i;part.count=groundCount;
            }
            int debrisCount=Mathf.Clamp(Mathf.RoundToInt(length),1,128);
            for(int i=0;i<debrisCount;i++)
            {
                Rect crop=DebrisCrops[i%4];
                Texture2D texture=Resources.Load<Texture2D>("CombatEffects/"+DebrisArtwork);
                var uv=new Vector4(crop.x/texture.width,1f-(crop.y+crop.height)/texture.height,
                    crop.width/texture.width,crop.height/texture.height);
                float width=(8f+Seed(i+6)*8f)*PixelsToWorld;
                Piece part=Add(Kind.Debris,DebrisArtwork,0,0,width,width*crop.height/crop.width,
                    0.5f,0.075f*(1f-i/(float)Mathf.Max(1,debrisCount-1)),uv);
                part.index=i;part.count=debrisCount;
            }
        }

        private Piece Add(Kind kind,string artwork,float x,float y,float width,float height,
            float baseline,float delay,Vector4? uv=null,float cropFade=0)
        {
            var child=new GameObject("Slam "+kind);
            child.transform.SetParent(transform,false);
            var mesh=new Mesh {name="Reviewed slam "+kind};
            mesh.vertices=new[] {new Vector3(0,-height*(1-baseline)),new Vector3(sign*width,-height*(1-baseline)),
                new Vector3(0,height*baseline),new Vector3(sign*width,height*baseline)};
            mesh.uv=new[] {Vector2.zero,Vector2.right,Vector2.up,Vector2.one};
            mesh.triangles=new[] {0,2,1,2,3,1};mesh.RecalculateBounds();
            child.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.sortingLayerID=sortingSource!=null?sortingSource.sortingLayerID:0;
            renderer.sortingOrder=(sortingSource!=null?sortingSource.sortingOrder:0)+
                (kind==Kind.Ground?-1:kind==Kind.Debris?3:1);
            var properties=new MaterialPropertyBlock();
            properties.SetTexture("_MainTex",Resources.Load<Texture2D>("CombatEffects/"+artwork));
            properties.SetVector("_UVRect",uv??new Vector4(0,0,1,1));
            properties.SetVector("_ClipRect",new Vector4(room.xMin,room.yMin,room.xMax,room.yMax));
            properties.SetFloat("_CropFade",cropFade);
            var part=new Piece {kind=kind,transform=child.transform,mesh=mesh,renderer=renderer,
                properties=properties,x=x,y=y,width=width,height=height,delay=delay};
            pieces.Add(part);
            return part;
        }

        public static float Smooth(float value)
        {
            value=Mathf.Clamp01(value);return value*value*(3f-2f*value);
        }
        public static float GroundScaleAt(float seconds) => 0.40f+0.68f*Smooth(seconds/0.30f);
        public static float GroundOpacityAt(float seconds) => seconds<0||seconds>=GroundSeconds?0:
            Smooth(seconds/0.035f)*(1-Smooth((seconds-0.12f)/0.44f))*0.55f;
        private static float Seed(int n) => Mathf.Repeat(Mathf.Sin(n*127.1f+31.7f)*43758.5453f,1f);

        public void RenderAt(float seconds)
        {
            foreach(Piece part in pieces)
            {
                float x=part.x,y=part.y,opacity=0,reveal=1,scale=1,rotation=0;
                if(part.kind==Kind.Ground)
                {
                    float t=seconds-SlamLightEffect.FallSeconds-part.delay;
                    scale=GroundScaleAt(t);
                    opacity=GroundOpacityAt(t);
                    x-=part.width*scale*0.5f;
                }
                else if(part.kind==Kind.Debris)
                {
                    float t=seconds-SlamLightEffect.FallSeconds-part.delay;
                    int i=part.index;
                    float life=0.62f+Seed(i+16)*0.16f;
                    float lift=135f+Seed(i+4)*100f;
                    x=0.56f+(length-1.12f)*(i+0.35f)/part.count+(Seed(i+2)-0.5f)*110f*PixelsToWorld*t;
                    x=Mathf.Clamp(x,0,length);
                    y=-0.12f-(Seed(i+8)-0.5f)*0.6f+Mathf.Max(0,lift*t-330*t*t)*PixelsToWorld;
                    opacity=t<0||t>=life?0:Smooth(t/0.025f)*(1-Smooth((t-life*0.58f)/(life*0.42f)))*
                        (1-Smooth((t-lift/330f)/0.10f));
                    rotation=((Seed(i+10)-0.5f)*2f+t*(Seed(i+12)-0.5f)*7f)*Mathf.Rad2Deg;
                    x-=part.width*0.5f;
                }
                else
                {
                    reveal=Smooth((seconds-part.delay)/(SlamLightEffect.FallSeconds-part.delay));
                    float fadeStart=SlamLightEffect.FallSeconds+SlamLightEffect.PeakHoldSeconds+part.delay;
                    opacity=seconds<part.delay||seconds>=Duration?0:
                        Mathf.Min(1,reveal*2)*(1-Smooth((seconds-fadeStart)/(SlamLightEffect.FadeSeconds-part.delay)));
                    y+=0.56f*(1-reveal);
                }
                part.transform.localPosition=new Vector3(sign*(x-length*0.5f),y,0);
                part.transform.localScale=new Vector3(scale,scale,1);
                part.transform.localRotation=Quaternion.Euler(0,0,sign*rotation);
                part.renderer.enabled=opacity>0.001f;
                part.properties.SetFloat("_Opacity",opacity);
                part.properties.SetFloat("_Reveal",reveal);
                part.renderer.SetPropertyBlock(part.properties);
            }
        }
        private void Update()
        {
            if(source==null||!source.gameObject.activeInHierarchy||(sourceHealth!=null&&sourceHealth.IsDead)||
                (source.position-previousSourcePosition).sqrMagnitude>16f)
            {Destroy(gameObject);return;}
            previousSourcePosition=source.position;
            age+=Time.deltaTime;
            if(age>=Duration){Destroy(gameObject);return;}
            RenderAt(age);
        }
        private void OnDestroy()
        {
            foreach(Piece part in pieces)CombatEffectArtwork.Release(part.mesh);
        }
    }
}
