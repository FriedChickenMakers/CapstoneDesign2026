# Kenney streetlamp source

Selected `lantern.obj`, `lantern.mtl` and `Textures/colormap.png` from [Kenney Holiday Kit 2.0](https://kenney.nl/assets/holiday-kit), downloaded from the official Kenney site on 2026-10-13. These source files are unmodified. The original `License.txt` is retained; the pack is released under [Creative Commons Zero (CC0)](https://creativecommons.org/publicdomain/zero/1.0/). Created and distributed by Kenney (www.kenney.nl).

`CapstoneDesign.EditorTools.StreetLampAssetBuilder.Build` derives the runtime prefab at `Assets/Resources/GardenModels/StreetLamp.prefab`. It retains the authored low-poly silhouette and triangles, separates the existing glass triangles by their palette UVs, applies project URP materials, and normalizes the height to 1.10 scene units. The prefab has a `Bulb` anchor in the glass center. Runtime code supplies the actual point lights and switches the glass emission with the garden clock.

The original model has 344 triangles, including 24 glass triangles. The generated frame and glass meshes are project adaptations, stored separately under `Assets/Art/StreetLampDerived`; the lighting and material changes are not authored by Kenney.
