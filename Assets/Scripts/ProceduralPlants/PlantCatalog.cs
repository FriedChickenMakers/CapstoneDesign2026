using System;
using System.Collections.Generic;

namespace CapstoneDesign.Plants
{
    public enum GrowthCategory { Breath, Body, Movement, Senses, Reflection, SelfCare }
    public enum LeafShape { Divided, Spoon, Heart, Oval, Sword, Narrow, Compound, Strap, BroadBasal }
    public enum LeafArrangement { Spiral, Opposite, Rosette, Basal }
    public enum FlowerShape { Rays, Bell, FivePetals, Trumpet, Lavender, Hydrangea, Rose, Snowdrop }

    public sealed class SpeciesDefinition
    {
        public readonly string Id, Name, Meaning, PaletteA, PaletteB, Center;
        public readonly int Primary, Secondary, AxesMin, AxesMax, LeavesMin, LeavesMax,
            HeadsMin, HeadsMax, FloretsMin, FloretsMax, PetalsMin, PetalsMax, FruitCap;
        public readonly double HeightMin, HeightMax, LeafMin, LeafMax, AspectMin, AspectMax,
            EdgeMax, FlowerMin, FlowerMax;
        public readonly LeafShape Leaf;
        public readonly LeafArrangement Arrangement;
        public readonly FlowerShape Flower;
        internal SpeciesDefinition(string id, string name, string meaning, int primary, int secondary,
            double h0, double h1, int b0, int b1, int l0, int l1, int c0, int c1, int f0, int f1,
            int p0, int p1, int fruit, LeafShape leaf, LeafArrangement arrangement, FlowerShape flower,
            double ll0, double ll1, double w0, double w1, double edge, double d0, double d1,
            string a, string b, string center)
        {
            Id="species:"+id; Name=name; Meaning=meaning; Primary=primary; Secondary=secondary;
            HeightMin=h0; HeightMax=h1; AxesMin=b0; AxesMax=b1; LeavesMin=l0; LeavesMax=l1;
            HeadsMin=c0; HeadsMax=c1; FloretsMin=f0; FloretsMax=f1; PetalsMin=p0; PetalsMax=p1;
            FruitCap=fruit; Leaf=leaf; Arrangement=arrangement; Flower=flower;
            LeafMin=ll0; LeafMax=ll1; AspectMin=w0; AspectMax=w1; EdgeMax=edge;
            FlowerMin=d0; FlowerMax=d1; PaletteA=a; PaletteB=b; Center=center;
        }
        public bool Basal { get { return Arrangement==LeafArrangement.Basal || Arrangement==LeafArrangement.Rosette || Flower==FlowerShape.Lavender; } }
        public int[] ExampleCounts()
        {
            var counts=new[]{3,3,3,3,3,3}; counts[Primary]=48; counts[Secondary]=20; return counts;
        }
    }

    // v1 values are frozen. Introduce a new catalog version instead of editing old recipes.
    public static class PlantCatalog
    {
        public const int Version=1;
        public static readonly IReadOnlyList<SpeciesDefinition> All=Array.AsReadOnly(new[] {
            new SpeciesDefinition("chamomile","캐모마일","역경 속의 힘",0,5,.60,.85,2,4,10,18,3,6,1,1,10,16,0,LeafShape.Divided,LeafArrangement.Spiral,FlowerShape.Rays,.14,.22,.12,.22,.15,.15,.22,"FFF8E7","F3E9CA","D5AC4D"),
            new SpeciesDefinition("daisy","데이지","순수",3,1,.35,.55,2,4,6,12,3,6,1,1,12,20,0,LeafShape.Spoon,LeafArrangement.Rosette,FlowerShape.Rays,.16,.25,.30,.50,.04,.18,.26,"FFF8F1","F3CDD7","D9B85D"),
            new SpeciesDefinition("sunflower","해바라기","기쁨·희망",2,5,.85,1.15,1,2,6,10,1,3,1,1,16,24,0,LeafShape.Heart,LeafArrangement.Spiral,FlowerShape.Rays,.18,.28,.50,.75,.12,.24,.34,"F9DC85","E9B95F","806343"),
            new SpeciesDefinition("cosmos","코스모스","조화",2,3,.70,1.00,2,4,8,14,3,6,1,1,8,8,0,LeafShape.Divided,LeafArrangement.Opposite,FlowerShape.Rays,.14,.22,.15,.25,.15,.18,.25,"F2B7C7","D6B6DE","D8B45B"),
            new SpeciesDefinition("campanula","흰 캄파눌라","감사",5,4,.55,.80,2,3,8,14,2,4,2,3,5,5,0,LeafShape.Oval,LeafArrangement.Spiral,FlowerShape.Bell,.14,.22,.25,.40,.05,.09,.13,"FFFFFF","F3EDD9","E8E2D3"),
            new SpeciesDefinition("forget-me-not","물망초","진실한 사랑",4,3,.30,.50,2,4,8,14,2,4,3,5,5,5,0,LeafShape.Oval,LeafArrangement.Spiral,FlowerShape.FivePetals,.16,.24,.25,.40,0,.07,.11,"A7C9E8","BDD1EF","E7D78A"),
            new SpeciesDefinition("freesia","프리지아","우정",5,2,.60,.85,2,3,4,8,2,3,3,5,6,6,0,LeafShape.Sword,LeafArrangement.Basal,FlowerShape.Trumpet,.28,.40,.10,.18,0,.09,.14,"FFF0AE","F4DA79","E5C777"),
            new SpeciesDefinition("lavender","라벤더","헌신",0,1,.50,.75,3,5,12,20,3,5,5,8,5,5,0,LeafShape.Narrow,LeafArrangement.Opposite,FlowerShape.Lavender,.12,.20,.10,.18,0,.04,.07,"B5A5D8","8F89B6","82966B"),
            new SpeciesDefinition("hydrangea","수국","진심·감사",4,5,.60,.90,2,3,8,14,2,3,6,10,4,4,0,LeafShape.Oval,LeafArrangement.Opposite,FlowerShape.Hydrangea,.20,.30,.55,.80,.12,.06,.09,"D1B9E7","ECC6DA","C3AED9"),
            new SpeciesDefinition("pink-rose","분홍 장미","감사·존중",5,1,.65,.95,2,4,8,14,3,6,1,1,5,5,3,LeafShape.Compound,LeafArrangement.Spiral,FlowerShape.Rose,.16,.24,.35,.55,.12,.15,.22,"F1B6C8","D88FA8","D6BA6A"),
            new SpeciesDefinition("snowdrop","스노드롭","희망",1,0,.30,.45,2,3,4,6,2,4,1,1,6,6,0,LeafShape.Strap,LeafArrangement.Basal,FlowerShape.Snowdrop,.30,.45,.10,.16,0,.13,.18,"FFFFFF","EFEEDC","86A174"),
            new SpeciesDefinition("lily-of-the-valley","은방울꽃","다시 찾은 행복",1,3,.35,.55,2,3,4,6,2,3,4,6,6,6,5,LeafShape.BroadBasal,LeafArrangement.Basal,FlowerShape.Bell,.35,.50,.35,.55,0,.07,.11,"FFFDF2","EEE8D5","8C9F76")
        });
        public static SpeciesDefinition Get(string id)
        {
            foreach(var s in All) if(s.Id==id) return s;
            throw new ArgumentException("Unknown species ID.",nameof(id));
        }
    }
}
