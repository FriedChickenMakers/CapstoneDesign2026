using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Two product photographs share the available portrait height. Text and
    // touch targets keep their size; only photograph whitespace flexes.
    [ExecuteAlways]
    public sealed class GardenShopLayout : MonoBehaviour
    {
        public sealed class Product { public RectTransform Root, Photo, Title, Status, Price; }
        public Product[] Products;
        public Text Coins, Notice;
        public RectTransform CoinIcon, Back;

        void OnEnable() { Canvas.preWillRenderCanvases += Refresh; }
        void OnDisable() { Canvas.preWillRenderCanvases -= Refresh; }

        public void Refresh()
        {
            if (Products == null || Back == null) return;
            float height = ((RectTransform)transform).rect.height;
            float errorHeight = Notice != null && Notice.gameObject.activeSelf ? 44 : 0;
            float top = height < 760 ? 152 : 174;
            float productHeight = (height - top - 76 - errorHeight) * .5f;
            const float width = 354, gap = 12;
            for (int i = 0; i < Products.Length; i++)
            {
                var product = Products[i];
                FigmaMissionUi.SetRect(product.Root, 18, top + i * (productHeight + gap), width, productHeight);
                float photoHeight = Mathf.Min(165, productHeight - 88);
                float photoWidth = photoHeight * 2;
                FigmaMissionUi.SetRect(product.Photo, (width - photoWidth) * .5f,
                    12 + Mathf.Max(0, (productHeight - 88 - photoHeight) * .5f), photoWidth, photoHeight);
                FigmaMissionUi.SetRect(product.Title, 18, productHeight - 66, 172, 32);
                FigmaMissionUi.SetRect(product.Status, 18, productHeight - 32, 172, 20);
                FigmaMissionUi.SetRect(product.Price, width - 138, productHeight - 62, 120, 44);
            }
            FigmaMissionUi.SetRect(Back, 131, height - 56, 128, 48);
            if (Notice != null) FigmaMissionUi.SetRect(Notice.rectTransform, 24, height - 56 - errorHeight, 342, errorHeight);
            if (Coins != null && CoinIcon != null)
            {
                Coins.fontSize = 34;
                while (Coins.preferredWidth > 292 && Coins.fontSize > 22) Coins.fontSize--;
                float textWidth = Mathf.Min(292, Coins.preferredWidth + 4), total = textWidth + 42;
                FigmaMissionUi.SetRect(CoinIcon, (390 - total) * .5f, 109, 34, 34);
                FigmaMissionUi.SetRect(Coins.rectTransform, (390 - total) * .5f + 42, 101, textWidth, 48);
            }
        }
    }
}
