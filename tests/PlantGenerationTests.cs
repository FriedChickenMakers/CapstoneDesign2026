using System;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.PlantTests;
class PlantGenerationTests
{
    static int previewAssertions;
    static void Check(bool condition,string message)
    {previewAssertions++;if(!condition)throw new Exception(message);}
    static void CheckSpeciesPreview()
    {
        foreach(var counts in new[]{new[]{4,0,0,0,0,2},new[]{1,1,1,1,1,1}})
        {
            var seed=PlantGenerator.Create("synthetic-preview-owner","synthetic-preview-plant");
            seed.Counts=(int[])counts.Clone();string before=PlantCodec.Encode(seed);
            string species=PlantGenerator.SpeciesFor(seed);
            Check(PlantCatalog.All.Any(s=>s.Id==species),"Preview species is absent from the catalog.");
            Check(species==PlantGenerator.SpeciesFor(seed),"Preview selection is not stable for the same recipe.");
            Check(PlantCodec.Encode(seed)==before&&seed.SpeciesId==null,"Preview mutated or prematurely locked the seed.");
            var grown=seed;
            while(grown.GrowthStep<grown.DifferentiationStep)grown=PlantGenerator.Advance(grown);
            Check(grown.SpeciesId==species,"Preview differs from the species actually selected at differentiation.");
            Check(PlantCodec.Encode(seed)==before,"Advancing the preview changed the original seed.");
            grown.Counts=new[]{0,0,0,9,0,0};string lockedBefore=PlantCodec.Encode(grown);
            Check(PlantGenerator.SpeciesFor(grown)==species,"Later activity counts replaced an already selected species.");
            Check(PlantCodec.Encode(grown)==lockedBefore,"Reading the selected species mutated the grown plant.");
        }
        bool rejected=false;try{PlantGenerator.SpeciesFor(null);}catch(ArgumentNullException){rejected=true;}
        Check(rejected,"A missing recipe was silently assigned a species.");
    }
    static int Main()
    {
        try { Console.WriteLine(PlantContractChecks.Run());CheckSpeciesPreview();Console.WriteLine(previewAssertions+" species preview assertions passed.");return 0; }
        catch(Exception e) { Console.Error.WriteLine(e);return 1; }
    }
}
