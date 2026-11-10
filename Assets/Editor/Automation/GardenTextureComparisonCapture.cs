using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Explicit material studies in a synthetic preview scene. These do not
    // replace Resources, modify saved content, or select a product theme.
    public static class GardenTextureComparisonCapture
    {
        sealed class Finish
        {
            public string Asset;
            public Color Base, Ink;
            public float Contrast, Chroma, NormalStrength, Gloss;
            public bool Leaves;
            public Finish(string asset, Color basis, Color ink, float contrast, float chroma, float normalStrength, float gloss, bool leaves=false)
            { Asset=asset;Base=basis;Ink=ink;Contrast=contrast;Chroma=chroma;NormalStrength=normalStrength;Gloss=gloss;Leaves=leaves; }
        }
        sealed class Option
        {
            public string Id, Description;
            public Finish Header, Button;
            public bool LitNavigation, GardenPrimaryNavigation;
            public Option(string id,string description,Finish header,Finish button,bool litNavigation=false,bool gardenPrimaryNavigation=false)
            {Id=id;Description=description;Header=header;Button=button;LitNavigation=litNavigation;GardenPrimaryNavigation=gardenPrimaryNavigation;}
        }
        static readonly Color Ink = new Color32(42,68,49,255);
        static readonly Color CreamInk = new Color32(255,250,232,255);
        static readonly Option[] Options = {
            new Option("A-light-timber", "Wood093 light title + Wood092 warm wood button",
                new Finish("Wood093",new Color32(216,203,180,255),new Color32(31,56,40,255),1.12f,.22f,2.2f,.13f),
                new Finish("Wood092",new Color32(218,202,174,255),new Color32(31,56,40,255),1.30f,.18f,1.8f,.10f)),
            new Option("B-paper-sage", "Paper006 ivory title + muted sage button",
                new Finish("Paper006",new Color32(240,232,208,255),Ink,.70f,.10f,.65f,.025f),
                new Finish("Paper006",new Color32(176,187,161,255),Ink,.85f,.08f,.75f,.03f)),
            new Option("C-paper-ivy", "Ivory paper title + softened moss button, normal-lit ivy",
                new Finish("Paper006",new Color32(240,232,208,255),Ink,.70f,.10f,.65f,.025f,true),
                new Finish("Paper006",new Color32(84,107,83,255),CreamInk,.65f,.06f,.8f,.035f,true)),
            new Option("D-green-label", "Muted moss title + warm paper button, normal-lit ivy",
                new Finish("Paper006",new Color32(98,116,89,255),CreamInk,.65f,.06f,.8f,.035f,true),
                new Finish("Paper006",new Color32(235,226,199,255),Ink,.80f,.10f,.7f,.025f,true))
        };

        public static void RunBatch()=>RunComparison(Options);

        // Same relit timber finish and ivy placement as the previous studies.
        // Keep the leaf-free A option available for a direct visual comparison.
        public static void RunTimberIvyBatch()=>RunComparison(new[]{
            new Option("E-light-timber-ivy", "A light timber with the existing normal-lit green ivy",
                WithLeaves(Options[0].Header),WithLeaves(Options[0].Button))
        });

        public static void RunNavigationLeafBatch()=>RunComparison(new[]{
            new Option("E-light-timber-ivy", "Approved light timber and ivy; original inactive navigation",
                WithLeaves(Options[0].Header),WithLeaves(Options[0].Button)),
            new Option("F-lit-navigation", "Light timber and ivy with normal-lit overlapping inactive navigation leaves",
                WithLeaves(Options[0].Header),WithLeaves(Options[0].Button),true)
        });

        public static void RunGardenStyleBatch()
        {
            RunComparison(new[]{
                new Option("F-lit-navigation", "Previous contrast-dark navigation retained as rollback reference",
                    WithLeaves(Options[0].Header),WithLeaves(Options[0].Button),true),
                new Option("H-garden-frame-same-leaves", "Garden-care frame with the unchanged normal-lit inactive navigation leaf texture",
                    WithLeaves(Options[0].Header),WithLeaves(Options[0].Button),gardenPrimaryNavigation:true)
            });
            GardenActionStyleCapture.RunBatch();
        }

        static Finish WithLeaves(Finish source)=>new Finish(source.Asset,source.Base,source.Ink,
            source.Contrast,source.Chroma,source.NormalStrength,source.Gloss,true);

        static void RunComparison(Option[] options)
        {
            string output=Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            string source=Environment.GetEnvironmentVariable("CAPSTONE_TEXTURE_OPTIONS_SOURCE");
            if(!Application.isBatchMode || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(source)
                || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
                throw new InvalidOperationException("Use explicit batch graphics, CAPSTONE_ARTIFACTS and CAPSTONE_TEXTURE_OPTIONS_SOURCE.");
            string reportPath=Path.Combine(output,"texture-options-report.json");
            if(File.Exists(reportPath) || Directory.Exists(Path.Combine(output,"focus-validation")))
                throw new InvalidOperationException("Use a fresh output directory.");
            Directory.CreateDirectory(output);
            var report=new FocusMissionValidation.Report {runId="texture-options",utc=DateTimeOffset.UtcNow.ToString("O"),
                unityVersion=Application.unityVersion,environment="Editor-only material comparison; actual mission UI in a synthetic preview scene; no device/PlayerPrefs/user saves; product Resources unchanged"};
            var textures=new Dictionary<Finish,Texture2D>();
            var leaves=new Dictionary<int,Texture2D>();
            Texture2D navigationTexture=null;
            var bakeLog=new List<string> {
                "CC0 ambientCG Color + NormalGL + Roughness; LeafSet029 also uses Opacity.",
                "Preview-only offline linear-light bake, not a runtime Canvas normal shader.",
                "Upper-left key, soft ambient fill, roughness-weighted broad highlight; no opaque color wash.",
                "Normal XY is rotated into screen coordinates before lighting when a texture/leaf is rotated."
            };
            try
            {
                foreach(var option in options)
                foreach(var entry in new[]{("title",option.Header),("button",option.Button)})
                    textures[entry.Item2]=Bake(source,entry.Item2,entry.Item2.Asset=="Wood092"?90:0,
                        output,option.Id+"-"+entry.Item1,bakeLog);
                var leafFinish=new Finish("LeafSet029",Color.white,Ink,1,.60f,2.4f,.20f);
                foreach(int angle in new[]{-22,153})
                    leaves[angle]=Bake(source,leafFinish,angle,output,"ivy-"+angle,bakeLog,true);
                if(options.Any(o=>o.LitNavigation))
                {
                    string navigationSource=Environment.GetEnvironmentVariable("CAPSTONE_NAV_LEAF_SOURCE");
                    if(string.IsNullOrWhiteSpace(navigationSource))throw new InvalidOperationException("Set CAPSTONE_NAV_LEAF_SOURCE to the original LeafSet024 maps.");
                    navigationTexture=GardenNavigationLeafBake.Bake(navigationSource,output);
                }
                foreach(var size in new[]{new Vector2Int(780,1688),new Vector2Int(1200,2000)})
                {
                    Capture(report,size,null,textures,leaves,navigationTexture);
                    foreach(var option in options)Capture(report,size,option,textures,leaves,navigationTexture);
                }
                File.WriteAllLines(Path.Combine(output,"options.txt"),new[]{
                    "Baseline and alternatives use the same course-picker layout and synthetic state.",
                    "Backdrop, paper cards, navigation shelf, badge colors, spacing and event handlers are unchanged.",
                    "Wood/header decoration and their ink change by option; H uses the garden-care frame with the same LeafSet024 navigation texture as F.",
                    "All alternatives have normal/roughness lighting, individual color grading and a lit bevel; product Resources are unchanged.",
                    "Downloaded textures: ambientCG Wood093, Wood092, Paper006, LeafSet029; CC0.",
                    "This is an Editor preview, not an installed Android candidate."
                }.Concat(options.Select(o=>o.Id+": "+o.Description)));
            }
            catch(Exception error)
            {
                report.failed++;
                report.checks.Add(new FocusMissionValidation.CheckResult{name="Texture comparison",status="fail",detail=error.ToString()});
                throw;
            }
            finally
            {
                foreach(var texture in textures.Values)UnityEngine.Object.DestroyImmediate(texture);
                foreach(var leaf in leaves.Values)UnityEngine.Object.DestroyImmediate(leaf);
                if(navigationTexture!=null)UnityEngine.Object.DestroyImmediate(navigationTexture);
                File.WriteAllLines(Path.Combine(output,"material-bakes.txt"),bakeLog);
                File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
            }
            Debug.Log("GARDEN_TEXTURE_OPTIONS_PASS captures="+report.captures.Count);
        }

        static void Capture(FocusMissionValidation.Report report,Vector2Int size,Option option,Dictionary<Finish,Texture2D> textures,Dictionary<int,Texture2D> leaves,Texture2D navigationTexture)
        {
            using(var fixture=new FocusMissionValidation.Fixture(report,true,chooseCourse:false))
            {
                fixture.Resize(size.x,size.y);
                var debug=fixture.Ui.GetComponentsInChildren<Button>(false).FirstOrDefault(b=>b.name=="Focus.DebugLauncher");
                if(debug!=null)debug.gameObject.SetActive(false); // Match the non-development Android presentation in every image.
                Settle(fixture.Ui.transform.root);
                string before=StateCodec.Encode(fixture.Service.Snapshot);
                var surfaces=fixture.Ui.GetComponentsInChildren<GardenThemeSurface>(false)
                    .Where(s=>s.style==GardenThemeSurface.Style.Header || s.style==GardenThemeSurface.Style.Wood || s.style==GardenThemeSurface.Style.TabWood).ToArray();
                if(surfaces.Length<2)throw new InvalidOperationException("Expected a title and wood action on the actual course picker.");
                var originalRects=surfaces.Select(s=>s.rectTransform.rect).ToArray();
                var navigation=fixture.Ui.GetComponentsInChildren<MissionBottomNavigation>(false).Single();
                var navigationRects=navigation.GetComponentsInChildren<RectTransform>(true).ToDictionary(r=>r,r=>r.rect);
                var navigationInk=navigation.GetComponentsInChildren<Graphic>(true).ToDictionary(g=>g,g=>g.color);
                var selectedFaces=navigation.GetComponentsInChildren<GardenThemeSurface>(false)
                    .Where(s=>s.style==GardenThemeSurface.Style.Home).ToArray();
                var selectedTextures=selectedFaces.Select(s=>s.mainTexture).ToArray();
                var inkChecks=new Dictionary<Text,Color>();
                if(option!=null)
                    foreach(var surface in surfaces)Apply(surface,surface.style==GardenThemeSurface.Style.Header?option.Header:option.Button,textures,leaves,inkChecks);
                if(option!=null && (option.LitNavigation || option.GardenPrimaryNavigation))
                {
                    var inactive=navigation.GetComponentsInChildren<GardenThemeSurface>(false)
                        .Where(s=>s.style==GardenThemeSurface.Style.NavigationLeaf).ToArray();
                    if(inactive.Length!=2 || selectedFaces.Length!=1)throw new InvalidOperationException("Expected two inactive and one active navigation face.");
                    foreach(var surface in inactive)
                        if(option.GardenPrimaryNavigation)PreviewGardenPrimary(surface.transform.parent.GetComponent<Button>(),1f,navigationTexture);
                        else ApplyNavigation(surface,navigationTexture);
                }
                Canvas.ForceUpdateCanvases();
                string id=option==null?"00-baseline":option.Id;
                fixture.Capture(id+"-missions-"+size.x+"x"+size.y,true);
                foreach(var check in inkChecks)
                    if(!((Color32)check.Key.color).Equals((Color32)check.Value))
                        throw new InvalidOperationException("Preview text ink changed while rendering: "+check.Key.name);
                if(StateCodec.Encode(fixture.Service.Snapshot)!=before)
                    throw new InvalidOperationException("Material capture changed saved state.");
                for(int i=0;i<surfaces.Length;i++)
                    if(surfaces[i].rectTransform.rect!=originalRects[i])
                        throw new InvalidOperationException("Material replacement changed a control's layout.");
                foreach(var check in navigationRects)
                    if(check.Key.rect!=check.Value)throw new InvalidOperationException("Navigation layout changed.");
                foreach(var check in navigationInk)
                    if(!((Color32)check.Key.color).Equals((Color32)check.Value))throw new InvalidOperationException("Navigation icon, caption or badge color changed.");
                for(int i=0;i<selectedFaces.Length;i++)
                    if(!selectedFaces[i].enabled || selectedFaces[i].mainTexture!=selectedTextures[i])
                        throw new InvalidOperationException("Selected navigation face changed.");
                if(fixture.Ui.GetComponentsInChildren<GardenTextureOptionSurface>(false).Any(s=>s.raycastTarget))
                    throw new InvalidOperationException("A preview surface intercepts input.");
                report.passed++;
                report.checks.Add(new FocusMissionValidation.CheckResult{name=id+" "+size,status="pass",detail="Actual UI rendered; saved state, layout, navigation ink/selected face and nonblocking decoration preserved."});
            }
        }

        internal static void ApplyNavigation(GardenThemeSurface surface,Texture2D texture)
        {
            if(texture==null)throw new InvalidOperationException("Missing navigation leaf bake.");
            var owner=(RectTransform)surface.transform.parent;
            surface.enabled=false;
            var go=new GameObject("Normal-lit inactive navigation preview",typeof(RectTransform),typeof(CanvasRenderer),typeof(LayoutElement));
            go.hideFlags=HideFlags.DontSave;go.transform.SetParent(owner,false);go.transform.SetAsFirstSibling();
            go.GetComponent<LayoutElement>().ignoreLayout=true;
            var rect=(RectTransform)go.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var graphic=go.AddComponent<GardenTextureOptionSurface>();
            graphic.Source=texture;graphic.Basis=new Color32(62,82,46,255);graphic.Bevel=1.7f;
            graphic.CornerRadius=16;graphic.RadiusHeightRatio=.24f;graphic.CropCoverage=.94f;
            graphic.raycastTarget=false;graphic.SetAllDirty();
        }

        internal static void PreviewGardenPrimary(Button button,float zoom,Texture2D navigationTexture=null)
        {
            if(button==null)throw new InvalidOperationException("Missing garden-style preview button.");
            var previous=button.transform.Find("Normal-lit inactive navigation preview");
            if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            if(navigationTexture!=null)
            {
                var original=button.transform.Find("Garden theme surface")?.GetComponent<GardenThemeSurface>();
                if(original==null)throw new InvalidOperationException("Missing original navigation surface.");
                ApplyNavigation(original,navigationTexture);
                var face=button.GetComponentInChildren<GardenTextureOptionSurface>();
                face.GardenPrimaryFrame=true;face.SetAllDirty();
                return;
            }
            var surface=GardenThemeSurface.Apply(button.gameObject,GardenThemeSurface.Style.Primary);
            surface.enabled=true;
            var effect=surface.GetComponent<GardenTexturePreviewZoom>()??surface.gameObject.AddComponent<GardenTexturePreviewZoom>();
            effect.hideFlags=HideFlags.DontSave;effect.Zoom=zoom;
            surface.SetAllDirty();
        }

        static void Settle(Transform root)
        {
            for(int pass=0;pass<3;pass++)
            {
                Canvas.ForceUpdateCanvases();
                foreach(var safe in root.GetComponentsInChildren<PrototypePortraitLayout>(false))safe.Apply();
                foreach(var fit in root.GetComponentsInChildren<FigmaMissionCanvasFit>(false))fit.Fit();
                foreach(var page in root.GetComponentsInChildren<FocusMobilePageLayout>(false))page.Refresh();
                Canvas.ForceUpdateCanvases();
            }
        }

        static void Apply(GardenThemeSurface surface,Finish finish,Dictionary<Finish,Texture2D> textures,Dictionary<int,Texture2D> leaves,Dictionary<Text,Color> inkChecks)
        {
            bool header=surface.style==GardenThemeSurface.Style.Header;
            var owner=(RectTransform)surface.transform.parent;
            surface.enabled=false;
            var background=owner.GetComponent<Image>();if(background!=null)background.color=Color.clear;
            var go=new GameObject("Texture option preview",typeof(RectTransform),typeof(CanvasRenderer),typeof(LayoutElement));
            go.hideFlags=HideFlags.DontSave;go.transform.SetParent(owner,false);go.transform.SetAsFirstSibling();
            go.GetComponent<LayoutElement>().ignoreLayout=true;
            var rect=(RectTransform)go.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var graphic=go.AddComponent<GardenTextureOptionSurface>();
            graphic.Source=textures[finish];graphic.Basis=finish.Base;
            graphic.Bevel=finish.Asset.StartsWith("Wood",StringComparison.Ordinal)?2.8f:1.5f;
            graphic.Rotate=finish.Asset=="Wood092";graphic.raycastTarget=false;graphic.SetAllDirty();
            var labels=owner.GetComponentsInChildren<Text>(true).ToList();
            // FigmaHeader deliberately places its title beside the plaque, not
            // inside it. Match only same-parent labels centred on that plaque.
            if(header)
                labels.AddRange(owner.parent.GetComponentsInChildren<Text>(true).Where(text=>text.transform.parent==owner.parent
                    && owner.rect.Contains(owner.InverseTransformPoint(text.rectTransform.TransformPoint(text.rectTransform.rect.center)))));
            if(labels.Count==0)throw new InvalidOperationException("No label found for the preview surface: "+owner.name);
            foreach(var text in labels.Distinct())
            {
                text.color=finish.Ink;
                inkChecks[text]=finish.Ink;
                text.SetAllDirty();
            }
            if(finish.Leaves)
            {
                AddLeaf(owner,leaves[-22],true,header?46:39,header?0:1,header?-19:-17,header?9:7,-22);
                if(!header)AddLeaf(owner,leaves[153],false,42,3,18,-7,153);
            }
        }

        static void AddLeaf(RectTransform owner,Texture2D texture,bool right,float size,int cell,float x,float y,float angle)
        {
            var go=new GameObject("Preview ivy decoration",typeof(RectTransform),typeof(LayoutElement),typeof(RawImage));
            go.hideFlags=HideFlags.DontSave;go.transform.SetParent(owner,false);go.transform.SetSiblingIndex(1);
            go.GetComponent<LayoutElement>().ignoreLayout=true;
            var rect=(RectTransform)go.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(right?1:0,.5f);rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=new Vector2(size,size);rect.anchoredPosition=new Vector2(x,y);rect.localRotation=Quaternion.Euler(0,0,angle);
            var image=go.GetComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
            image.uvRect=new Rect((cell%3)/3f,1f-(cell/3+1)/3f,1f/3f,1f/3f);
            image.color=Color.white;
        }

        static Texture2D Read(string path)
        {
            var t=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!t.LoadImage(File.ReadAllBytes(path)))throw new InvalidOperationException("Could not decode material input: "+Path.GetFileName(path));
            t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;return t;
        }
        static float Luma(Color c)=>.2126f*c.r+.7152f*c.g+.0722f*c.b;
        static string Hash(string path)
        {
            using var sha=SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
        }

        static Texture2D Bake(string source,Finish finish,float rotation,string output,string id,List<string> log,bool leaf=false)
        {
            string stem=Path.Combine(source,finish.Asset,finish.Asset+"_1K-JPG_");
            var inputs=new List<Texture2D>();
            Texture2D result=null;
            try
            {
                Texture2D Load(string suffix)
                {
                    string path=stem+suffix+".jpg";
                    var t=Read(path);inputs.Add(t);
                    log.Add(id+" source "+finish.Asset+" "+suffix+" sha256="+Hash(path));
                    return t;
                }
                var color=Load("Color");var normal=Load("NormalGL");var rough=Load("Roughness");
                var mask=leaf?Load("Opacity"):null;
                if(inputs.Any(t=>t.width!=color.width || t.height!=color.height))
                    throw new InvalidOperationException("Material map dimensions differ: "+finish.Asset);
                var albedo=color.GetPixels();var normals=normal.GetPixels();var roughness=rough.GetPixels();
                var opacity=mask!=null?mask.GetPixels32():null;
                Color average=Color.clear;
                foreach(var c in albedo)average+=c/albedo.Length;
                float mean=Luma(average);
                var light=new Vector3(-.52f,.66f,.55f).normalized;
                var half=(light+Vector3.forward).normalized;
                var turn=Quaternion.Euler(0,0,rotation);
                float reference=.48f+.90f*light.z;
                float flatGloss=finish.Gloss*Mathf.Pow(Mathf.Max(0,Vector3.Dot(Vector3.forward,half)),18);
                var pixels=new Color32[albedo.Length];
                double normalEffect=0;float low=1,high=0;int clipped=0,count=0;
                for(int i=0;i<albedo.Length;i++)
                {
                    var c=albedo[i];float y=Luma(c);
                    Color graded;
                    if(leaf)
                    {
                        // Keep leaf variation, but remove the previous lime tint.
                        graded=Color.Lerp(new Color(y,y,y,1),c,finish.Chroma);
                        graded=new Color(graded.r*.98f,graded.g*.94f,graded.b*.90f,1);
                    }
                    else
                    {
                        // Recenter the photograph on each finish's chosen palette.
                        // Preserve luminance variation instead of covering it with a wash.
                        float detail=(y-mean)*finish.Contrast;
                        graded=new Color(
                            finish.Base.r+detail+((c.r-y)-(average.r-mean))*finish.Chroma,
                            finish.Base.g+detail+((c.g-y)-(average.g-mean))*finish.Chroma,
                            finish.Base.b+detail+((c.b-y)-(average.b-mean))*finish.Chroma,1);
                    }
                    graded=new Color(Mathf.Clamp01(graded.r),Mathf.Clamp01(graded.g),Mathf.Clamp01(graded.b),1);
                    var sample=normals[i];
                    var n=turn*new Vector3((sample.r*2-1)*finish.NormalStrength,
                        (sample.g*2-1)*finish.NormalStrength,sample.b*2-1).normalized;
                    float diffuse=(.48f+.90f*Mathf.Max(0,Vector3.Dot(n,light)))/reference;
                    float smooth=1-roughness[i].r;
                    float gloss=finish.Gloss*smooth*Mathf.Pow(Mathf.Max(0,Vector3.Dot(n,half)),18);
                    var linear=graded.linear;
                    var lit=new Color(linear.r*diffuse+gloss,linear.g*diffuse+gloss*.96f,linear.b*diffuse+gloss*.86f,1).gamma;
                    if(float.IsNaN(lit.r) || float.IsInfinity(lit.r) || float.IsNaN(lit.g) || float.IsInfinity(lit.g)
                        || float.IsNaN(lit.b) || float.IsInfinity(lit.b))throw new InvalidOperationException("Nonfinite material bake.");
                    lit.a=leaf?opacity[i].r/255f:1;
                    if(lit.a>.9f)
                    {
                        count++;float luminance=Luma(lit);low=Mathf.Min(low,luminance);high=Mathf.Max(high,luminance);
                        if(lit.r>=1 || lit.g>=1 || lit.b>=1)clipped++;
                        normalEffect+=Math.Abs(diffuse-1)+Math.Abs(gloss-flatGloss*smooth);
                    }
                    pixels[i]=lit;
                }
                // JPEG LoadImage uses RGB24. A separate RGBA32 texture preserves
                // opacity; the material atlas's white strip remains unlit white.
                result=new Texture2D(color.width,color.height+(leaf?0:32),TextureFormat.RGBA32,false);
                result.SetPixels32(0,0,color.width,color.height,pixels);
                if(!leaf)result.SetPixels(0,color.height,color.width,32,Enumerable.Repeat(Color.white,color.width*32).ToArray());
                result.Apply(false);result.wrapMode=TextureWrapMode.Clamp;result.filterMode=FilterMode.Bilinear;
                var actual=result.GetPixels32();
                if(leaf && (actual.Where((p,i)=>p.a!=opacity[i].r).Any() || !actual.Any(p=>p.a==0) || !actual.Any(p=>p.a==255)))
                    throw new InvalidOperationException("Leaf opacity changed during bake.");
                if(!leaf && actual.Skip(pixels.Length).Any(p=>!p.Equals(new Color32(255,255,255,255))))
                    throw new InvalidOperationException("Atlas white strip was shaded.");
                if(count==0 || normalEffect/count<.00001)throw new InvalidOperationException("Normal map had no measurable lighting contribution.");
                string folder=Path.Combine(output,"baked-materials");Directory.CreateDirectory(folder);
                string path=Path.Combine(folder,id+".png");File.WriteAllBytes(path,result.EncodeToPNG());
                log.Add(FormattableString.Invariant($"{id}: contrast={finish.Contrast}, chroma={finish.Chroma}, normal={finish.NormalStrength}, gloss={finish.Gloss}, rotation={rotation}, luma={low:F4}..{high:F4}, clippedChannelsPixels={(float)clipped/count:P2}, normalLightDeltaMean={normalEffect/count:F6}, sha256={Hash(path)}"));
                return result;
            }
            catch {if(result!=null)UnityEngine.Object.DestroyImmediate(result);throw;}
            finally {foreach(var t in inputs)UnityEngine.Object.DestroyImmediate(t);}
        }

    }

    // Crop only the photographed material UVs; leave the white atlas strip,
    // rounded frame, pointer states and existing button geometry untouched.
    public sealed class GardenTexturePreviewZoom : BaseMeshEffect
    {
        public float Zoom=1;
        public override void ModifyMesh(VertexHelper mesh)
        {
            if(!IsActive() || Zoom<=1)return;
            const float materialHeight=1024f/1056f;
            var pivot=new Vector2(.5f,materialHeight*.5f);
            var vertex=new UIVertex();
            for(int i=0;i<mesh.currentVertCount;i++)
            {
                mesh.PopulateUIVertex(ref vertex,i);
                if(vertex.uv0.y>=materialHeight)continue;
                vertex.uv0.x=pivot.x+(vertex.uv0.x-pivot.x)/Zoom;
                vertex.uv0.y=pivot.y+(vertex.uv0.y-pivot.y)/Zoom;
                mesh.SetUIVertex(vertex,i);
            }
        }
    }

    // Editor-only decoration: rounded geometry with preserved photographic grain.
    // Solid regions sample the atlas white strip; no custom shader or runtime asset.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GardenTextureOptionSurface : MaskableGraphic
    {
        public Texture2D Source;
        public Color Basis=Color.white;
        public float Bevel=2;
        public float CornerRadius=10, RadiusHeightRatio=.20f, CropCoverage=.72f;
        public bool Rotate;
        public bool GardenPrimaryFrame;
        public override Texture mainTexture=>Source!=null?Source:base.mainTexture;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;if(r.width<=0 || r.height<=0 || Source==null)return;
            if(GardenPrimaryFrame)
            {
                // Borrow only the garden-care outline and inset. The existing
                // navigation bake already contains its approved color and lighting.
                float unit=Mathf.Clamp(Mathf.Min(r.width,r.height)/68f,.35f,1.2f);
                float primaryRadius=Mathf.Min(16*unit,r.height*.24f);
                Rounded(mesh,new Rect(r.x,r.y-4*unit,r.width,r.height),primaryRadius,
                    new Color32(97,81,49,38),false,r);
                Rounded(mesh,r,primaryRadius,new Color32(41,73,53,255),false,r);
                var primaryFace=new Rect(r.x+2*unit,r.y+2*unit,r.width-4*unit,r.height-4*unit);
                var originalTextureFace=new Rect(r.x+Bevel,r.y+Bevel,r.width-2*Bevel,r.height-2*Bevel);
                Rounded(mesh,primaryFace,Mathf.Max(1,primaryRadius-2*unit),Color.white,true,originalTextureFace);
                return;
            }
            float radius=Mathf.Min(CornerRadius,r.height*RadiusHeightRatio);
            float depth=Bevel*1.1f;
            Rounded(mesh,new Rect(r.x-.7f,r.y-depth-1.5f,r.width+1.4f,r.height),radius+1,new Color32(70,60,39,19),false,r);
            Rounded(mesh,new Rect(r.x,r.y-depth-.5f,r.width,r.height),radius,new Color32(70,60,39,35),false,r);
            var edge=Basis*.66f;edge.a=1;
            Rounded(mesh,new Rect(r.x,r.y-depth,r.width,r.height),radius,edge,false,r);
            var face=new Rect(r.x+Bevel,r.y+Bevel,r.width-2*Bevel,r.height-2*Bevel);
            Rim(mesh,r,face,radius,Mathf.Max(0,radius-Bevel));
            Rounded(mesh,face,Mathf.Max(0,radius-Bevel),Color.white,true,face);
        }
        static Vector2 Perimeter(Rect r,float radius,int corner,int step)
        {
            var center=new Vector2(corner==0||corner==3?r.xMax-radius:r.xMin+radius,corner<2?r.yMax-radius:r.yMin+radius);
            float a=(corner*90+step*90f/8)*Mathf.Deg2Rad;
            return center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
        }
        void Rim(VertexHelper mesh,Rect outer,Rect inner,float outerRadius,float innerRadius)
        {
            const int count=36;int start=mesh.currentVertCount;
            for(int corner=0;corner<4;corner++)
            for(int i=0;i<=8;i++)
            {
                float a=(corner*90+i*90f/8)*Mathf.Deg2Rad;
                float facing=Vector2.Dot(new Vector2(Mathf.Cos(a),Mathf.Sin(a)),new Vector2(-.52f,.66f).normalized);
                float shade=.82f+.18f*facing;
                var tint=new Color(shade,shade,shade,1);
                Vertex(mesh,Perimeter(outer,outerRadius,corner,i),tint,true,inner,false);
                Vertex(mesh,Perimeter(inner,innerRadius,corner,i),Color.white,true,inner,false);
            }
            for(int i=0;i<count;i++)
            {
                int a=start+2*i,b=start+2*((i+1)%count);
                mesh.AddTriangle(a,b,b+1);mesh.AddTriangle(a,b+1,a+1);
            }
        }

        void Rounded(VertexHelper mesh,Rect r,float radius,Color tint,bool textured,Rect face)
        {
            const int steps=8,count=4*(steps+1);int start=mesh.currentVertCount;
            Vertex(mesh,r.center,tint,textured,face);
            for(int corner=0;corner<4;corner++)
            {
                var center=new Vector2(corner==0||corner==3?r.xMax-radius:r.xMin+radius,corner<2?r.yMax-radius:r.yMin+radius);
                for(int i=0;i<=steps;i++)
                {
                    float a=(corner*90+i*90f/steps)*Mathf.Deg2Rad;
                    Vertex(mesh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,tint,textured,face);
                }
            }
            for(int i=0;i<count;i++)mesh.AddTriangle(start,start+i+1,start+(i+1)%count+1);
        }
        void Vertex(VertexHelper mesh,Vector2 point,Color tint,bool textured,Rect face,bool faceLighting=true)
        {
            Vector2 uv=new Vector2(.5f,(Source.height-16f)/Source.height);
            if(textured)
            {
                if(faceLighting)
                {
                    float nx=Mathf.Clamp01((point.x-face.xMin)/face.width);
                    float ny=Mathf.Clamp01((point.y-face.yMin)/face.height);
                    float shade=.945f+.035f*ny+.02f*(1-nx);
                    tint=new Color(tint.r*shade,tint.g*shade,tint.b*shade,tint.a);
                }
                // Crop without stretching the source's physical grain aspect.
                float cropWidth=Rotate?Source.height-32:Source.width;
                float cropHeight=Rotate?Source.width:Source.height-32;
                float scale=Mathf.Min(cropWidth/face.width,cropHeight/face.height)*CropCoverage;
                Vector2 delta=(point-face.center)*scale;
                if(Rotate)delta=new Vector2(delta.y,-delta.x);
                uv=new Vector2(.5f+delta.x/Source.width,((Source.height-32)*.5f+delta.y)/Source.height);
            }
            mesh.AddVert(point,tint,uv);
        }
    }
}
