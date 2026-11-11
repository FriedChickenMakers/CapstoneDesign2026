using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        public void ShowShop()
        {
            if (!opened) { ShowHome(); return; }
            ClearGhost();
            var root = BeginFigmaPage("shop", ShowShop, FigmaMissionUi.Background);
            root.GetComponent<FocusMobilePageLayout>().FixedContent = true;
            var state = Service.Snapshot;
            var heading = FigmaMissionUi.Label(root, "정원 상점", 18, 20, 354, 48, 32,
                FigmaMissionUi.Ink, true, TextAnchor.MiddleCenter);
            heading.name = "Shop.Title";
            FigmaMissionUi.Label(root, "보유 코인", 18, 78, 354, 24, 14,
                FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter).name = "Shop.BalanceLabel";
            var coins = FigmaMissionUi.Label(root, state.Coins.ToString("N0") + " 코인", 18, 104, 354, 48, 34,
                new Color32(110, 77, 35, 255), true, TextAnchor.MiddleCenter);
            coins.name = "Shop.Coins";
            var coin = FigmaMissionUi.Rect(root, "Shop.CoinIcon", 60, 109, 34, 34).gameObject.AddComponent<PrototypeUiIcon>();
            coin.symbol = PrototypeUiIcon.Symbol.Coin;
            coin.raycastTarget = false;

            var products = new[]
            {
                CreateShopProduct(root, state, "E01", "left", "flower-bed"),
                CreateShopProduct(root, state, "E02", "right", "rest-corner")
            };
            var back = FigmaMissionUi.Button(root, "돌아가기", 131, 784, 128, 48,
                GoGarden, Color.clear, FigmaMissionUi.Green, 15);
            back.name = "Shop.Back";
            var layout = root.gameObject.AddComponent<GardenShopLayout>();
            layout.Coins = coins;
            layout.CoinIcon = coin.rectTransform;
            layout.Products = products;
            layout.Back = (RectTransform)back.transform;
            layout.Notice = notice;
            layout.Refresh();
        }

        GardenShopLayout.Product CreateShopProduct(RectTransform root, GardenState state, string code, string slot, string image)
        {
            string id = "environment:" + code;
            bool owned = state.Environments.Any(item => item.EnvironmentId == id || item.Slot == slot);
            var button = FigmaMissionUi.Button(root, "", 18, 174, 354, 288,
                () => { if (root != null && screen == "shop" && root.gameObject == view) PreviewEnvironment(id); },
                Color.clear, FigmaMissionUi.Ink);
            button.name = "Shop.Product." + code;
            button.GetComponentInChildren<Text>().gameObject.SetActive(false);
            GardenThemeSurface.Apply(button.gameObject, GardenThemeSurface.Style.Paper);
            var photo = FigmaMissionUi.Asset(button.transform, "UI/Shop/" + image, 12, 12, 330, 165);
            photo.name = "Shop.Photo";
            var title = FigmaMissionUi.Label(button.transform, MindfulnessContent.FindGardenContent(id).title,
                18, 222, 170, 32, 23, FigmaMissionUi.Ink, true, TextAnchor.MiddleLeft);
            title.name = "Shop.ProductTitle";
            var status = FigmaMissionUi.Label(button.transform, owned ? "보유 중" : state.Coins < Balance.EnvironmentCost ? "코인 부족" : "",
                18, 254, 170, 20, 12, owned ? FigmaMissionUi.Green : FigmaMissionUi.Brown);
            status.name = "Shop.Status";
            var price = FigmaMissionUi.Box(button.transform, "Shop.PriceFace", 216, 224, 120, 44,
                FigmaMissionUi.Green, 12);
            FigmaMissionUi.Label(price, Balance.EnvironmentCost.ToString("N0") + " 코인", 0, 0, 120, 44,
                18, Color.white, true, TextAnchor.MiddleCenter).name = "Shop.Price";
            return new GardenShopLayout.Product { Root = (RectTransform)button.transform,
                Photo = photo.rectTransform, Title = title.rectTransform, Status = status.rectTransform, Price = price };
        }
    }
}
