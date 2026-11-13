using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    // Editor-only material study. Does not import or replace product Resources.
    public static class GardenNavigationLeafBake
    {
        const int Size = 1024, Strip = 32;
        static readonly Vector3 Key = new Vector3(-.52f, .66f, .55f).normalized;
        static readonly Color Basis = new Color32(84, 106, 77, 255);
        // Display-space grading follows the material bake, retaining the normal
        // lighting and restored saturation while increasing light/shadow separation.
        const float SaturationScale = 1.24f, ValueScale = .782f;
        const float ValueContrast = 1.30f, ValuePivot = .42f;

        struct Placement
        {
            public int Cell;
            public float X, Y, Size, Angle;
            public Placement(int cell, float x, float y, float size, float angle)
            { Cell=cell; X=x; Y=y; Size=size; Angle=angle; }
        }

        // Coordinates are in the resulting atlas's bottom-left origin.
        // Nine large leaves replace the old 36-leaf patchwork. No damaged cells 7/8.
        static readonly Placement[] Placements = {
            new Placement(2,-30,-30,960,-30), new Placement(0,530,-80,950,24),
            new Placement(4,1060,-20,950,-18), new Placement(1,-40,530,970,28),
            new Placement(5,480,460,960,-22), new Placement(3,1080,500,970,26),
            new Placement(4,-60,1040,970,-20), new Placement(2,520,1000,960,18),
            new Placement(0,1070,1040,960,-25)
        };

        /// <summary>Returns an owned RGBA atlas; the caller must destroy it.</summary>
        public static Texture2D Bake(string sourceFolder, string outputFolder)
        {
            if(string.IsNullOrWhiteSpace(sourceFolder) || string.IsNullOrWhiteSpace(outputFolder))
                throw new ArgumentException("Explicit source and output directories are required.");
            Directory.CreateDirectory(outputFolder);
            var loaded = new List<Texture2D>();
            var evidence = new List<string> {
                "Editor-only inactive navigation leaf material; no product Resources or UI state changed.",
                "Source: https://ambientcg.com/view?id=LeafSet024 ; CC0 1.0 Universal.",
                "Nine larger overlapping healthy leaf cells, deterministic placement; color and normal details softened.",
                "Albedo lighting in linear RGB; NormalGL/Roughness/Opacity sampled as raw data.",
                "Upper-left screen-space key (-0.52,0.66,0.55), normalized; ambient 0.36, diffuse 0.95.",
                "Normals rotated into screen space for each leaf; XY strength 1.65; normal blur radius 3 source pixels.",
                "Roughness-dependent restrained specular 0.035; soft shadow offset (+6,-8), radius 6 output pixels.",
                "Moss basis #546A4D, source chroma contribution 9%; soft luminance knee 0.14, asymptote 0.175 for white labels.",
                FormattableString.Invariant($"Final HSV grade: saturation x{SaturationScale}, value contrast x{ValueContrast} around {ValuePivot}, then value x{ValueScale}; hue, leaf layout and authored lighting unchanged."),
                "Atlas: 1024x1056 RGBA32, bottom 1024 image pixels, top 32 pixels exact opaque white."
            };
            Texture2D result=null;
            try
            {
                Texture2D Load(string kind)
                {
                    string path=Path.Combine(sourceFolder,"LeafSet024_1K-JPG_"+kind+".jpg");
                    using(var sha=SHA256.Create())
                        evidence.Add(kind+" SHA-256 "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());
                    var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    loaded.Add(texture);
                    if(!texture.LoadImage(File.ReadAllBytes(path)) || texture.width!=Size || texture.height!=Size)
                        throw new InvalidOperationException("Expected a 1024x1024 LeafSet024 map: "+kind);
                    return texture;
                }
                var albedo=Load("Color").GetPixels();
                var normal=Blur(Load("NormalGL").GetPixels(),3);
                var roughness=Load("Roughness").GetPixels();
                var opacity=Load("Opacity").GetPixels();
                var softAlbedo=Blur(albedo,1);
                float mean=0,weight=0;
                for(int i=0;i<albedo.Length;i++)
                {
                    mean+=Luma(albedo[i].linear)*opacity[i].r;
                    weight+=opacity[i].r;
                    albedo[i]=Color.Lerp(albedo[i],softAlbedo[i],.65f);
                }
                mean/=Mathf.Max(1,weight);
                var canvas=new Color[Size*Size];
                var baseLinear=Basis.linear;
                // A continuous interior-leaf underlayer fills small inter-leaf gaps.
                // It uses the same photographed material, not a flat green filler.
                for(int y=0;y<Size;y++)
                for(int x=0;x<Size;x++)
                {
                    float u=(.38f+x/(float)Size*.23f)/3f;
                    float v=(2+.26f+y/(float)Size*.38f)/3f;
                    canvas[y*Size+x]=Shade(Sample(albedo,u,v),Sample(normal,u,v),
                        Sample(roughness,u,v).r,0,baseLinear,mean)*.72f;
                    canvas[y*Size+x].a=1;
                }
                foreach(var p in Placements)
                {
                    evidence.Add("leaf cell="+p.Cell+" center=("+p.X+","+p.Y+") size="+p.Size+" angle="+p.Angle);
                    var alpha=new float[Size*Size];
                    float angle=p.Angle*Mathf.Deg2Rad,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
                    float bound=p.Size*.72f;
                    int minX=Mathf.Max(0,Mathf.FloorToInt(p.X-bound)),maxX=Mathf.Min(Size-1,Mathf.CeilToInt(p.X+bound));
                    int minY=Mathf.Max(0,Mathf.FloorToInt(p.Y-bound)),maxY=Mathf.Min(Size-1,Mathf.CeilToInt(p.Y+bound));
                    for(int y=minY;y<=maxY;y++)
                    for(int x=minX;x<=maxX;x++)
                    {
                        if(Texel(p,x,y,c,s,out float u,out float v))
                            alpha[y*Size+x]=Sample(opacity,u,v).r;
                    }
                    var shadow=Blur(alpha,6);
                    for(int y=0;y<Size;y++)
                    for(int x=0;x<Size;x++)
                    {
                        int sx=x-6,sy=y+8;
                        if(sx<0 || sx>=Size || sy<0 || sy>=Size)continue;
                        float amount=shadow[sy*Size+sx]*.23f;
                        canvas[y*Size+x]*=1-amount;
                        canvas[y*Size+x].a=1;
                    }
                    for(int y=minY;y<=maxY;y++)
                    for(int x=minX;x<=maxX;x++)
                    {
                        float a=alpha[y*Size+x];
                        if(a<=.001f || !Texel(p,x,y,c,s,out float u,out float v))continue;
                        var lit=Shade(Sample(albedo,u,v),Sample(normal,u,v),Sample(roughness,u,v).r,
                            p.Angle,baseLinear,mean);
                        canvas[y*Size+x]=Color.Lerp(canvas[y*Size+x],lit,a);
                        canvas[y*Size+x].a=1;
                    }
                }
                var pixels=new Color32[Size*(Size+Strip)];
                float lowest=1,highest=0;
                for(int i=0;i<canvas.Length;i++)
                {
                    var c=canvas[i].gamma;
                    Color.RGBToHSV(c,out float hue,out float saturation,out float value);
                    value=Mathf.Clamp01((value-ValuePivot)*ValueContrast+ValuePivot)*ValueScale;
                    c=Color.HSVToRGB(hue,Mathf.Clamp01(saturation*SaturationScale),value);
                    c.a=1;pixels[i]=c;
                    float y=Luma(c.linear);lowest=Mathf.Min(lowest,y);highest=Mathf.Max(highest,y);
                }
                for(int i=canvas.Length;i<pixels.Length;i++)pixels[i]=new Color32(255,255,255,255);
                result=new Texture2D(Size,Size+Strip,TextureFormat.RGBA32,false);
                result.name="Preview normal-lit inactive navigation leaves";
                result.SetPixels32(pixels);result.Apply(false);
                result.wrapMode=TextureWrapMode.Clamp;result.filterMode=FilterMode.Bilinear;
                var actual=result.GetPixels32();
                if(actual.Any(p=>p.a!=255) || actual.Skip(canvas.Length).Any(p=>p.r!=255 || p.g!=255 || p.b!=255))
                    throw new InvalidOperationException("Navigation atlas lost its opaque face or white decoration strip.");
                evidence.Add("Actual linear face luminance min="+lowest.ToString("F5")+" max="+highest.ToString("F5")+
                    "; minimum nominal white contrast="+(1.05f/(highest+.05f)).ToString("F2")+":1.");
                File.WriteAllBytes(Path.Combine(outputFolder,"navigation-leaf-lit-atlas.png"),result.EncodeToPNG());
                File.WriteAllLines(Path.Combine(outputFolder,"navigation-leaf-bake.txt"),evidence);
                return result;
            }
            catch
            {
                if(result!=null)UnityEngine.Object.DestroyImmediate(result);
                throw;
            }
            finally
            {
                foreach(var texture in loaded)UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static bool Texel(Placement p,float x,float y,float c,float s,out float u,out float v)
        {
            float dx=x-p.X,dy=y-p.Y;
            float lx=(c*dx+s*dy)/p.Size+.5f,ly=(-s*dx+c*dy)/p.Size+.5f;
            u=(p.Cell%3+lx)/3f;v=(2-p.Cell/3+ly)/3f;
            return lx>=.002f && lx<=.998f && ly>=.002f && ly<=.998f;
        }

        static Color Shade(Color source,Color mapped,float roughness,float angle,Color basis,float mean)
        {
            var linear=source.linear;float y=Luma(linear);
            var graded=basis*Mathf.Clamp(1+.75f*(y-mean)/Mathf.Max(.01f,mean),.55f,1.50f)
                +(linear-new Color(y,y,y,0))*.09f;
            var n=new Vector3((mapped.r*2-1)*1.65f,(mapped.g*2-1)*1.65f,mapped.b*2-1).normalized;
            n=Quaternion.Euler(0,0,angle)*n;
            float diffuse=Mathf.Max(0,Vector3.Dot(n,Key));
            float shade=(.36f+.95f*diffuse)/(.36f+.95f*Key.z);
            var half=(Key+Vector3.forward).normalized;
            float spec=.035f*(1-roughness)*Mathf.Pow(Mathf.Max(0,Vector3.Dot(n,half)),Mathf.Lerp(28,7,roughness));
            var lit=graded*shade+new Color(spec,spec,spec,0);
            lit.r=Mathf.Max(0,lit.r);lit.g=Mathf.Max(0,lit.g);lit.b=Mathf.Max(0,lit.b);
            float luminance=Luma(lit);
            if(luminance>.14f)
                lit*=(.14f+.035f*(1-Mathf.Exp(-(luminance-.14f)/.035f)))/luminance;
            lit.a=1;return lit;
        }

        static float Luma(Color c)=>.2126f*c.r+.7152f*c.g+.0722f*c.b;
        static Color Sample(Color[] pixels,float u,float v)
        {
            float x=Mathf.Clamp(u*Size-.5f,0,Size-1),y=Mathf.Clamp(v*Size-.5f,0,Size-1);
            int x0=(int)x,y0=(int)y,x1=Mathf.Min(Size-1,x0+1),y1=Mathf.Min(Size-1,y0+1);
            return Color.Lerp(Color.Lerp(pixels[y0*Size+x0],pixels[y0*Size+x1],x-x0),
                Color.Lerp(pixels[y1*Size+x0],pixels[y1*Size+x1],x-x0),y-y0);
        }

        static Color[] Blur(Color[] input,int radius)
        {
            var temp=new Color[input.Length];var result=new Color[input.Length];int span=radius*2+1;
            for(int y=0;y<Size;y++)
            {
                var sum=Color.clear;
                for(int k=-radius;k<=radius;k++)sum+=input[y*Size+Mathf.Clamp(k,0,Size-1)];
                for(int x=0;x<Size;x++)
                {
                    temp[y*Size+x]=sum/span;
                    sum+=input[y*Size+Mathf.Clamp(x+radius+1,0,Size-1)]-input[y*Size+Mathf.Clamp(x-radius,0,Size-1)];
                }
            }
            for(int x=0;x<Size;x++)
            {
                var sum=Color.clear;
                for(int k=-radius;k<=radius;k++)sum+=temp[Mathf.Clamp(k,0,Size-1)*Size+x];
                for(int y=0;y<Size;y++)
                {
                    result[y*Size+x]=sum/span;
                    sum+=temp[Mathf.Clamp(y+radius+1,0,Size-1)*Size+x]-temp[Mathf.Clamp(y-radius,0,Size-1)*Size+x];
                }
            }
            return result;
        }

        static float[] Blur(float[] input,int radius)
        {
            var temp=new float[input.Length];var result=new float[input.Length];int span=radius*2+1;
            for(int y=0;y<Size;y++)
            {
                float sum=0;
                for(int k=-radius;k<=radius;k++)sum+=input[y*Size+Mathf.Clamp(k,0,Size-1)];
                for(int x=0;x<Size;x++)
                {
                    temp[y*Size+x]=sum/span;
                    sum+=input[y*Size+Mathf.Clamp(x+radius+1,0,Size-1)]-input[y*Size+Mathf.Clamp(x-radius,0,Size-1)];
                }
            }
            for(int x=0;x<Size;x++)
            {
                float sum=0;
                for(int k=-radius;k<=radius;k++)sum+=temp[Mathf.Clamp(k,0,Size-1)*Size+x];
                for(int y=0;y<Size;y++)
                {
                    result[y*Size+x]=Mathf.Clamp01(sum/span);
                    sum+=temp[Mathf.Clamp(y+radius+1,0,Size-1)*Size+x]-temp[Mathf.Clamp(y-radius,0,Size-1)*Size+x];
                }
            }
            return result;
        }
    }
}
