using System;
using CapstoneDesign.PlantTests;
class PlantGenerationTests
{
    static int Main()
    {
        try { Console.WriteLine(PlantContractChecks.Run());return 0; }
        catch(Exception e) { Console.Error.WriteLine(e);return 1; }
    }
}
