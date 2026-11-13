# Kenney garden sources

This directory contains selected, unmodified model sources from official Kenney downloads, retrieved on 2026-10-12. Each pack's original `License.txt` is retained. All three packs are Creative Commons Zero (CC0).

- [Nature Kit](https://kenney.nl/assets/nature-kit): floating island surface and rock underside, low perimeter planting, all four growth stages, flowers, flower bed and fence.
- [Furniture Kit](https://kenney.nl/assets/furniture-kit): the bench and low table in the rest corner.
- [Cube Pets](https://kenney.nl/assets/cube-pets): the authored visitor faces, bodies, legs, ears and tails.

The runtime prefabs are built by `CapstoneDesign.EditorTools.KenneyGardenBuilder.Build` into `Assets/Resources/GardenModels`. The builder applies a shared muted meadow/wood/cream palette, URP lit materials, real shadow casting/receiving, bounded proportions and species variants. Adaptations are project work; do not attribute the arrangements or changes to Kenney.

`GardenSurface` intersects the actual imported ground mesh. `GardenSceneModels` aligns the visible bounds to separate flower-bed, rest-corner and visitor positions, then places the minimum model height on that surface. Saved purchase and animal identifiers remain unchanged. The central growth footprint is reserved so mature flowers cannot intersect purchased furniture.

The original historical prototype authoring files are retained as project history. The integrated app scene uses the new Kenney garden prefab; runtime purchase previews and visitors use the same resources.

## Visible-model replacement inventory

| App object | Current source / adaptation |
| --- | --- |
| Floating island top | Nature `platform_grass`; upper surface leveled in a derived mesh for reliable foot contact |
| Floating rock underside | Nature `rock_largeA`, `rock_largeB`; scaled and recolored earth |
| Rim rocks and grass | Nature `rock_smallA`, `grass_leafs` |
| Seed, sprout, bud | Nature `crops_dirtSingle`, `crops_leafsStageA`, `flower_yellowC` |
| Mature chamomile | Nature `flower_yellowC` stem/leaves/center; six open faceted white petals and yellow center |
| Alternate prototype flower | Nature `flower_purpleC` |
| Purchased flower bed | Nature platform, low fence, red/purple flowers |
| Purchased rest corner | Furniture `bench`, `tableCoffeeSquare` |
| A01 cat / A03 fennec / A07 rabbit | Cube Pets cat / fox with sandy fur / bunny |
| A02 armadillo / A09 hedgehog | Cube Pets hog body with an earth shell / a distinct spined back |
| A04 chinchilla | Cube Pets koala, adjusted proportions |
| A05 small bird / A12 duck | Cube Pets chick, ivory plumage; duck has a broader bill |
| A06 red panda | Cube Pets fox with rust-colored fur, shortened ears and a ringed tail |
| A08 squirrel | Cube Pets beaver with a narrower upright tail |
| A10 capybara / A11 otter | Cube Pets beaver with removed incisors and different proportions; capybara has no tail, otter a narrow long tail |

These are stylized species adaptations, not new models published by Kenney. The build keeps each original source intact and writes modifications under `Assets/Art/KenneyGardenDerived`. The application no longer creates a generic sphere/capsule visitor for every animal.

Validation entry point: `CapstoneDesign.EditorTools.GardenEvolutionCapture.Capture`. It renders all 12 visitors, all four growth stages, six island viewing angles, morning/day/afternoon/sunset/night, and home at phone/landscape sizes. Placement validation checks real ground height under the center and four footprint corners, actual paw height, pairwise decoration clearance, maximum-growth central plant clearance, and lit shadow-enabled renderers. It also checks local-clock light directions, cloud particle movement after 60 seconds, and rendered pixels with directional shadows enabled versus disabled. Captures and synthetic validation output belong in ignored `artifacts/`.
