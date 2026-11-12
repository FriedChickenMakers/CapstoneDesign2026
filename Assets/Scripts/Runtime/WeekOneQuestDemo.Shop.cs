using System;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        GardenDetailSheet shopDetail;
        bool shopPurchasing;

        public void ShowShop()
        {
            if (!opened) { ShowHome(); return; }
            ClearGhost();
            var root = BeginFigmaPage("shop", ShowShop, FigmaMissionUi.Background);
            FigmaMissionUi.UseGardenShell(activityFrame);
            root.GetComponent<FocusMobilePageLayout>().FixedContent = true;
            var state = Service.Snapshot;
            var heading = FigmaMissionUi.Label(root, "정원 상점", 18, 20, 354, 48, 32,
                FigmaMissionUi.Ink, true, TextAnchor.MiddleCenter);
            heading.name = "Shop.Title";
            FigmaMissionUi.Label(root, "보유 코인", 18, 78, 354, 24, 14,
                FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter).name = "Shop.BalanceLabel";
            var coins = FigmaMissionUi.Label(root, state.Coins.ToString("N0"), 18, 104, 354, 48, 34,
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
            int cost = Service.EnvironmentPrice(id);
            bool owned = state.Environments.Any(item => item.EnvironmentId == id || item.Slot == slot);
            bool affordable = state.Coins >= cost;
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
            var status = FigmaMissionUi.Label(button.transform, owned ? "정원에 배치됨" : !affordable
                ? (cost - state.Coins).ToString("N0") + "코인 더 필요해요" : "배치 미리보기",
                18, 254, 170, 20, 13, owned || affordable ? FigmaMissionUi.Muted : FigmaMissionUi.Brown);
            status.name = "Shop.Status";
            var price = FigmaMissionUi.Rect(button.transform, "Shop.PriceFace", 216, 224, 120, 44);
            GardenUi.Round(price.gameObject.AddComponent<Image>(), owned || !affordable ? GardenSagePalette.Disabled : GardenSagePalette.Primary);
            price.GetComponent<Image>().raycastTarget = false;
            FigmaMissionUi.Label(price, owned ? "보유 중" : cost.ToString("N0") + " 코인", 0, 0, 120, 44,
                17, owned || !affordable ? GardenSagePalette.Ink : Color.white, true, TextAnchor.MiddleCenter).name = "Shop.Price";
            return new GardenShopLayout.Product { Root = (RectTransform)button.transform,
                Photo = photo.rectTransform, Title = title.rectTransform, Status = status.rectTransform, Price = price };
        }

        void ShowShopProductDetail(string id)
        {
            if (!opened || (id != "environment:E01" && id != "environment:E02")) return;
            if (shopDetail != null && pendingEnvironment == id) return;
            if (screen != "shop" || view == null) ShowShop();
            CloseShopDetail(false);
            ClearGhost();
            pendingEnvironment = id; pendingPurchase = Guid.NewGuid().ToString("N");
            string request = pendingPurchase, slot = id == "environment:E01" ? "left" : "right";
            string title = MindfulnessContent.FindGardenContent(id).title;
            int cost = Service.EnvironmentPrice(id);
            var frame = activityFrame;
            var origin = FigmaMissionUi.Find<Button>(view.transform, "Shop.Product." + id.Substring("environment:".Length));
            var cameraRestore = new ShopPreviewCamera(transform.root.GetComponent<GardenHomePresenter>());
            var sheet = GardenDetailSheet.Create((RectTransform)view.transform, "Shop.Detail", title, () => CloseShopDetail(), origin);
            if (sheet == null) return;
            sheet.MaxHeight = 560;
            shopDetail = sheet; shopPurchasing = false;
            sheet.Closed += () => {
                ClearIntegratedPurchasePreview(); cameraRestore.Restore();
                if (shopDetail == sheet) { shopDetail = null; shopPurchasing = false; }
            };
            Func<bool> current = () => sheet != null && shopDetail == sheet && sheet.gameObject.activeInHierarchy
                && frame != null && frame == activityFrame && screen == "shop";
            var state = Service.Snapshot;
            bool owned = state.Environments.Any(item => item.EnvironmentId == id || item.Slot == slot);
            var candidate = Service.Snapshot;
            if (!owned) candidate.Environments.Add(new EnvironmentPlacement { EnvironmentId = id, Slot = slot });
            var preview = FigmaMissionUi.Rect(sheet.Content, "Shop.Preview", 16, 8, 334, 209);
            if (!ShowIntegratedPurchasePreview(candidate, target: preview))
            {
                FigmaMissionUi.Asset(preview, "UI/Shop/" + (slot == "left" ? "flower-bed" : "rest-corner"), 0, 0, 334, 167);
                FigmaMissionUi.Label(preview, "상품 사진 · 정원 미리보기를 불러오지 못했어요.",
                    8, 172, 318, 32, 13, FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter);
            }
            var description = FigmaMissionUi.Label(sheet.Content, owned ? "정원에 놓인 모습을 확인해 보세요."
                : "정원의 " + (slot == "left" ? "왼쪽" : "오른쪽") + " 자리에 놓여요.", 16, 229, 334, 30, 16,
                FigmaMissionUi.Ink, false, TextAnchor.MiddleCenter);
            float descriptionHeight = Mathf.Max(30, description.preferredHeight);
            description.rectTransform.sizeDelta = new Vector2(334, descriptionHeight);
            sheet.Content.sizeDelta = new Vector2(366, 229 + descriptionHeight + 16);

            FigmaMissionUi.Label(sheet.Footer, "가격", 16, 4, 104, 26, 15, FigmaMissionUi.Muted);
            FigmaMissionUi.Label(sheet.Footer, cost.ToString("N0") + " 코인", 126, 4, 224, 26, 19,
                FigmaMissionUi.Ink, true, TextAnchor.MiddleRight).name = "Shop.DetailPrice";
            var balanceLabel = FigmaMissionUi.Label(sheet.Footer, "구매 후 잔액", 16, 36, 136, 26, 15, FigmaMissionUi.Muted);
            var balance = FigmaMissionUi.Label(sheet.Footer, "", 154, 36, 196, 26, 19, FigmaMissionUi.Ink, true, TextAnchor.MiddleRight);
            balance.name = "Shop.AfterBalance";
            var message = FigmaMissionUi.Label(sheet.Footer, "", 16, 72, 334, 42, 14, FigmaMissionUi.Muted, false, TextAnchor.MiddleCenter);
            message.name = "Shop.PurchaseNotice";
            Button purchase = null;
            int shownBalance = state.Coins;
            Action<string> refresh = failure => {
                var live = Service.Snapshot;
                shownBalance = live.Coins;
                bool alreadyOwned = live.Environments.Any(item => item.EnvironmentId == id || item.Slot == slot);
                bool enough = live.Coins >= cost;
                balanceLabel.text = alreadyOwned || !enough ? "보유 코인" : "구매 후 잔액";
                balance.text = (alreadyOwned || !enough ? live.Coins : live.Coins - cost).ToString("N0") + " 코인";
                message.text = failure ?? (alreadyOwned ? "이미 보유한 가구예요. 추가로 차감하지 않아요."
                    : !enough ? (cost - live.Coins).ToString("N0") + "코인 더 모으면 구매할 수 있어요."
                    : "구매하면 정원에 바로 배치돼요.");
                message.color = failure != null ? GardenUi.Warning : GardenSagePalette.Muted;
                float messageHeight = Mathf.Max(36, Mathf.Ceil(message.preferredHeight));
                FigmaMissionUi.SetRect(message.rectTransform, 16, 72, 334, messageHeight);
                FigmaMissionUi.SetRect((RectTransform)purchase.transform, 16, 72 + messageHeight + 12, 334, 52);
                foreach (var label in purchase.GetComponentsInChildren<Text>())
                {
                    label.text = alreadyOwned ? "정원에서 보기" : enough ? cost.ToString("N0") + "코인으로 구매·배치" : "코인이 부족해요";
                    FigmaMissionUi.SetRect(label.rectTransform, 16, 0, 302, 52);
                }
                purchase.name = alreadyOwned ? "Shop.ViewOwned" : "Shop.Purchase";
                purchase.interactable = !shopPurchasing && (alreadyOwned || enough);
                sheet.FooterHeight = 72 + messageHeight + 12 + 52 + 16;
                sheet.Refresh();
            };
            purchase = FigmaMissionUi.Button(sheet.Footer, "", 16, 120, 334, 52, () => {
                if (!current() || shopPurchasing) return;
                var live = Service.Snapshot;
                if (live.Environments.Any(item => item.EnvironmentId == id || item.Slot == slot))
                { CloseShopDetail(false); GoGarden(); return; }
                if (live.Coins < cost) { refresh(null); return; }
                shopPurchasing = true; purchase.interactable = false;
                if (!Service.PurchaseEnvironment(request, id, slot))
                {
                    shopPurchasing = false;
                    refresh("구매하지 못했어요. 코인은 그대로예요.\n다시 눌러 주세요.");
                    GardenSfxPlayer.Play(GardenSfxCue.Error);
                    return;
                }
                GardenSfxPlayer.Play(GardenSfxCue.Purchase);
                CloseShopDetail(false); GoGarden();
            }, FigmaMissionUi.Green, Color.white, 17);
            refresh(null);
            // Reading the scalar balance avoids cloning the user's entire
            // journal every frame. Recalculate the offer only after it changes.
            Canvas.WillRenderCanvases refreshBalance = () => {
                if (current() && Service.CoinBalance != shownBalance) refresh(null);
            };
            Canvas.preWillRenderCanvases += refreshBalance;
            sheet.Closed += () => Canvas.preWillRenderCanvases -= refreshBalance;
            sheet.Refresh();
        }

        bool CloseShopDetail(bool restoreSelection = true)
        {
            if (shopDetail == null) return false;
            var sheet = shopDetail; shopDetail = null; shopPurchasing = false;
            sheet.Close(restoreSelection);
            return true;
        }

        // A purchase preview borrows the home camera, never a second camera.
        sealed class ShopPreviewCamera
        {
            readonly Camera camera;
            readonly PrototypeIslandDrag orbit;
            readonly PrototypeGardenPreviewLayout layout;
            readonly Vector3 position, focus;
            readonly Quaternion rotation;
            readonly Vector2 angles;
            readonly float size, aspect, zoom;
            readonly Matrix4x4 projection;
            public ShopPreviewCamera(GardenHomePresenter home)
            {
                if (home == null || home.gardenCamera == null) return;
                camera = home.gardenCamera; position = camera.transform.position; rotation = camera.transform.rotation;
                size = camera.orthographicSize; aspect = camera.aspect; projection = camera.projectionMatrix;
                orbit = home.homeCanvas.GetComponentInChildren<PrototypeIslandDrag>(true);
                if (orbit != null) { angles = orbit.Angles; focus = orbit.focus; layout = orbit.zoomLayout; }
                if (layout != null) zoom = layout.Zoom;
            }
            public void Restore()
            {
                if (camera == null) return;
                if (layout != null) layout.SetZoom(zoom);
                if (orbit != null) { orbit.SetFocus(focus); orbit.RestoreAngles(angles); }
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.orthographicSize = size; camera.aspect = aspect; camera.projectionMatrix = projection;
            }
        }
    }
}
