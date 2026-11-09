using System;
using System.Linq;
using CapstoneDesign.Plants;

static class MemoryLeafAffordanceTests
{
    static int checks;
    static void Check(bool condition,string message) {checks++;if(!condition)throw new Exception(message);}
    static void Run()
    {
        foreach(int version in new[]{1,2,6})
        foreach(var species in PlantCatalog.All)
        foreach(int step in new[]{0,9,21})
        {
            var recipe=PlantGenerator.Create("synthetic-memory-affordance",species.Id,21,PlantGrowthMode.MissionCompletions);
            recipe.MeshVersion=version;recipe.Counts=species.ExampleCounts();
            for(int i=0;i<step;i++)recipe=PlantGenerator.Advance(recipe);
            string before=PlantCodec.Encode(recipe);
            var ordinary=PlantGeometry.Build(recipe);
            var memories=Enumerable.Range(0,43).Select(i=>new PlantMemoryLeaf{OrganId="synthetic-memory-"+i,Slot=i,Linked=true}).ToArray();
            for(int page=0;page<3;page++)
            {
                var linked=PlantMemoryGeometry.Build(recipe,memories,page);
                int count=ordinary.Vertices.Count;
                Check(linked.Vertices.Take(count).SequenceEqual(ordinary.Vertices),"Base plant vertex prefix changed");
                Check(linked.Normals.Take(count).SequenceEqual(ordinary.Normals),"Base plant normal prefix changed");
                Check(linked.Colors.Take(count).SequenceEqual(ordinary.Colors),"Base plant colour prefix changed");
                Check(linked.Organs.Select(o=>o.OrganId).SequenceEqual(memories.Skip(page*21).Take(21).Select(m=>m.OrganId)),"Memory identity/page changed");
                Check(linked.TriangleCount<=8000 && linked.Vertices.All(v=>PlantGenerator.InEnvelope(v)),"Memory affordance exceeded geometry bounds");
                Check(linked.Normals.All(n=>Math.Abs(n.Length-1)<1e-8),"Memory affordance has nonunit normals");
                Check(PlantCodec.Encode(recipe)==before,"Rendering modified the recipe");
                foreach(var memory in memories)memory.Linked=false;
                var unlinked=PlantMemoryGeometry.Build(recipe,memories,page);
                Check(linked.Vertices.SequenceEqual(unlinked.Vertices) && linked.Normals.SequenceEqual(unlinked.Normals),"Deleting a record moved its leaf/vein");
                Check(linked.Organs.Select(o=>o.Center).SequenceEqual(unlinked.Organs.Select(o=>o.Center)),"Record deletion moved attachment centres");
                Check(unlinked.Organs.All(o=>!o.Selectable),"Unlinked leaf remained interactive");
                Check(!linked.Colors.SequenceEqual(unlinked.Colors),"Linked records have no visual distinction");
                foreach(var memory in memories)memory.Linked=true;
                var rebuilt=PlantMemoryGeometry.Build(PlantCodec.Decode<PlantRecipe>(before),memories,page);
                Check(rebuilt.Vertices.SequenceEqual(linked.Vertices) && rebuilt.Colors.SequenceEqual(linked.Colors),"Re-entry changed memory appearance");
            }
        }
    }
    public static int Main()
    {
        try {Run();Console.WriteLine("Memory leaf affordance PASS: "+checks);return 0;}
        catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}
    }
}
