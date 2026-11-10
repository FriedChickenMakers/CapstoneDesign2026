using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    /// <summary>
    /// Scalable garden UI with attributed ambientCG timber and leaf textures.
    /// Reading surfaces retain their calm paper treatment.
    /// The surface is decoration only and never changes gameplay, layout or hit testing.
    /// </summary>
    [AddComponentMenu("UI/Garden Theme Surface")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GardenThemeSurface : MaskableGraphic
    {
        // Preserve serialized styles from the integrated branch; append new texture variants.
        public enum Style { Paper, Wood, Primary, Selected, Backdrop, Header, Shelf, Input, ProgressTrack, ProgressFill, TabWood, Home, NavigationLeaf }
        public enum Tone { Default, Growth, Additional, Reflection, Utility }
        public Style style = Style.Paper;
        public Tone tone = Tone.Default;
        const string SurfaceName = "Garden theme surface";
        Selectable ownerSelectable;
        GardenThemePointerState pointer;
        int lastState = -1;
        Style lastStyle;
        Tone lastTone;
        bool disabled;
        bool pressed;
        bool highlighted;
        static Texture2D backdropTexture;
        static Texture2D BackdropTexture => backdropTexture != null ? backdropTexture :
            (backdropTexture = Resources.Load<Texture2D>("UI/Textures/Wood096Background"));
        static Texture2D woodTexture;
        static Texture2D denseLeafTexture;
        static Texture2D DenseLeafTexture => denseLeafTexture != null ? denseLeafTexture :
            (denseLeafTexture = Resources.Load<Texture2D>("UI/Textures/LeafSet024LitNavigation"));
        static Texture2D homeTexture;
        static Texture2D HomeTexture => homeTexture != null ? homeTexture :
            (homeTexture = Resources.Load<Texture2D>("UI/Textures/LeafSet024UI"));
        static Texture2D leafTexture;
        static Texture2D LeafTexture => leafTexture != null ? leafTexture :
            (leafTexture = Resources.Load<Texture2D>("UI/Textures/LeafSet017UI"));
        static Texture2D shelfTexture;
        static bool shelfTextureLoaded;
        const string ButtonTexturePath = "UI/Textures/LightWoodButtonUI"; // Wood092, baked normal/roughness
        static Texture2D headerTexture;
        static Texture2D HeaderTexture => headerTexture != null ? headerTexture :
            (headerTexture = Resources.Load<Texture2D>("UI/Textures/LightWoodTitleUI"));
        static Texture2D groundTexture;
        static Texture2D GroundTexture => groundTexture != null ? groundTexture :
            (groundTexture = Resources.Load<Texture2D>("UI/Textures/Ground048UI"));
        // Keep navigation backing independent from buttons and page headings.
        const string ShelfTexturePath = "UI/Textures/LightWoodTitleUI";
        static readonly Color TimberBorder = new Color32(76, 65, 52, 255);
        static readonly Color TimberWash = new Color32(92, 81, 68, 72);
        static readonly Color ButtonWash = new Color32(92, 81, 68, 38);
        static readonly Color ShelfWash = new Color32(64, 59, 51, 82);
        static readonly Color LeafWash = new Color32(52, 74, 57, 72);
        static readonly Color SelectedLeafWash = new Color32(199, 213, 193, 68);
        [Min(1)] public float textureZoom = 1f;
        bool texturedFace;
        Rect textureFaceRect;
        bool UsesWood => style == Style.Wood || style == Style.TabWood || style == Style.Header || style == Style.Shelf;
        static Texture2D WoodTexture => woodTexture != null ? woodTexture :
            (woodTexture = Resources.Load<Texture2D>(ButtonTexturePath));
        static Texture2D ShelfTexture
        {
            get
            {
                if (!shelfTextureLoaded)
                {
                    shelfTexture = Resources.Load<Texture2D>(ShelfTexturePath);
                    shelfTextureLoaded = true;
                }
                return shelfTexture != null ? shelfTexture : GroundTexture;
            }
        }
        bool UsesTexture => style == Style.Backdrop || UsesWood || style == Style.Primary || style == Style.Home || style == Style.NavigationLeaf;
        Texture2D SurfaceTexture => style == Style.NavigationLeaf ? DenseLeafTexture : style == Style.Backdrop ? BackdropTexture : style == Style.Home ? HomeTexture : style == Style.Primary ? LeafTexture : style == Style.Shelf ? ShelfTexture : style == Style.Header ? HeaderTexture : WoodTexture;
        public override Texture mainTexture => UsesTexture && SurfaceTexture != null ? SurfaceTexture : base.mainTexture;
        public static readonly Color WoodInk = new Color32(31, 56, 40, 255);
        public static Color Foreground(Transform parent, Color fallback)
        {
            for (var node = parent; node != null; node = node.parent)
            {
                var surface = node.Find(SurfaceName)?.GetComponent<GardenThemeSurface>();
                if (surface != null) return surface.UsesWood ? WoodInk : fallback;
            }
            return fallback;
        }

        public static GardenThemeSurface Apply(GameObject owner, Style style, Tone tone = Tone.Default)
        {
            if (owner == null) return null;
            Transform child = owner.transform.Find(SurfaceName);
            GardenThemeSurface surface = child != null ? child.GetComponent<GardenThemeSurface>() : null;
            bool created=surface==null;
            if (surface == null)
            {
                var go = new GameObject(SurfaceName, typeof(RectTransform), typeof(LayoutElement));
                go.transform.SetParent(owner.transform, false);
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                surface = go.AddComponent<GardenThemeSurface>();
            }
            bool changed=created || surface.style!=style || surface.tone!=tone || surface.ownerSelectable!=owner.GetComponent<Selectable>();
            surface.style = style;
            surface.tone = tone;
            surface.raycastTarget = false;
            surface.color = Color.white;
            RectTransform rect = surface.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            surface.transform.SetAsFirstSibling();
            surface.gameObject.SetActive(true);
            if(changed)
            {
                surface.BindOwner();
                surface.UpdateIvy();
                surface.SetAllDirty();
            }
            return surface;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            BindOwner();
        }

        void BindOwner()
        {
            ownerSelectable = transform.parent != null ? transform.parent.GetComponent<Selectable>() : null;
            pointer = null;
            if (ownerSelectable != null)
            {
                pointer = ownerSelectable.GetComponent<GardenThemePointerState>();
                if (pointer == null) pointer = ownerSelectable.gameObject.AddComponent<GardenThemePointerState>();
            }
            lastState = -1;
        }

        void LateUpdate()
        {
            int state = State();
            if (state == lastState && style == lastStyle && tone == lastTone) return;
            lastState = state;
            lastStyle = style;
            lastTone = tone;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        int State()
        {
            if (ownerSelectable == null) return 0;
            if (!ownerSelectable.IsActive() || !ownerSelectable.IsInteractable()) return 3;
            if (pointer != null && pointer.IsPressed && pointer.IsInside) return 2;
            if (pointer != null && (pointer.IsInside || pointer.IsSelected)) return 1;
            return 0;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0 || r.height <= 0) return;
            int state = State();
            disabled = state == 3;
            pressed = state == 2;
            highlighted = state == 1;
            float unit = Mathf.Clamp(Mathf.Min(r.width, r.height) / 68f, .35f, 1.2f);
            switch (style)
            {
                case Style.Backdrop: Backdrop(mesh, r); break;
                case Style.NavigationLeaf: LitNavigation(mesh, r, unit); break;
                case Style.Home: Primary(mesh, r, unit); break;
                case Style.Primary: Primary(mesh, r, unit); break;
                case Style.TabWood:
                case Style.Wood: Timber(mesh, r, unit, false); break;
                case Style.Header: Timber(mesh, r, unit, true); break;
                case Style.Shelf: Shelf(mesh, r, unit); break;
                case Style.Selected: Paper(mesh, r, unit, true, false); break;
                case Style.Input: Paper(mesh, r, unit, false, true); break;
                case Style.ProgressTrack: Progress(mesh, r, false); break;
                case Style.ProgressFill: Progress(mesh, r, true); break;
                default: Paper(mesh, r, unit, false, false); break;
            }
        }

        void Paper(VertexHelper mesh, Rect r, float u, bool selected, bool input)
        {
            float radius = Mathf.Min(16f * u, r.height * .2f);
            Shadow(mesh, r, radius, 3f * u);
            Rounded(mesh, r, radius, selected ? GardenThemePalette.Sage : GardenThemePalette.Border);
            Rect face = Inset(r, selected ? 3f * u : 1.5f * u);
            Color top = selected ? new Color32(239, 245, 211, 255) : GardenThemePalette.Paper;
            Color bottom = selected ? new Color32(219, 232, 181, 255) : GardenThemePalette.PaperShade;
            if (input) { top = new Color32(248, 244, 224, 255); bottom = GardenThemePalette.Paper; }
            Gradient(mesh, face, Mathf.Max(1, radius - 2 * u), top, bottom);
            Rect line = Inset(r, 5f * u);
            if (r.width > 180 && r.height > 90 && !input)
            {
                // A fine inset rule keeps the reading area calm and gives paper a finished edge.
                Border(mesh, line, Mathf.Max(2, radius - 4 * u), .65f * u,
                    Alpha(selected ? GardenThemePalette.Sage : GardenThemePalette.WoodDark, .18f));
                PaperFlecks(mesh, r, u);
            }
            if (input)
            {
                Line(mesh, new Vector2(r.xMin + 13 * u, r.yMax - 6 * u),
                    new Vector2(r.xMax - 13 * u, r.yMax - 6 * u), u, Alpha(GardenThemePalette.WoodDark, .18f));
            }
            if (selected && r.width > 100 && r.height > 48)
            {
                Leaf(mesh, new Vector2(r.xMax - 13 * u, r.yMax - 11 * u),
                    new Vector2(-10, -16) * u, 5 * u, GardenThemePalette.Sage, GardenThemePalette.Leaf);
            }
        }

        void Timber(VertexHelper mesh, Rect r, float u, bool header)
        {
            if (SurfaceTexture == null) { TimberFace(mesh, r, 10); return; }
            float radius = Mathf.Min(10, r.height * .20f);
            const float bevel = 2.8f;
            float depth = pressed ? 1 : bevel * 1.1f;
            LitRounded(mesh, Offset(r, -.7f, -depth-1.5f), radius+1, new Color32(70,60,39,19), false, r);
            LitRounded(mesh, Offset(r, 0, -depth-.5f), radius, new Color32(70,60,39,35), false, r);
            Color edge = (header ? new Color32(216,203,180,255) : new Color32(218,202,174,255));
            edge *= .66f; edge.a=1;
            LitRounded(mesh, Offset(r, 0, -depth), radius, edge, false, r);
            var face=Inset(r,bevel);
            if(pressed)face=Offset(face,0,-1);
            LitRim(mesh,r,face,radius,Mathf.Max(0,radius-bevel));
            LitRounded(mesh,face,Mathf.Max(0,radius-bevel),Color.white,true,face);
        }

        void LitNavigation(VertexHelper mesh, Rect r, float u)
        {
            float radius=Mathf.Min(16*u,r.height*.24f);
            Shadow(mesh,r,radius,(pressed?1:4)*u);
            Rounded(mesh,r,radius,new Color32(41,73,53,255));
            var face=Inset(r,2*u);
            if(pressed)face=Offset(face,0,-1.5f*u);
            // Preserve the approved broad-leaf crop; only its frame matches the garden action.
            LitRounded(mesh,face,Mathf.Max(1,radius-2*u),Color.white,true,Inset(r,1.7f));
        }

        void UpdateIvy()
        {
            bool timber=style==Style.Wood || style==Style.Header;
            for(int i=0;i<2;i++)
            {
                string name=i==0?"Lit ivy right":"Lit ivy left";
                var child=transform.Find(name);
                bool visible=timber && (i==0 || style!=Style.Header);
                if(child==null && !visible)continue;
                if(child==null)
                {
                    var go=new GameObject(name,typeof(RectTransform),typeof(LayoutElement),typeof(RawImage));
                    go.transform.SetParent(transform,false);
                    go.GetComponent<LayoutElement>().ignoreLayout=true;child=go.transform;
                }
                child.gameObject.SetActive(visible);
                if(!visible)continue;
                var rect=(RectTransform)child;
                bool right=i==0,header=style==Style.Header;
                float size=right?(header?46:39):42;
                rect.anchorMin=rect.anchorMax=new Vector2(right?1:0,.5f);rect.pivot=new Vector2(.5f,.5f);
                rect.sizeDelta=new Vector2(size,size);rect.anchoredPosition=right?new Vector2(header?-19:-17,header?9:7):new Vector2(18,-7);
                rect.localRotation=Quaternion.Euler(0,0,right?-22:153);
                var image=child.GetComponent<RawImage>();
                image.texture=Resources.Load<Texture2D>(right?"UI/Textures/IvyLitRightUI":"UI/Textures/IvyLitLeftUI");
                image.raycastTarget=false;int cell=right?(header?0:1):3;
                image.uvRect=new Rect((cell%3)/3f,1f-(cell/3+1)/3f,1f/3f,1f/3f);
            }
        }

        static Vector2 LitPerimeter(Rect r,float radius,int corner,int step)
        {
            var center=Corner(r,radius,corner);
            float angle=(corner*90+step*90f/8)*Mathf.Deg2Rad;
            return center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
        }
        void LitRim(VertexHelper mesh,Rect outer,Rect inner,float outerRadius,float innerRadius)
        {
            const int count=36;int start=mesh.currentVertCount;
            for(int corner=0;corner<4;corner++)for(int i=0;i<=8;i++)
            {
                float a=(corner*90+i*90f/8)*Mathf.Deg2Rad;
                float shade=.82f+.18f*Vector2.Dot(new Vector2(Mathf.Cos(a),Mathf.Sin(a)),new Vector2(-.52f,.66f).normalized);
                LitVertex(mesh,LitPerimeter(outer,outerRadius,corner,i),new Color(shade,shade,shade,1),true,inner,false);
                LitVertex(mesh,LitPerimeter(inner,innerRadius,corner,i),Color.white,true,inner,false);
            }
            for(int i=0;i<count;i++)
            {
                int a=start+2*i,b=start+2*((i+1)%count);
                mesh.AddTriangle(a,b,b+1);mesh.AddTriangle(a,b+1,a+1);
            }
        }
        void LitRounded(VertexHelper mesh,Rect r,float radius,Color tint,bool textured,Rect face)
        {
            const int count=36;int start=mesh.currentVertCount;
            LitVertex(mesh,r.center,tint,textured,face);
            for(int corner=0;corner<4;corner++)for(int i=0;i<=8;i++)
                LitVertex(mesh,LitPerimeter(r,radius,corner,i),tint,textured,face);
            for(int i=0;i<count;i++)mesh.AddTriangle(start,start+i+1,start+(i+1)%count+1);
        }
        void LitVertex(VertexHelper mesh,Vector2 point,Color tint,bool textured,Rect face,bool light=true)
        {
            var texture=SurfaceTexture;
            Vector2 uv=new Vector2(.5f,(texture.height-16f)/texture.height);
            if(textured)
            {
                if(light)
                {
                    float nx=Mathf.Clamp01((point.x-face.xMin)/face.width),ny=Mathf.Clamp01((point.y-face.yMin)/face.height);
                    float shade=.945f+.035f*ny+.02f*(1-nx);
                    tint=new Color(tint.r*shade,tint.g*shade,tint.b*shade,tint.a);
                }
                bool rotate=style==Style.Wood || style==Style.TabWood;
                float w=rotate?texture.height-32:texture.width,h=rotate?texture.width:texture.height-32;
                float coverage=style==Style.NavigationLeaf?.94f:style==Style.Shelf?.72f/2.5f:.72f;
                float scale=Mathf.Min(w/face.width,h/face.height)*coverage;
                Vector2 delta=(point-face.center)*scale;
                if(rotate)delta=new Vector2(delta.y,-delta.x);
                uv=new Vector2(.5f+delta.x/texture.width,((texture.height-32)*.5f+delta.y)/texture.height);
            }
            if(disabled)tint=Color.Lerp(tint,new Color(.86f,.85f,.78f,tint.a),.4f);
            else if(pressed)tint=Color.Lerp(tint,new Color(.20f,.29f,.19f,tint.a),.1f);
            else if(highlighted)tint=Color.Lerp(tint,new Color(1,.99f,.86f,tint.a),.06f);
            mesh.AddVert(point,tint*color,uv);
        }

        void Primary(VertexHelper mesh, Rect r, float u)
        {
            float radius = Mathf.Min(16 * u, r.height * .24f);
            Shadow(mesh, r, radius, (pressed ? 1 : 4) * u);
            bool navigationSelected = style == Style.Home || style == Style.NavigationLeaf;
            if (!navigationSelected) Rounded(mesh, r, radius, new Color32(41, 73, 53, 255));
            Rect face = navigationSelected ? r : Inset(r, 2 * u);
            if (pressed) face = Offset(face, 0, -1.5f * u);
            if (SurfaceTexture != null)
            {
                textureFaceRect = face;
                texturedFace = true;
                float faceRadius = navigationSelected ? radius : Mathf.Max(1, radius - 2 * u);
                Gradient(mesh, face, faceRadius,
                    style == Style.NavigationLeaf ? new Color(.59f, .65f, .56f) :
                    navigationSelected ? new Color(.90f, .96f, .91f) : new Color(.64f, .69f, .61f),
                    style == Style.NavigationLeaf ? new Color(.52f, .58f, .50f) :
                    navigationSelected ? new Color(.86f, .92f, .87f) : new Color(.56f, .62f, .54f));
                texturedFace = false;
                TextureWash(mesh, face, faceRadius, style == Style.Home ? SelectedLeafWash : LeafWash);
                return;
            }
            Gradient(mesh, face, Mathf.Max(1, radius - 2 * u),
                new Color32(80, 130, 89, 255), GardenThemePalette.Forest);
            Border(mesh, Inset(face, 3 * u), Mathf.Max(1, radius - 5 * u), 1f * u,
                Alpha(GardenThemePalette.Mint, .42f));
            // Botanical corners reserve the centre for long Korean action labels.
            float clusterSize = Mathf.Min(r.height * .66f, 54 * u);
            if (r.width >= 280 && r.height >= 48)
            {
                Sprig(mesh, new Vector2(r.xMin + 7 * u, r.yMin + 6 * u), clusterSize, 1, 1f);
                Sprig(mesh, new Vector2(r.xMax - 7 * u, r.yMax - 6 * u), clusterSize, -1, 1f);
            }
            else if (r.width >= 100 && r.height >= 48)
            {
                // Paired actions retain their text width; a single corner leaf still carries the theme.
                Leaf(mesh, new Vector2(r.xMin + 4 * u, r.yMin + 7 * u),
                    new Vector2(9, 15) * u, 4.3f * u, GardenThemePalette.Sage, GardenThemePalette.Leaf);
                Leaf(mesh, new Vector2(r.xMax - 4 * u, r.yMax - 7 * u),
                    new Vector2(-9, -15) * u, 4.3f * u, GardenThemePalette.Leaf, GardenThemePalette.Mint);
            }
        }

        void Shelf(VertexHelper mesh, Rect r, float u)
        {
            // The inset navigation shelf has transparent rounded corners; the
            // parent keeps the touch rectangle separate from this decoration.
            Shadow(mesh, r, 15 * u, 4 * u);
            if(SurfaceTexture!=null)
                LitRounded(mesh,r,15*u,new Color(.96f,.91f,.83f),true,r);
            else TimberFace(mesh, r, 15 * u);
            if (SurfaceTexture == null) WoodGrain(mesh, r, u);
        }

        void TimberFace(VertexHelper mesh, Rect face, float radius)
        {
            if (SurfaceTexture == null)
            {
                Gradient(mesh, face, radius, GardenThemePalette.WoodLight, GardenThemePalette.Wood);
                return;
            }
            textureFaceRect = face;
            texturedFace = true;
            Gradient(mesh, face, radius,
                style == Style.Shelf ? new Color(.53f, .52f, .48f) : new Color(.92f, .93f, .90f),
                style == Style.Shelf ? new Color(.46f, .45f, .42f) : new Color(.85f, .87f, .83f));
            texturedFace = false;
            TextureWash(mesh, face, radius, style == Style.Shelf ? ShelfWash :
                style == Style.Wood || style == Style.TabWood ? ButtonWash : TimberWash);
        }

        void TextureWash(VertexHelper mesh, Rect face, float radius, Color tint)
        {
            // The existing atlas has a white strip for solid decoration. A translucent
            // face gently compresses photographic contrast without replacing the texture,
            // allocating materials, or changing the opaque face and its hit rectangle.
            Rounded(mesh, face, radius, tint);
        }

        void Progress(VertexHelper mesh, Rect r, bool fill)
        {
            float radius = Mathf.Min(r.height * .5f, 9);
            Rounded(mesh, r, radius, fill ? GardenThemePalette.Forest : GardenThemePalette.WoodDark);
            float inset = Mathf.Min(2, r.height * .16f);
            Gradient(mesh, Inset(r, inset), Mathf.Max(0, radius - inset),
                fill ? GardenThemePalette.Leaf : new Color32(227, 211, 172, 255),
                fill ? GardenThemePalette.Sage : new Color32(210, 190, 146, 255));
        }

        void Backdrop(VertexHelper mesh, Rect r)
        {
            if (BackdropTexture != null)
            {
                textureFaceRect = r;
                texturedFace = true;
                Gradient(mesh, r, 0, Color.white, Color.white);
                texturedFace = false;
                return;
            }
            Gradient(mesh, r, 0, new Color32(250, 247, 231, 255), new Color32(233, 232, 204, 255));
            float scale = Mathf.Clamp(r.width / 720f, .5f, 1.5f);
            // Low-contrast border foliage leaves the entire middle available to content.
            Sprig(mesh, new Vector2(r.xMin - 7 * scale, r.yMax - 190 * scale), 140 * scale, 1, .16f);
            Sprig(mesh, new Vector2(r.xMax + 7 * scale, r.yMin + 220 * scale), 155 * scale, -1, .17f);
            Sprig(mesh, new Vector2(r.xMin - 7 * scale, r.yMin + 36 * scale), 104 * scale, 1, .12f);
            for (int i = 0; i < 16; i++)
            {
                float y = r.yMin + r.height * ((i + .5f) / 16f);
                float x = i % 2 == 0 ? r.xMin + (14 + i % 3 * 8) * scale : r.xMax - (14 + i % 3 * 8) * scale;
                Circle(mesh, new Vector2(x, y), 1.2f * scale, Alpha(GardenThemePalette.WoodDark, .13f));
            }
        }

        void WoodGrain(VertexHelper mesh, Rect r, float u)
        {
            if (r.width < 80 || r.height < 30) return;
            Color grain = Alpha(GardenThemePalette.WoodDark, .18f);
            float length = Mathf.Min(r.width * .16f, 70 * u);
            for (int i = 0; i < 3; i++)
            {
                float y = r.yMin + r.height * (.22f + i * .28f);
                float x = r.xMin + (10 + i % 2 * 5) * u;
                Line(mesh, new Vector2(x, y), new Vector2(x + length * .55f, y + u), .75f * u, grain);
                Line(mesh, new Vector2(x + length * .55f, y + u), new Vector2(x + length, y), .75f * u, grain);
                x = r.xMax - (10 + i % 2 * 5) * u;
                Line(mesh, new Vector2(x, y + 3 * u), new Vector2(x - length, y + 4 * u), .75f * u, grain);
            }
        }

        void PaperFlecks(VertexHelper mesh, Rect r, float u)
        {
            Color fleck = Alpha(GardenThemePalette.WoodDark, .11f);
            for (int i = 0; i < 4; i++)
            {
                float y = r.yMin + r.height * (.14f + i * .22f);
                float x = (i % 2 == 0 ? r.xMin + 10 * u : r.xMax - 12 * u);
                Line(mesh, new Vector2(x, y), new Vector2(x + 2.5f * u, y + .7f * u), .7f * u, fleck);
            }
        }

        void Sprig(VertexHelper mesh, Vector2 origin, float size, float direction, float opacity)
        {
            Vector2 a = origin;
            Vector2 b = origin + new Vector2(size * .18f, size * .48f) * direction;
            Vector2 c = origin + new Vector2(size * .10f, size * .95f) * direction;
            Color stem = Alpha(GardenThemePalette.Mint, opacity);
            Line(mesh, a, b, Mathf.Max(1, size * .028f), stem);
            Line(mesh, b, c, Mathf.Max(.8f, size * .022f), stem);
            for (int i = 0; i < 5; i++)
            {
                float t = .16f + i * .15f;
                Vector2 root = t < .5f ? Vector2.Lerp(a, b, t * 2) : Vector2.Lerp(b, c, (t - .5f) * 2);
                bool right = i % 2 == 0;
                Vector2 axis = new Vector2(right ? .35f : -.15f, .24f) * size * direction;
                Color dark = Alpha(i % 2 == 0 ? GardenThemePalette.Sage : GardenThemePalette.Leaf, opacity);
                Color light = Alpha(i % 2 == 0 ? GardenThemePalette.Leaf : GardenThemePalette.Mint, opacity);
                Leaf(mesh, root, axis, size * (right ? .11f : .085f), dark, light);
            }
            Leaf(mesh, c - new Vector2(0, size * .06f) * direction,
                new Vector2(.04f, .25f) * size * direction, size * .085f,
                Alpha(GardenThemePalette.Sage, opacity), Alpha(GardenThemePalette.Mint, opacity));
            if (opacity > .5f)
            {
                Vector2 flower = a + new Vector2(size * .26f, size * .16f) * direction;
                float petal = size * .043f;
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.PI * .4f;
                    Circle(mesh, flower + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * petal * 1.3f,
                        petal, GardenThemePalette.Paper);
                }
                Circle(mesh, flower, petal * .7f, GardenThemePalette.Gold);
            }
        }

        void Leaf(VertexHelper mesh, Vector2 root, Vector2 axis, float width, Color dark, Color light)
        {
            Vector2 n = new Vector2(-axis.y, axis.x).normalized * width;
            Vector2 tip = root + axis;
            Vector2 middle = root + axis * .52f;
            // Two curved halves with a gently highlighted centre vein.
            Triangle(mesh, root, root + axis * .23f + n * .75f, middle, light);
            Triangle(mesh, root + axis * .23f + n * .75f, middle + n, middle, light);
            Triangle(mesh, middle + n, root + axis * .78f + n * .6f, tip, light);
            Triangle(mesh, middle + n, tip, middle, light);
            Triangle(mesh, root, middle, root + axis * .23f - n * .75f, dark);
            Triangle(mesh, root + axis * .23f - n * .75f, middle, middle - n, dark);
            Triangle(mesh, middle - n, middle, tip, dark);
            Triangle(mesh, middle - n, tip, root + axis * .78f - n * .6f, dark);
            Line(mesh, root, tip - axis * .12f, Mathf.Max(.5f, width * .075f), Alpha(GardenThemePalette.Mint, light.a * .6f));
        }

        void Shadow(VertexHelper mesh, Rect r, float radius, float depth)
        {
            Rounded(mesh, Offset(r, 0, -depth), radius, new Color32(97, 81, 49, 38));
        }

        void Border(VertexHelper mesh, Rect r, float radius, float thickness, Color tint)
            => Border(mesh, r, radius, thickness, tint, tint);

        void Border(VertexHelper mesh, Rect r, float radius, float thickness, Color top, Color bottom)
        {
            if (r.width <= 0 || r.height <= 0 || thickness <= 0) return;
            radius = Mathf.Clamp(radius, 0, Mathf.Min(r.width, r.height) * .5f);
            Rect inner = Inset(r, thickness);
            if (inner.width <= 0 || inner.height <= 0) return;
            float innerRadius = Mathf.Max(0, radius - thickness);
            const int segments = 5;
            int start = mesh.currentVertCount;
            int count = 4 * (segments + 1);
            for (int corner = 0; corner < 4; corner++)
            {
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (corner * 90f + i * 90f / segments) * Mathf.Deg2Rad;
                    Vector2 unit = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 outerPoint = Corner(r, radius, corner) + unit * radius;
                    Vector2 innerPoint = Corner(inner, innerRadius, corner) + unit * innerRadius;
                    Vertex(mesh, outerPoint, Color.Lerp(bottom, top, Mathf.InverseLerp(r.yMin, r.yMax, outerPoint.y)));
                    Vertex(mesh, innerPoint, Color.Lerp(bottom, top, Mathf.InverseLerp(r.yMin, r.yMax, innerPoint.y)));
                }
            }
            for (int i = 0; i < count; i++)
            {
                int a = start + i * 2;
                int b = start + ((i + 1) % count) * 2;
                mesh.AddTriangle(a, b, a + 1);
                mesh.AddTriangle(b, b + 1, a + 1);
            }
        }

        void Rounded(VertexHelper mesh, Rect r, float radius, Color tint) => Gradient(mesh, r, radius, tint, tint);

        void Gradient(VertexHelper mesh, Rect r, float radius, Color top, Color bottom)
        {
            if (r.width <= 0 || r.height <= 0) return;
            radius = Mathf.Clamp(radius, 0, Mathf.Min(r.width, r.height) * .5f);
            int start = mesh.currentVertCount;
            Vertex(mesh, r.center, Color.Lerp(bottom, top, .5f));
            const int segments = 5;
            const int count = 4 * (segments + 1);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 centre = Corner(r, radius, corner);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (corner * 90f + i * 90f / segments) * Mathf.Deg2Rad;
                    Vector2 point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    Vertex(mesh, point, Color.Lerp(bottom, top, Mathf.InverseLerp(r.yMin, r.yMax, point.y)));
                }
            }
            for (int i = 0; i < count; i++) mesh.AddTriangle(start, start + i + 1, start + (i + 1) % count + 1);
        }

        static Vector2 Corner(Rect r, float radius, int corner) => new Vector2(
            corner == 0 || corner == 3 ? r.xMax - radius : r.xMin + radius,
            corner < 2 ? r.yMax - radius : r.yMin + radius);

        void Circle(VertexHelper mesh, Vector2 centre, float radius, Color tint)
        {
            int start = mesh.currentVertCount;
            Vertex(mesh, centre, tint);
            const int segments = 10;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vertex(mesh, centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint);
            }
            for (int i = 0; i < segments; i++) mesh.AddTriangle(start, start + i + 1, start + (i + 1) % segments + 1);
        }

        void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            int start = mesh.currentVertCount;
            Vertex(mesh, a - n, tint); Vertex(mesh, a + n, tint);
            Vertex(mesh, b + n, tint); Vertex(mesh, b - n, tint);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }

        void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int start = mesh.currentVertCount;
            Vertex(mesh, a, tint); Vertex(mesh, b, tint); Vertex(mesh, c, tint);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        void Vertex(VertexHelper mesh, Vector2 point, Color tint)
        {
            if (disabled)
            {
                float gray = tint.grayscale;
                tint = Color.Lerp(tint, new Color(gray, gray, gray, tint.a), .65f);
                tint = Color.Lerp(tint, new Color(.86f, .85f, .78f, tint.a), .25f);
            }
            else if (pressed) tint = Color.Lerp(tint, new Color(.20f, .29f, .19f, tint.a), .10f);
            else if (highlighted) tint = Color.Lerp(tint, new Color(1, .99f, .86f, tint.a), .06f);
            Vector2 uv = UsesTexture && SurfaceTexture != null ? new Vector2(.5f, (SurfaceTexture.height-16f)/SurfaceTexture.height) : Vector2.zero;
            if (texturedFace)
            {
                // Aspect-preserving central crop; never squeeze a square photo
                // into a wide button. The top atlas strip is reserved for white.
                // Keep wood grain at a consistent UI-space size: wide buttons reveal
                // more of the source instead of enlarging the same small crop.
                // Enlarge only the shelf grain 2.5x; the approved shelf geometry,
                // icon positions, button gaps and all other texture crops stay fixed.
                float coverage = style == Style.Shelf ? .94f / 2.5f : style == Style.Backdrop ? .80f : style == Style.Wood || style == Style.TabWood || style == Style.Header ? .94f / 3f : .94f;
                float scale = coverage / Mathf.Max(textureFaceRect.width, textureFaceRect.height) / Mathf.Max(1,textureZoom);
                if (style == Style.Wood || style == Style.TabWood || style == Style.Header)
                    scale = Mathf.Min(1f / 480f, .94f / Mathf.Max(textureFaceRect.width, textureFaceRect.height));
                uv = new Vector2(.5f + (point.x - textureFaceRect.center.x) * scale,
                    (.5f + (point.y - textureFaceRect.center.y) * scale) * (style == Style.Backdrop ? 1f : 1024f / 1056f));
            }
            mesh.AddVert(point, tint * color, uv);
        }

        static Color Alpha(Color value, float alpha) { value.a = alpha; return value; }
        static Rect Inset(Rect r, float amount) => Rect.MinMaxRect(r.xMin + amount, r.yMin + amount, r.xMax - amount, r.yMax - amount);
        static Rect Offset(Rect r, float x, float y) => new Rect(r.x + x, r.y + y, r.width, r.height);
    }

    // The Graphic child is intentionally not a raycast target. Read pointer state on the
    // existing Selectable's owner, so buttons keep their original action and hit rectangle.
    [AddComponentMenu("")]
    public sealed class GardenThemePointerState : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public bool IsPressed { get; private set; }
        public bool IsInside { get; private set; }
        public bool IsSelected { get; private set; }
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) { IsPressed = true; IsInside = true; } }
        public void OnPointerUp(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) IsPressed = false; }
        public void OnPointerEnter(PointerEventData data) { IsInside = true; }
        public void OnPointerExit(PointerEventData data) { IsInside = false; }
        public void OnSelect(BaseEventData data) { IsSelected = true; }
        public void OnDeselect(BaseEventData data) { IsSelected = false; IsPressed = false; }
        void OnDisable() { IsPressed = false; IsInside = false; IsSelected = false; }
    }
}
