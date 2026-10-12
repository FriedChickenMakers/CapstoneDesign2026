using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace CapstoneDesign.Plants
{
    public enum PlantGrowthMode { NutrientPurchases, MissionCompletions }
    [Serializable] public sealed class AxisRecipe
    {
        public int Parent=-1;
        public double Birth,AttachT,Length,Azimuth,Spread;
        public PlantVector Anchor;
    }
    [Serializable] public sealed class LeafRecipe
    {
        public int Parent;
        public double Birth,Length,Aspect,Edge,Angle,Attachment,Scale=1;
        public PlantVector Anchor,Direction;
    }
    [Serializable] public sealed class HeadRecipe
    {
        public bool Terminal;
        public int Parent,Florets,Petals;
        public double Birth,Diameter,Attachment,Scale=1;
        public PlantVector Anchor,Tip;
        public PlantColor Color;
    }
    [Serializable] public sealed class FruitRecipe
    {
        public int Head,Floret,EvaluationStep;
        public bool Present;
        public double Radius;
    }
    [Serializable] public sealed class PlantRecipe
    {
        // Initializers retain v1 semantics when loading XML without these new fields.
        public int MaxGrowthSteps=12;
        public PlantGrowthMode GrowthMode=PlantGrowthMode.NutrientPurchases;
        public int GeneratorVersion=1,CatalogVersion=1,ProfileVersion=1,GrowthStep;
        // Missing in old XML: preserve the original flat geometry, including v2 growth.
        public int MeshVersion=1;
        public string InstanceId,BaseSeed,SpeciesId;
        public int[] Counts=new int[6];
        public double[] LockedProfile;
        public double Height,Radius,Bend,Spread,Heading,LeafPhase;
        public int TargetAxes,TargetLeaves,TargetHeads;
        public List<AxisRecipe> Axes=new List<AxisRecipe>();
        // These contain only species-specific leaves. Two fixed cotyledons are implicit.
        public List<LeafRecipe> Leaves=new List<LeafRecipe>();
        public List<HeadRecipe> Heads=new List<HeadRecipe>();
        public List<FruitRecipe> Fruits=new List<FruitRecipe>();
        public bool Mature { get { return GrowthStep==MaxGrowthSteps; } }
        public double Growth { get { return (double)GrowthStep/MaxGrowthSteps; } }
        public int DifferentiationStep { get { return (MaxGrowthSteps+2)/3; } }
    }
    public static class PlantCodec
    {
        sealed class Utf8Writer : StringWriter
        {
            public Utf8Writer() : base(System.Globalization.CultureInfo.InvariantCulture) { }
            public override System.Text.Encoding Encoding { get { return System.Text.Encoding.UTF8; } }
        }
        public static string Encode<T>(T value)
        { using(var writer=new Utf8Writer()) { new XmlSerializer(typeof(T)).Serialize(writer,value);return writer.ToString(); } }
        public static T Decode<T>(string xml)
        {
            var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16*1024*1024 };
            using(var input=new StringReader(xml)) using(var reader=XmlReader.Create(input,settings)) return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
        }
        public static T Clone<T>(T value) { return Decode<T>(Encode(value)); }
    }
}
