using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // Original vector artwork drawn directly into uGUI. No downloaded artwork or textures.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PrototypeUiIcon : MaskableGraphic
    {
        // The first six values are serialized in existing scenes; keep them stable.
        public enum Symbol
        {
            Leaf = 0, Journal = 1, Check = 2, Arrow = 3, Gear = 4, Lock = 5,
            Pot = 6, Nutrient = 7, Flower = 8, Bell = 9, Music = 10, Heart = 11,
            ZoomIn = 12, ZoomOut = 13, Garden = 14, Seed = 15, BookMark = 16, Sparkle = 17
        }
        public Symbol symbol;
        private Rect iconRect;
        private static readonly Color Ink = new Color(.22f, .32f, .24f);
        private static readonly Color Sage = new Color(.49f, .65f, .38f);
        private static readonly Color LightLeaf = new Color(.76f, .83f, .49f);
        private static readonly Color Paper = new Color(1f, .94f, .77f);
        private static readonly Color Wood = new Color(.72f, .48f, .27f);
        private static readonly Color Clay = new Color(.84f, .47f, .32f);
        private static readonly Color Gold = new Color(.94f, .72f, .34f);

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            float size = Mathf.Min(bounds.width, bounds.height);
            iconRect = new Rect(bounds.center - Vector2.one * (size * .5f), Vector2.one * size);
            if (size <= 0) return;
            switch (symbol)
            {
                case Symbol.Leaf:
                    DrawLeaf(mesh, new Vector2(.23f, .21f), new Vector2(.82f, .83f), .25f, Sage);
                    Stroke(mesh, .035f, Ink, new Vector2(.18f, .14f), new Vector2(.61f, .63f));
                    break;
                case Symbol.Journal:
                    Box(mesh, .19f, .12f, .64f, .76f, .08f, Ink);
                    Box(mesh, .23f, .16f, .55f, .66f, .05f, Wood);
                    Box(mesh, .33f, .20f, .43f, .57f, .025f, Paper);
                    Stroke(mesh, .023f, Ink, new Vector2(.32f, .19f), new Vector2(.32f, .81f));
                    DrawLeaf(mesh, new Vector2(.45f, .40f), new Vector2(.67f, .65f), .095f, Sage);
                    Stroke(mesh, .018f, Wood, new Vector2(.44f, .31f), new Vector2(.66f, .31f));
                    Poly(mesh, Clay, new Vector2(.64f,.86f),new Vector2(.73f,.86f),new Vector2(.73f,.68f),new Vector2(.685f,.72f),new Vector2(.64f,.68f));
                    break;
                case Symbol.Check:
                    Stroke(mesh, .075f, Ink, new Vector2(.18f, .49f), new Vector2(.42f, .26f), new Vector2(.83f, .75f));
                    Stroke(mesh, .041f, LightLeaf, new Vector2(.18f, .50f), new Vector2(.42f, .29f), new Vector2(.82f, .76f));
                    break;
                case Symbol.Arrow:
                    Stroke(mesh, .065f, Ink, new Vector2(.18f, .50f), new Vector2(.77f, .50f));
                    Stroke(mesh, .065f, Ink, new Vector2(.54f, .76f), new Vector2(.82f, .50f), new Vector2(.54f, .24f));
                    Stroke(mesh, .031f, Paper, new Vector2(.18f, .51f), new Vector2(.77f, .51f));
                    Stroke(mesh, .031f, Paper, new Vector2(.54f, .76f), new Vector2(.81f, .51f), new Vector2(.54f, .25f));
                    break;
                case Symbol.Lock:
                    Disc(mesh, new Vector2(.5f,.66f), new Vector2(.23f,.23f), Ink);
                    Disc(mesh, new Vector2(.5f,.66f), new Vector2(.145f,.145f), Paper);
                    Box(mesh, .21f,.14f,.58f,.48f,.07f,Ink);
                    Box(mesh, .25f,.18f,.50f,.40f,.04f,Gold);
                    Disc(mesh, new Vector2(.5f,.40f),Vector2.one*.065f,Ink);
                    Stroke(mesh,.035f,Ink,new Vector2(.5f,.38f),new Vector2(.5f,.29f));
                    break;
                case Symbol.Gear:
                    for (int i=0; i<8; i++)
                    {
                        float angle=i*Mathf.PI/4f;
                        Vector2 direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                        Stroke(mesh,.075f,Ink,Vector2.one*.5f+direction*.23f,Vector2.one*.5f+direction*.35f);
                        Stroke(mesh,.042f,Wood,Vector2.one*.5f+direction*.24f,Vector2.one*.5f+direction*.35f);
                    }
                    Disc(mesh,Vector2.one*.5f,Vector2.one*.29f,Ink);
                    Disc(mesh,Vector2.one*.5f,Vector2.one*.24f,Wood);
                    Disc(mesh,Vector2.one*.5f,Vector2.one*.12f,Ink);
                    Disc(mesh,Vector2.one*.5f,Vector2.one*.07f,Paper);
                    break;
                case Symbol.Pot:
                case Symbol.Garden:
                    Stroke(mesh,.025f,Ink,new Vector2(.5f,.41f),new Vector2(.5f,.75f));
                    DrawLeaf(mesh,new Vector2(.49f,.60f),new Vector2(.21f,.83f),.12f,Sage);
                    DrawLeaf(mesh,new Vector2(.50f,.68f),new Vector2(.78f,.88f),.12f,LightLeaf);
                    DrawPot(mesh);
                    break;
                case Symbol.Nutrient:
                    Box(mesh,.37f,.73f,.26f,.14f,.025f,Ink);
                    Box(mesh,.40f,.77f,.20f,.07f,.01f,Wood);
                    Box(mesh,.24f,.13f,.52f,.63f,.12f,Ink);
                    Box(mesh,.28f,.17f,.44f,.55f,.09f,Sage);
                    Box(mesh,.30f,.28f,.40f,.31f,.035f,Paper);
                    DrawLeaf(mesh,new Vector2(.40f,.33f),new Vector2(.62f,.54f),.08f,Sage);
                    Stroke(mesh,.018f,Paper,new Vector2(.33f,.62f),new Vector2(.37f,.68f));
                    break;
                case Symbol.Flower:
                    Stroke(mesh,.03f,Ink,new Vector2(.5f,.13f),new Vector2(.5f,.63f));
                    DrawLeaf(mesh,new Vector2(.49f,.25f),new Vector2(.75f,.44f),.09f,Sage);
                    for(int i=0;i<6;i++)
                    {
                        float angle=i*Mathf.PI/3f;
                        Vector2 center=new Vector2(.5f,.66f)+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.16f;
                        Disc(mesh,center,Vector2.one*.14f,Ink);
                        Disc(mesh,center,Vector2.one*.105f,Paper);
                    }
                    Disc(mesh,new Vector2(.5f,.66f),Vector2.one*.13f,Ink);
                    Disc(mesh,new Vector2(.5f,.66f),Vector2.one*.095f,Gold);
                    break;
                case Symbol.Bell:
                    Disc(mesh,new Vector2(.5f,.81f),Vector2.one*.06f,Ink);
                    Disc(mesh,new Vector2(.5f,.23f),Vector2.one*.09f,Ink);
                    Disc(mesh,new Vector2(.5f,.25f),Vector2.one*.06f,Wood);
                    Poly(mesh,Ink,new Vector2(.18f,.30f),new Vector2(.25f,.42f),new Vector2(.29f,.66f),new Vector2(.36f,.77f),new Vector2(.50f,.81f),new Vector2(.64f,.77f),new Vector2(.71f,.66f),new Vector2(.75f,.42f),new Vector2(.82f,.30f));
                    Poly(mesh,Gold,new Vector2(.26f,.35f),new Vector2(.31f,.45f),new Vector2(.35f,.65f),new Vector2(.40f,.72f),new Vector2(.50f,.75f),new Vector2(.60f,.72f),new Vector2(.65f,.65f),new Vector2(.69f,.45f),new Vector2(.74f,.35f));
                    Stroke(mesh,.025f,Paper,new Vector2(.40f,.49f),new Vector2(.43f,.65f));
                    break;
                case Symbol.Music:
                    Stroke(mesh,.05f,Ink,new Vector2(.38f,.28f),new Vector2(.38f,.75f),new Vector2(.77f,.84f),new Vector2(.77f,.38f));
                    Stroke(mesh,.022f,Gold,new Vector2(.38f,.30f),new Vector2(.38f,.75f),new Vector2(.77f,.84f),new Vector2(.77f,.40f));
                    Disc(mesh,new Vector2(.27f,.26f),new Vector2(.14f,.105f),Ink);
                    Disc(mesh,new Vector2(.26f,.28f),new Vector2(.09f,.065f),Sage);
                    Disc(mesh,new Vector2(.66f,.36f),new Vector2(.14f,.105f),Ink);
                    Disc(mesh,new Vector2(.65f,.38f),new Vector2(.09f,.065f),Sage);
                    break;
                case Symbol.Heart:
                    Heart(mesh,1f,Ink);
                    Heart(mesh,.81f,Clay);
                    Stroke(mesh,.02f,Paper,new Vector2(.26f,.62f),new Vector2(.29f,.69f),new Vector2(.36f,.70f));
                    break;
                case Symbol.ZoomIn:
                case Symbol.ZoomOut:
                    Stroke(mesh,.075f,Ink,new Vector2(.61f,.39f),new Vector2(.83f,.17f));
                    Stroke(mesh,.04f,Wood,new Vector2(.62f,.38f),new Vector2(.83f,.17f));
                    Disc(mesh,new Vector2(.43f,.58f),Vector2.one*.28f,Ink);
                    Disc(mesh,new Vector2(.43f,.58f),Vector2.one*.22f,Paper);
                    Stroke(mesh,.027f,Ink,new Vector2(.31f,.58f),new Vector2(.55f,.58f));
                    if(symbol==Symbol.ZoomIn) Stroke(mesh,.027f,Ink,new Vector2(.43f,.46f),new Vector2(.43f,.70f));
                    break;
                case Symbol.Seed:
                    DrawLeaf(mesh,new Vector2(.31f,.20f),new Vector2(.72f,.79f),.23f,Gold);
                    Stroke(mesh,.022f,Wood,new Vector2(.37f,.28f),new Vector2(.53f,.52f));
                    break;
                case Symbol.BookMark:
                    Poly(mesh,Ink,new Vector2(.25f,.87f),new Vector2(.75f,.87f),new Vector2(.75f,.14f),new Vector2(.5f,.30f),new Vector2(.25f,.14f));
                    Poly(mesh,Sage,new Vector2(.30f,.82f),new Vector2(.70f,.82f),new Vector2(.70f,.24f),new Vector2(.5f,.37f),new Vector2(.30f,.24f));
                    DrawLeaf(mesh,new Vector2(.41f,.50f),new Vector2(.60f,.72f),.075f,Paper);
                    break;
                case Symbol.Sparkle:
                    Star(mesh,new Vector2(.45f,.48f),.34f,Ink);
                    Star(mesh,new Vector2(.45f,.48f),.26f,Gold);
                    Star(mesh,new Vector2(.79f,.80f),.12f,Sage);
                    Star(mesh,new Vector2(.78f,.22f),.07f,Gold);
                    break;
            }
        }

        private void DrawPot(VertexHelper mesh)
        {
            Poly(mesh,Ink,new Vector2(.24f,.47f),new Vector2(.76f,.47f),new Vector2(.67f,.12f),new Vector2(.33f,.12f));
            Poly(mesh,Clay,new Vector2(.30f,.43f),new Vector2(.70f,.43f),new Vector2(.63f,.17f),new Vector2(.37f,.17f));
            Box(mesh,.19f,.43f,.62f,.14f,.035f,Ink);
            Box(mesh,.23f,.47f,.54f,.06f,.015f,Clay);
            Stroke(mesh,.022f,Paper,new Vector2(.39f,.35f),new Vector2(.42f,.24f));
        }
        private void Heart(VertexHelper mesh,float scale,Color pigment)
        {
            Vector2 center=Vector2.one*.5f;
            var points=new[] {new Vector2(.5f,.17f),new Vector2(.20f,.43f),new Vector2(.15f,.61f),new Vector2(.20f,.77f),new Vector2(.34f,.82f),new Vector2(.50f,.71f),new Vector2(.66f,.82f),new Vector2(.80f,.77f),new Vector2(.85f,.61f),new Vector2(.80f,.43f)};
            for(int i=0;i<points.Length;i++) points[i]=center+(points[i]-center)*scale;
            Poly(mesh,pigment,points);
        }
        private void Star(VertexHelper mesh,Vector2 center,float radius,Color pigment)
        {
            var points=new Vector2[8];
            for(int i=0;i<8;i++) { float a=i*Mathf.PI/4f;points[i]=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*(i%2==0?1f:.30f); }
            Poly(mesh,pigment,points);
        }
        private void DrawLeaf(VertexHelper mesh,Vector2 start,Vector2 end,float width,Color pigment)
        {
            LeafShape(mesh,start,end,width,Ink);
            Vector2 direction=end-start;
            LeafShape(mesh,start+direction*.08f,end-direction*.08f,width*.74f,pigment);
            Stroke(mesh,.013f,Paper,start+direction*.22f,start+direction*.73f);
        }
        private void LeafShape(VertexHelper mesh,Vector2 start,Vector2 end,float width,Color pigment)
        {
            Vector2 direction=(end-start).normalized;
            Vector2 normal=new Vector2(-direction.y,direction.x);
            var points=new Vector2[14];
            for(int i=0;i<7;i++)
            {
                float t=i/6f;
                points[i]=Vector2.Lerp(start,end,t)+normal*(Mathf.Sin(t*Mathf.PI)*width);
                points[13-i]=Vector2.Lerp(start,end,t)-normal*(Mathf.Sin(t*Mathf.PI)*width*.72f);
            }
            Poly(mesh,pigment,points);
        }
        private void Box(VertexHelper mesh,float x,float y,float width,float height,float radius,Color pigment)
        {
            var points=new Vector2[20];
            Vector2[] centers={new Vector2(x+width-radius,y+height-radius),new Vector2(x+radius,y+height-radius),new Vector2(x+radius,y+radius),new Vector2(x+width-radius,y+radius)};
            for(int corner=0;corner<4;corner++) for(int step=0;step<5;step++)
            {
                float angle=(corner*90f+step*22.5f)*Mathf.Deg2Rad;
                points[corner*5+step]=centers[corner]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
            }
            Poly(mesh,pigment,points);
        }
        private void Disc(VertexHelper mesh,Vector2 center,Vector2 radius,Color pigment)
        {
            const int count=24;
            var points=new Vector2[count];
            for(int i=0;i<count;i++) {float angle=i*Mathf.PI*2/count;points[i]=center+Vector2.Scale(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)),radius);}
            Poly(mesh,pigment,points);
        }
        private void Poly(VertexHelper mesh,Color pigment,params Vector2[] points)
        {
            if(points.Length<3)return;
            Vector2 center=Vector2.zero;
            foreach(var p in points)center+=p;
            center/=points.Length;
            Color tint=Tint(pigment);
            int first=mesh.currentVertCount;
            mesh.AddVert(Position(center),tint,Vector2.zero);
            foreach(var p in points)mesh.AddVert(Position(p),tint,Vector2.zero);
            for(int i=0;i<points.Length;i++)mesh.AddTriangle(first,first+1+i,first+1+(i+1)%points.Length);
        }
        private void Stroke(VertexHelper mesh,float width,Color pigment,params Vector2[] points)
        {
            for(int i=1;i<points.Length;i++)
            {
                Vector2 a=points[i-1],b=points[i],direction=(b-a).normalized;
                Vector2 normal=new Vector2(-direction.y,direction.x)*width;
                Poly(mesh,pigment,a-normal,a+normal,b+normal,b-normal);
            }
            // Small round joins keep arrows and stems soft without a texture or extra material.
            foreach(var point in points)Disc(mesh,point,Vector2.one*width,pigment);
        }
        private Vector2 Position(Vector2 point) { return iconRect.min+Vector2.Scale(point,iconRect.size); }
        private Color Tint(Color pigment)
        {
            // Preserve the illustration palette; the caller still controls hue gently and opacity fully.
            Color result=Color.Lerp(pigment,new Color(color.r,color.g,color.b,1f),.14f);
            result.a=pigment.a*color.a;
            return result;
        }
    }
}
