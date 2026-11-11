using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Explicit synthetic batch only: no device, app preferences or production save.
    public static class GardenShopValidation
    {
        static FocusMissionValidation.Report report;
        static int assertions;

        public static void RunBatch()
        {
            string requested = Environment.GetEnvironmentVariable("CAPSTONE_SHOP_VALIDATION");
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode
                || Application.companyName != "CapstoneSyntheticValidation" || string.IsNullOrWhiteSpace(requested))
                throw new InvalidOperationException("An isolated synthetic batch and CAPSTONE_SHOP_VALIDATION are required.");
            string output = Path.GetFullPath(requested);
            if (output == Path.GetFullPath(Application.persistentDataPath)
                || Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("A fresh validation output directory is required.");
            Directory.CreateDirectory(output);
            string previousArtifacts = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            Environment.SetEnvironmentVariable("CAPSTONE_ARTIFACTS", output);
            report = new FocusMissionValidation.Report { runId = "garden-shop", utc = DateTimeOffset.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                environment = "Explicit synthetic memory state and preview-scene controls; no production save, preferences or native input" };
            assertions = 0;
            try
            {
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844), new Vector2Int(1200, 2000) })
                {
                    var requestedSize = size;
                    Run("Shop layout, preview cancellation and garden return " + size, () => Landing(requestedSize));
                }
                Run("Owned and insufficient products retain price and allow unpaid previews", ProductStates);
                Run("Purchase failure retries once and restores the same furniture and wallet", PurchaseRetry);
                Run("Existing placement and availability regressions", LocalLoopValidation.ValidateShopPurchaseRegressions);
            }
            finally
            {
                report.checks.Add(new FocusMissionValidation.CheckResult { name = "assertions", status = "info", detail = assertions.ToString() });
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
                Environment.SetEnvironmentVariable("CAPSTONE_ARTIFACTS", previousArtifacts);
            }
            Debug.Log("GARDEN_SHOP_VALIDATION passed=" + report.passed + " failed=" + report.failed + " assertions=" + assertions);
            if (report.failed != 0) throw new InvalidOperationException("Garden shop validation failed; inspect report.json.");
        }

        static void Run(string name, Action action)
        {
            try { action(); report.passed++; report.checks.Add(new FocusMissionValidation.CheckResult { name = name, status = "pass" }); }
            catch (Exception error) { report.failed++; report.checks.Add(new FocusMissionValidation.CheckResult { name = name, status = "fail", detail = error.ToString() }); }
        }
        static void Check(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException(message); }
        static Button Button(FocusMissionValidation.Fixture f, string name) => f.Ui.GetComponentsInChildren<Button>(false).Single(b => b.name == name);
        static Text Text(FocusMissionValidation.Fixture f, string name) => f.Ui.GetComponentsInChildren<Text>(false).Single(t => t.name == name);
        static string Saved(FocusMissionValidation.Fixture f) => StateCodec.Encode(f.Service.Snapshot);
        static void Fund(FocusMissionValidation.Fixture f, int hundreds = 1)
        { for (int i = 0; i < hundreds; i++) Check(f.Service.AddTestCoins(), f.Service.LastError); }

        static void Landing(Vector2Int size)
        {
            using (var f = new FocusMissionValidation.Fixture(report, true))
            {
                Fund(f, 12);
                string before = Saved(f);
                f.Ui.ShowShop(); f.Resize(size.x, size.y); Settle(f);
                Check(f.Ui.CurrentScreen == "shop", "Entry opens the shop directly.");
                Check(Text(f, "Shop.Title").text == "정원 상점", "Shop title uses the requested name.");
                Check(Text(f, "Shop.Coins").text == f.Service.CoinBalance.ToString("N0") + " 코인", "The large wallet shows the actual formatted balance.");
                Check(Text(f, "Shop.Title").fontSize >= 28 && Text(f, "Shop.Coins").fontSize >= 30, "Title and wallet remain prominent.");
                Check(!f.Ui.GetComponentsInChildren<ScrollRect>(false).Any(), "The shop requires no scrolling.");
                Check(!f.Ui.GetComponentsInChildren<MissionBottomNavigation>(false).Any(), "The shop has its simple return action instead of a second tab bar.");
                var products = f.Ui.GetComponentsInChildren<Button>(false).Where(b => b.name.StartsWith("Shop.Product.", StringComparison.Ordinal)).ToArray();
                Check(products.Length == 2, "Exactly two furniture products are offered.");
                var first = Button(f, "Shop.Product.E01"); var second = Button(f, "Shop.Product.E02");
                var back = Button(f, "Shop.Back");
                var screen = new Rect(0, 0, size.x, size.y);
                CheckProduct(f, first, "꽃밭", "UI/Shop/flower-bed", screen);
                CheckProduct(f, second, "쉼터", "UI/Shop/rest-corner", screen);
                var title = Bounds(Text(f, "Shop.Title").rectTransform); var wallet = Bounds(Text(f, "Shop.Coins").rectTransform);
                var firstRect = Bounds((RectTransform)first.transform); var secondRect = Bounds((RectTransform)second.transform);
                var backRect = Bounds((RectTransform)back.transform);
                foreach (var rect in new[] { title, wallet, firstRect, secondRect, backRect }) Check(Contains(screen, rect), "Every primary control stays inside the screen.");
                Check(!firstRect.Overlaps(secondRect) && !backRect.Overlaps(firstRect) && !backRect.Overlaps(secondRect), "Product cards and return action do not overlap.");
                Check(!title.Overlaps(wallet) && wallet.yMin >= Mathf.Max(firstRect.yMax, secondRect.yMax) - .5f, "Wallet stays above the merchandise.");
                var backLabel = back.GetComponentsInChildren<Text>(false).Single();
                Check(backLabel.text == "돌아가기" && backLabel.fontSize <= 18, "Return is a small text action.");
                var backImage = back.GetComponent<Image>();
                Check((backImage == null || backImage.color.a == 0) && !back.GetComponentsInChildren<GardenThemeSurface>(false).Any(), "Return has no button frame.");
                Check(Saved(f) == before, "Opening and laying out the shop changes no saved state.");
                f.Capture("shop-" + size.x + "x" + size.y, true);
                f.ClickControl(first);
                Check(f.Ui.CurrentScreen == "placement" && Saved(f) == before, "Product picture opens an unpaid preview.");
                f.Click("취소");
                Check(f.Ui.CurrentScreen == "shop" && Saved(f) == before, "Cancel restores the shop without a purchase.");
                f.Click("Shop.Back");
                Check(f.Navigation.home.homeCanvas.gameObject.activeSelf && !f.Navigation.activitiesPanel.activeSelf,
                    "The return text opens the garden home, not the mission page.");
                Check(Saved(f) == before, "Returning to the garden does not change progress or coins.");
            }
        }

        static void CheckProduct(FocusMissionValidation.Fixture f, Button card, string title, string resource, Rect screen)
        {
            Check(card.interactable, "Product remains available for a free preview.");
            Check(card.GetComponentsInChildren<Text>(false).Any(t => t.text == title), "Product title matches the furniture.");
            var price = card.GetComponentsInChildren<Text>(false).Single(t => t.name == "Shop.Price");
            Check(price.text == f.Ui.Balance.EnvironmentCost + " 코인", "Product price comes from the existing balance policy.");
            var photo = card.GetComponentsInChildren<RawImage>(false).Single(i => i.name == "Shop.Photo");
            Check(photo.texture != null && photo.texture == Resources.Load<Texture2D>(resource), "Product uses its real furniture photograph.");
            Check(!photo.raycastTarget && !price.raycastTarget, "Photograph and price do not consume the product tap.");
            var imageRect = Bounds(photo.rectTransform); var priceRect = Bounds(price.rectTransform); var cardRect = Bounds((RectTransform)card.transform);
            Check(imageRect.width >= screen.width * .50f && imageRect.height >= screen.height * .14f, "Furniture photograph occupies a large readable area.");
            Check(Contains(cardRect, imageRect) && Contains(cardRect, priceRect) && !imageRect.Overlaps(priceRect), "Photograph and price stay separate inside their product.");
        }

        static void ProductStates()
        {
            using (var f = new FocusMissionValidation.Fixture(report, true))
            {
                f.Ui.ShowShop(); Settle(f); string empty = Saved(f);
                Check(Text(f, "Shop.Coins").text == "0 코인", "An empty wallet is represented accurately.");
                foreach (string code in new[] { "E01", "E02" })
                {
                    var card = Button(f, "Shop.Product." + code);
                    Check(card.interactable && card.GetComponentsInChildren<Text>(false).Any(t => t.name == "Shop.Status" && t.text == "코인 부족"),
                        "Insufficient funds retain a readable product state and free preview.");
                    f.ClickControl(card);
                    Check(!Button(f, "확정").interactable && Saved(f) == empty, "Insufficient funds cannot be spent.");
                    f.Click("취소");
                }
                Fund(f);
                Check(f.Service.PurchaseEnvironment("synthetic-owned-flower", "environment:E01", "left"), f.Service.LastError);
                f.Ui.ShowShop(); Settle(f); string owned = Saved(f);
                var flower = Button(f, "Shop.Product.E01");
                Check(flower.GetComponentsInChildren<Text>(false).Any(t => t.name == "Shop.Status" && t.text == "보유 중"), "Owned furniture is clearly labeled.");
                Check(Text(f, "Shop.Coins").text == f.Service.CoinBalance.ToString("N0") + " 코인", "Wallet updates when entering after a purchase.");
                f.Capture("shop-owned", true);
                f.ClickControl(flower);
                Check(!Button(f, "확정").interactable && Saved(f) == owned, "Owned preview cannot buy the same furniture again.");
                f.Click("취소"); Check(Saved(f) == owned, "Owned preview cancellation changes nothing.");
            }
        }

        static void PurchaseRetry()
        {
            using (var f = new FocusMissionValidation.Fixture(report, true))
            {
                Fund(f); f.Ui.ShowShop(); string before = Saved(f); int coins = f.Service.CoinBalance;
                f.Click("Shop.Product.E02");
                Check(Saved(f) == before, "Shelter preview is unpaid.");
                f.Store.FailNextSaves = 1; f.Click("확정");
                Check(f.Ui.CurrentScreen == "placement" && Saved(f) == before, "A failed save retains the preview, wallet and placements.");
                Check(f.VisibleText.Contains("배치하지 못했어요"), "The user sees a retry explanation.");
                f.Capture("shop-purchase-save-failure", true);
                f.Click("확정");
                var purchased = f.Service.Snapshot;
                Check(purchased.Coins == coins - f.Ui.Balance.EnvironmentCost && purchased.Environments.Count == 1
                    && purchased.Environments.Single().EnvironmentId == "environment:E02" && purchased.Environments.Single().Slot == "right",
                    "Retry spends exactly once and places the requested furniture.");
                Check(purchased.PurchaseReceipts.Count == 1 && purchased.PurchaseCommands.Count == 1, "Retry records exactly one purchase command.");
                string receipt = purchased.PurchaseReceipts.Single(); string after = Saved(f);
                Check(f.Service.PurchaseEnvironment(receipt, "environment:E02", "right") && Saved(f) == after,
                    "Replaying the same successful command does not double-charge or duplicate the furniture.");
                Check(!f.Service.PurchaseEnvironment(receipt, "environment:E01", "left") && Saved(f) == after,
                    "A reused request ID cannot purchase a different product.");
                using (var restored = new GardenStateService(f.Store, f.Clock, f.Ui.Balance))
                {
                    Check(restored.Open(), restored.LastError);
                    Check(restored.CoinBalance == purchased.Coins && restored.Snapshot.Environments.Single().EnvironmentId == "environment:E02"
                        && restored.Snapshot.PurchaseReceipts.Single() == receipt, "Reopening the synthetic store preserves the furniture and wallet.");
                }
                f.Navigation.ShowActivities(); f.Ui.ShowShop(); Settle(f);
                Check(Text(f, "Shop.Coins").text == purchased.Coins.ToString("N0") + " 코인", "Re-entering shows the committed wallet.");
                Check(Button(f, "Shop.Product.E02").GetComponentsInChildren<Text>(false).Any(t => t.name == "Shop.Status" && t.text == "보유 중"),
                    "Re-entering shows the purchased shelter as owned.");
            }
        }

        static void Settle(FocusMissionValidation.Fixture f)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var fit in f.Ui.GetComponentsInChildren<FigmaMissionCanvasFit>(false)) fit.Fit();
                foreach (var layout in f.Ui.GetComponentsInChildren<GardenShopLayout>(false)) layout.Refresh();
                Canvas.ForceUpdateCanvases();
            }
        }
        static Rect Bounds(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var points = corners.Select(p => RectTransformUtility.WorldToScreenPoint(camera, p)).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - .6f && inner.xMax <= outer.xMax + .6f
            && inner.yMin >= outer.yMin - .6f && inner.yMax <= outer.yMax + .6f;
    }
}
