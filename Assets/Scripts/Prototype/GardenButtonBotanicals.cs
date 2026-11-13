using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // Kept as a compatibility entry point for the existing screen builders.
    // Larger leaves, timber depth and state feedback now belong to the shared surface.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GardenButtonBotanicals : MaskableGraphic
    {
        public static void Apply(Button button, bool visible)
        {
            if (button == null) return;
            Transform legacy = button.transform.Find("Botanical button edge");
            if (legacy != null) legacy.gameObject.SetActive(false);
            if (visible) GardenThemeSurface.Apply(button.gameObject, GardenThemeSurface.Style.Primary);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            // Previously serialized edge components remain harmless during migration.
            mesh.Clear();
        }
    }
}
