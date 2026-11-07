# Shared pure-C# source set for state/mission/plant contracts. No Unity or live data.
param([Parameter(Mandatory=$true)][string]$ProjectRoot)
$runtime = @(
    'LocalGardenState', 'FocusMissionCatalog', 'FocusMissionCatalog.TextureHistory',
    'FocusAdditionalRecommendation', 'FocusDrawingData', 'GardenPlantAppearance',
    'CameraMissionPolicy', 'WeeklyPlantState', 'CombinedPlantCollection',
    'MindfulnessContent', 'MbctContent'
)
foreach ($name in $runtime) { Join-Path $ProjectRoot ('Assets/Scripts/Runtime/' + $name + '.cs') }
foreach ($name in @('Catalog','Math','Recipe','Generator','Geometry','FlowerLayout','MemoryGeometry')) {
    Join-Path $ProjectRoot ('Assets/Scripts/ProceduralPlants/Plant' + $name + '.cs')
}
