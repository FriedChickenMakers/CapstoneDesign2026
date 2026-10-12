using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CapstoneDesign.Plants
{
    [Serializable] public struct PlantVector
    {
        public double X,Y,Z;
        public PlantVector(double x,double y,double z) { X=x;Y=y;Z=z; }
        public static PlantVector Up { get { return new PlantVector(0,1,0); } }
        public double Length { get { return Math.Sqrt(X*X+Y*Y+Z*Z); } }
        public PlantVector Normalized { get { return Length<1e-12 ? Up : this/Length; } }
        public static PlantVector operator +(PlantVector a,PlantVector b) { return new PlantVector(a.X+b.X,a.Y+b.Y,a.Z+b.Z); }
        public static PlantVector operator -(PlantVector a,PlantVector b) { return new PlantVector(a.X-b.X,a.Y-b.Y,a.Z-b.Z); }
        public static PlantVector operator *(PlantVector a,double b) { return new PlantVector(a.X*b,a.Y*b,a.Z*b); }
        public static PlantVector operator /(PlantVector a,double b) { return a*(1/b); }
        public static PlantVector Cross(PlantVector a,PlantVector b) { return new PlantVector(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X); }
        public static PlantVector Direction(double degrees,double up=0) { double a=degrees*Math.PI/180; return new PlantVector(Math.Cos(a),up,Math.Sin(a)).Normalized; }
    }
    // All stored colors and mesh colors are LINEAR RGB. Shader converts only when needed.
    [Serializable] public struct PlantColor
    {
        public double R,G,B;
        public PlantColor(double r,double g,double b) { R=r;G=g;B=b; }
        public static PlantColor Hex(string hex)
        {
            return new PlantColor(Linear(Convert.ToInt32(hex.Substring(0,2),16)/255.0),
                Linear(Convert.ToInt32(hex.Substring(2,2),16)/255.0),Linear(Convert.ToInt32(hex.Substring(4,2),16)/255.0));
        }
        static double Linear(double x) { return x<=.04045 ? x/12.92 : Math.Pow((x+.055)/1.055,2.4); }
        public static PlantColor Mix(PlantColor a,PlantColor b,double t) { return new PlantColor(PlantMath.Lerp(a.R,b.R,t),PlantMath.Lerp(a.G,b.G,t),PlantMath.Lerp(a.B,b.B,t)); }
        public PlantColor Brightness(double b) { return new PlantColor(PlantMath.Clip(R*b),PlantMath.Clip(G*b),PlantMath.Clip(B*b)); }
    }
    public static class PlantMath
    {
        public static double Clip(double x) { return Math.Max(0,Math.Min(1,x)); }
        public static double Lerp(double a,double b,double t) { return a+(b-a)*t; }
        public static int Integer(int a,int b,double z) { return Math.Min(b,a+(int)Math.Floor((b-a+1)*z)); }
        public static double Smooth(double g,double a,double b) { double x=Clip((g-a)/(b-a)); return x*x*(3-2*x); }
        public static bool Finite(double x) { return !double.IsNaN(x)&&!double.IsInfinity(x); }
        public static double Birth(int index,int count,double a,double b)
        {
            if(index<=0) return a;
            if(index>=count) return b;
            double target=(double)index/count,lo=0,hi=1;
            for(int k=0;k<60;k++) { double m=(lo+hi)/2; if(m*m*(3-2*m)<target) lo=m; else hi=m; }
            return Lerp(a,b,(lo+hi)/2);
        }
        public static string Number(int i) { return i.ToString(CultureInfo.InvariantCulture); }
        public static string Hash(params string[] fields)
        {
            using(var bytes=new MemoryStream())
            {
                foreach(string field in fields)
                {
                    byte[] b=Encoding.UTF8.GetBytes(field??throw new ArgumentNullException(nameof(fields)));
                    int n=b.Length; bytes.WriteByte((byte)(n>>24));bytes.WriteByte((byte)(n>>16));bytes.WriteByte((byte)(n>>8));bytes.WriteByte((byte)n);
                    bytes.Write(b,0,b.Length);
                }
                using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        public static double U(string seed,string node,string tag)
        { return uint.Parse(Hash(seed,node,tag).Substring(0,8),NumberStyles.HexNumber,CultureInfo.InvariantCulture)/4294967296.0; }
        public static double[] Profile(int[] counts)
        {
            if(counts==null||counts.Length!=6) throw new ArgumentException("Six activity counts required.");
            long n=0; foreach(int c in counts) { if(c<0) throw new ArgumentException("Negative activity count."); n+=c; }
            var p=new double[6]; for(int i=0;i<6;i++) p[i]=(counts[i]+1.0)/(n+6.0); return p;
        }
        public static double Z(double[] p,int a,int b,string seed,string node,string tag)
        { return Clip(.5+.6*(p[a]-p[b])+.2*(2*U(seed,node,tag)-1)); }
    }
}
