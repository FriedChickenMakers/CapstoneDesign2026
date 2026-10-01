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
            seed.SetActive(session.Stage == 0);
            sprout.SetActive(session.Stage == 1);
            bud.gameObject.SetActive(session.Stage == 2);
            blossomAnchor.gameObject.SetActive(session.Stage == 3);
            chamomileFlower.SetActive(session.FinalFlower?.shape == FlowerShape.Chamomile);
            hydrangeaFlower.SetActive(session.FinalFlower?.shape == FlowerShape.Hydrangea);
        }
    }
}
