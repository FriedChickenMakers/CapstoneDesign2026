using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CapstoneDesign.Runtime.LocalState
{
    public struct FocusDrawingPoint
    {
        public int X,Y;
        public FocusDrawingPoint(int x,int y){X=Math.Max(0,Math.Min(63,x));Y=Math.Max(0,Math.Min(63,y));}
    }
    // Compact local stroke data fits the same 500-character private answer storage as text.
    public static class FocusDrawingData
    {
        public const int MaximumPoints=210,MaximumStrokes=30;
        const string Digits="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        public static string Encode(IReadOnlyList<List<FocusDrawingPoint>> strokes)
        {
            if(strokes==null||strokes.Count==0)return "";
            var value=new StringBuilder("DRAW1|");int count=0;
            foreach(var stroke in strokes.Take(MaximumStrokes))
            {
                if(stroke==null||stroke.Count==0)continue;
                if(value.Length>6)value.Append('.');
                foreach(var point in stroke)
                {
                    if(count++>=MaximumPoints)return value.ToString();
                    value.Append(Digits[Math.Max(0,Math.Min(63,point.X))]);value.Append(Digits[Math.Max(0,Math.Min(63,point.Y))]);
                }
            }
            return value.Length==6?"":value.ToString();
        }
        public static List<List<FocusDrawingPoint>> Decode(string value)
        {
            var result=new List<List<FocusDrawingPoint>>();
            if(string.IsNullOrEmpty(value)||!value.StartsWith("DRAW1|",StringComparison.Ordinal)||value.Length>500)return result;
            int count=0;
            foreach(var part in value.Substring(6).Split('.'))
            {
                if(part.Length==0)continue;if(part.Length%2!=0||result.Count>=MaximumStrokes)return new List<List<FocusDrawingPoint>>();
                var stroke=new List<FocusDrawingPoint>();
                for(int i=0;i<part.Length;i+=2){int x=Digits.IndexOf(part[i]),y=Digits.IndexOf(part[i+1]);if(x<0||y<0||++count>MaximumPoints)return new List<List<FocusDrawingPoint>>();stroke.Add(new FocusDrawingPoint(x,y));}
                result.Add(stroke);
            }
            return result;
        }
    }
}
