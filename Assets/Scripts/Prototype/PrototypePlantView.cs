using UnityEngine;

namespace CapstoneDesign.Prototype
{
    public sealed class PrototypePlantView : MonoBehaviour
    {
        public GameObject seed;
        public GameObject sprout;
        public Transform bud;
        public Transform blossomAnchor;
        public GameObject chamomileFlower;
        public GameObject hydrangeaFlower;

        public void Render(PrototypeSession session, PrototypeDefinition definition)
        {
            RenderVisual(session.Stage, session.FinalFlower?.shape);
        }

        // The integrated home uses the saved garden state, while the standalone
        // prototype can continue to render its own temporary session.
        public void RenderVisual(int stage, FlowerShape? flower)
        {
            seed.SetActive(stage == 0);
            sprout.SetActive(stage == 1);
            bud.gameObject.SetActive(stage == 2);
            blossomAnchor.gameObject.SetActive(stage >= 3);
            chamomileFlower.SetActive(stage >= 3 && flower == FlowerShape.Chamomile);
            hydrangeaFlower.SetActive(stage >= 3 && flower == FlowerShape.Hydrangea);
        }
    }
}
