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
                Run("Live wallet and stale detail commands", LiveAndStale);
                Run("Owner destruction closes previews before disposing their state", OwnerDestruction);
                Run("Completed M artwork across mobile sizes", () => MissionCompletionUiValidation.AdditionalArtwork(report, Check, true));
                Run("All P/M completion actions preserve saved parameters and recommendation", () => MissionCompletionUiValidation.AllCompletionActions(report, Check, true));
                Run("Completed course record links are read-only", () => MissionCompletionUiValidation.CompletedCourseReadOnly(report, Check, true));
                Run("Shared detail sheet input, scroll and responsive layout", () => GardenDetailSheetValidation.Run(report, Check, true));
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
                Check(Text(f, "Shop.Coins").text == f.Service.CoinBalance.ToString("N0"), "The large wallet shows the actual formatted balance.");
                Check(Text(f, "Shop.Title").fontSize >= 28 && Text(f, "Shop.Coins").fontSize >= 30, "Title and wallet remain prominent.");
                Check(!f.Ui.GetComponentsInChildren<ScrollRect>(false).Any(), "The shop requires no scrolling.");
                Check(!f.Ui.GetComponentsInChildren<MissionBottomNavigation>(false).Any(), "The shop has its simple return action instead of a second tab bar.");
                var products = f.Ui.GetComponentsInChildren<Button>(false).Where(b => b.name.StartsWith("Shop.Product.", StringComparison.Ordinal)).ToArray();
                Check(products.Length == 2, "Exactly two furniture products are offered.");
                var first = Button(f, "Shop.Product.E01"); var second = Button(f, "Shop.Product.E02");
                var back = Button(f, "Shop.Back");
                var screen = new Rect(0, 0, size.x, size.y);
                CheckProduct(f, first, "미니 꽃밭", "UI/Shop/flower-bed", 400, screen);
                CheckProduct(f, second, "쉼터", "UI/Shop/rest-corner", 1000, screen);
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
                f.ClickControl(first); Settle(f); CheckDetail(f, screen);
                f.Capture("shop-detail-" + size.x + "x" + size.y, true);
                Check(f.Ui.CurrentScreen == "shop" && f.Ui.GetComponentInChildren<GardenDetailSheet>() != null && Saved(f) == before, "Product picture opens an unpaid preview.");
                f.Click("Shop.DetailClose");
                Check(f.Ui.CurrentScreen == "shop" && Saved(f) == before, "Cancel restores the shop without a purchase.");
                f.Click("Shop.Back");
                Check(f.Navigation.home.homeCanvas.gameObject.activeSelf && !f.Navigation.activitiesPanel.activeSelf,
                    "The return text opens the garden home, not the mission page.");
                Check(Saved(f) == before, "Returning to the garden does not change progress or coins.");
            }
        }

        static void CheckProduct(FocusMissionValidation.Fixture f, Button card, string title, string resource, int expectedCost, Rect screen)
        {
            Check(card.interactable, "Product remains available for a free preview.");
            Check(card.GetComponentsInChildren<Text>(false).Any(t => t.text == title), "Product title matches the furniture.");
            var price = card.GetComponentsInChildren<Text>(false).Single(t => t.name == "Shop.Price");
            string id = "environment:" + card.name.Substring("Shop.Product.".Length);
            Check(f.Service.EnvironmentPrice(id) == expectedCost && price.text == expectedCost.ToString("N0") + " 코인",
                "The product-specific displayed price matches its actual purchase cost.");
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
                Check(Text(f, "Shop.Coins").text == "0", "An empty wallet is represented accurately.");
                foreach (string code in new[] { "E01", "E02" })
                {
                    var card = Button(f, "Shop.Product." + code);
                    int expectedCost = code == "E01" ? 400 : 1000;
                    Check(card.interactable && card.GetComponentsInChildren<Text>(false).Any(t => t.name == "Shop.Price" && t.text == expectedCost.ToString("N0") + " 코인")
                        && card.GetComponentsInChildren<Text>(false).Any(t => t.name == "Shop.Status" && t.text == expectedCost.ToString("N0") + "코인 더 필요해요"),
                        "An insufficient wallet keeps each actual product price visible and shows its shortfall separately without blocking free preview.");
                    f.ClickControl(card);
                    Check(!Button(f, "Shop.Purchase").interactable && Saved(f) == empty, "Insufficient funds cannot be spent.");
                    f.Click("Shop.DetailClose");
                }
                Fund(f, 4); f.Ui.ShowShop(); Settle(f);
                Check(f.Service.CoinBalance == 400 && f.Service.CanPurchaseEnvironment("environment:E01", "left")
                    && !f.Service.CanPurchaseEnvironment("environment:E02", "right"), "At 400 coins only the mini flower bed is affordable.");
                f.Click("Shop.Product.E01");
                Check(Text(f, "Shop.DetailPrice").text == "400 코인" && Text(f, "Shop.AfterBalance").text == "0 코인"
                    && Button(f, "Shop.Purchase").interactable, "Mini flower bed detail quotes the exact 400-coin purchase boundary.");
                f.Click("Shop.DetailClose"); f.Click("Shop.Product.E02");
                Check(Text(f, "Shop.DetailPrice").text == "1,000 코인" && Text(f, "Shop.AfterBalance").text == "400 코인"
                    && Text(f, "Shop.PurchaseNotice").text.Contains("600코인") && !Button(f, "Shop.Purchase").interactable,
                    "Shelter retains its own price and exact shortfall while the cheaper product is affordable.");
                Fund(f, 6); Settle(f);
                Check(f.Service.CoinBalance == 1000 && f.Service.CanPurchaseEnvironment("environment:E02", "right")
                    && Text(f, "Shop.AfterBalance").text == "0 코인" && Button(f, "Shop.Purchase").interactable,
                    "The open shelter quote enables purchase exactly at 1,000 coins.");
                f.Click("Shop.DetailClose");
                Check(f.Service.PurchaseEnvironment("synthetic-owned-flower", "environment:E01", "left"), f.Service.LastError);
                f.Ui.ShowShop(); Settle(f); string owned = Saved(f);
                var flower = Button(f, "Shop.Product.E01");
                Check(flower.GetComponentsInChildren<Text>(false).Any(t => t.name == "Shop.Price" && t.text == "보유 중"), "Owned furniture is clearly labeled.");
                Check(Text(f, "Shop.Coins").text == f.Service.CoinBalance.ToString("N0"), "Wallet updates when entering after a purchase.");
                f.Capture("shop-owned", true);
                f.ClickControl(flower);
                Check(Button(f, "Shop.ViewOwned").interactable && !f.Ui.GetComponentsInChildren<Button>().Any(b => b.name == "Shop.Purchase") && Saved(f) == owned, "Owned detail offers garden viewing with no duplicate purchase action.");
                f.Click("Shop.DetailClose"); Check(Saved(f) == owned, "Owned preview cancellation changes nothing.");
            }
        }

        static void PurchaseRetry()
        {
            using (var f = new FocusMissionValidation.Fixture(report, true))
            {
                Fund(f, 12); f.Ui.ShowShop(); string before = Saved(f); int coins = f.Service.CoinBalance;
                f.Click("Shop.Product.E02");
                Check(Saved(f) == before, "Shelter preview is unpaid.");
                string request = PendingRequest(f); var submit = Button(f, "Shop.Purchase").onClick;
                f.Store.FailNextSaves = 1; f.Click("Shop.Purchase");
                Check(f.Ui.CurrentScreen == "shop" && f.Ui.GetComponentInChildren<GardenDetailSheet>() != null && Saved(f) == before, "A failed save retains the preview, wallet and placements.");
                Check(f.VisibleText.Contains("구매하지 못했어요"), "The user sees a retry explanation.");
                f.Capture("shop-purchase-save-failure", true);
                Check(PendingRequest(f) == request, "Save failure preserves its request ID.");
                f.Click("Shop.Purchase");
                var purchased = f.Service.Snapshot;
                Check(purchased.Coins == coins - 1000 && purchased.Environments.Count == 1
                    && purchased.Environments.Single().EnvironmentId == "environment:E02" && purchased.Environments.Single().Slot == "right",
                    "Retry spends exactly once and places the requested furniture.");
                Check(purchased.PurchaseReceipts.Count == 1 && purchased.PurchaseCommands.Count == 1, "Retry records exactly one purchase command.");
                string receipt = purchased.PurchaseReceipts.Single(); string after = Saved(f);
                Check(receipt == request, "Retry commits the original command ID.");
                submit.Invoke(); Check(Saved(f) == after, "A duplicate UI callback after success does not purchase again.");
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
                Check(Text(f, "Shop.Coins").text == purchased.Coins.ToString("N0"), "Re-entering shows the committed wallet.");
                Check(Button(f, "Shop.Product.E02").GetComponentsInChildren<Text>(false).Any(t => t.name == "Shop.Price" && t.text == "보유 중"),
                    "Re-entering shows the purchased shelter as owned.");
            }
        }

        static string PendingRequest(FocusMissionValidation.Fixture f) => (string)typeof(WeekOneQuestDemo)
            .GetField("pendingPurchase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(f.Ui);

        static void CheckDetail(FocusMissionValidation.Fixture f, Rect screen)
        {
            var sheet = f.Ui.GetComponentInChildren<GardenDetailSheet>(false);
            Check(sheet != null && sheet.name == "Shop.DetailOverlay", "One shared product detail is visible.");
            foreach (var rect in new[] { sheet.Panel, sheet.Heading.rectTransform, (RectTransform)sheet.CloseButton.transform,
                sheet.Viewport, sheet.Footer, (RectTransform)Button(f, "Shop.Purchase").transform })
                Check(Contains(screen, Bounds(rect)), "Detail title, close, body and purchase remain inside the viewport.");
            Check(!Bounds(sheet.Viewport).Overlaps(Bounds(sheet.Footer)), "Only the body scrolls; it cannot overlap the purchase footer.");
            Check(Contains(Bounds(sheet.Footer), Bounds((RectTransform)Button(f, "Shop.Purchase").transform)), "Purchase action remains inside its fixed footer.");
            Check(!Button(f, "Shop.Product.E01").IsInteractable() && sheet.CloseButton.IsInteractable(), "Modal blocks products while close remains usable.");
        }

        static void LiveAndStale()
        {
            using (var f = new FocusMissionValidation.Fixture(report, true))
            {
                var cameraObject = new GameObject("Synthetic borrowed shop camera", typeof(Camera));
                cameraObject.transform.SetParent(f.Navigation.home.transform, false);
                var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
                f.Navigation.home.gardenCamera = camera;
                camera.transform.SetPositionAndRotation(new Vector3(3, 4, -8), Quaternion.Euler(25, 35, 0));
                camera.orthographicSize = 4.3f; camera.aspect = .8f;
                var position = camera.transform.position; var rotation = camera.transform.rotation; var projection = camera.projectionMatrix;
                Fund(f, 10); f.Ui.ShowShop(); f.Click("Shop.Product.E02");
                var delayed = Button(f, "Shop.Purchase").onClick;
                Fund(f); Settle(f);
                Check(Text(f, "Shop.AfterBalance").text == (f.Service.CoinBalance - 1000).ToString("N0") + " 코인",
                    "Open detail refreshes its projected balance when the shared wallet changes.");
                camera.transform.position = Vector3.zero; camera.orthographicSize = 8; camera.aspect = 2;
                string before = Saved(f); f.Click("Shop.DetailClose"); delayed.Invoke();
                Check(Saved(f) == before && camera.transform.position == position && Quaternion.Angle(camera.transform.rotation, rotation) < .001f
                    && Mathf.Abs(camera.orthographicSize - 4.3f) < .001f && Mathf.Abs(camera.aspect - .8f) < .001f && camera.projectionMatrix == projection,
                    "Cancellation restores borrowed camera pose, zoom and projection, and rejects stale commands.");
                f.Click("Shop.Product.E02"); delayed = Button(f, "Shop.Purchase").onClick;
                f.Navigation.ShowIsland(); delayed.Invoke();
                Check(!GardenDetailSheet.IsOpen && Saved(f) == before, "Hiding the shop releases its sheet and rejects delayed purchase callbacks.");
                f.Navigation.ShowActivities(); f.Ui.ShowShop(); Settle(f);
                Check(!GardenDetailSheet.IsOpen && Button(f, "Shop.Product.E02").IsInteractable(), "Re-entering has no stale overlay or input lock.");
                f.Click("Shop.Product.E02"); delayed = Button(f, "Shop.Purchase").onClick;
                Check(f.Service.PurchaseEnvironment("synthetic-external-shelter", "environment:E02", "right"), f.Service.LastError);
                before = Saved(f); delayed.Invoke();
                Check(Saved(f) == before && f.Navigation.home.homeCanvas.gameObject.activeSelf,
                    "A competing successful purchase is recognized without charging again.");
            }
        }

        static void OwnerDestruction()
        {
            using (var f = new FocusMissionValidation.Fixture(report, true))
            {
                var cameraObject = new GameObject("Synthetic owner-destruction camera", typeof(Camera));
                cameraObject.transform.SetParent(f.Navigation.home.transform, false);
                var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
                f.Navigation.home.gardenCamera = camera;
                camera.transform.SetPositionAndRotation(new Vector3(2, 5, -7), Quaternion.Euler(18, 42, 0));
                camera.orthographicSize = 3.7f; camera.aspect = .72f;
                var position = camera.transform.position; var rotation = camera.transform.rotation; var projection = camera.projectionMatrix;
                Fund(f, 4); f.Ui.ShowShop(); f.Click("Shop.Product.E01");
                var sheet = f.Ui.GetComponentInChildren<GardenDetailSheet>(false);
                var ownerGroup = sheet.Owner.GetComponent<CanvasGroup>();
                var delayed = Button(f, "Shop.Purchase").onClick;
                int expectedCoins = f.Service.CoinBalance;
                bool closedWhileSourceOpen = false;
                sheet.Closed += () => {
                    // This callback deliberately reads the source while cleanup is in progress.
                    // It must run before Dispose, like the real preview restoration callback.
                    closedWhileSourceOpen = f.Ui.HasOpenState && f.Service.CoinBalance == expectedCoins;
                };
                camera.transform.position = Vector3.zero; camera.orthographicSize = 9; camera.aspect = 2;
                typeof(WeekOneQuestDemo).GetMethod("OnDestroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(f.Ui, null);
                Check(closedWhileSourceOpen && !f.Ui.HasOpenState && !GardenDetailSheet.IsOpen,
                    "Owner destruction releases its service-backed preview before closing the source.");
                Check(ownerGroup.interactable && ownerGroup.blocksRaycasts, "Owner cleanup releases the modal input lock.");
                Check(camera.transform.position == position && Quaternion.Angle(camera.transform.rotation, rotation) < .001f
                    && Mathf.Abs(camera.orthographicSize - 3.7f) < .001f && Mathf.Abs(camera.aspect - .72f) < .001f && camera.projectionMatrix == projection,
                    "Owner destruction restores the borrowed camera before releasing its state.");
                // Neither call may touch the disposed state. Canvas callbacks must also be removed.
                delayed.Invoke(); f.Navigation.home.RefreshFromState(); Canvas.ForceUpdateCanvases();
                Check(!f.Ui.HasOpenState && !GardenDetailSheet.IsOpen, "Late purchase, home refresh and canvas callbacks remain harmless after destruction.");
            }
        }

        static void Settle(FocusMissionValidation.Fixture f)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var fit in f.Ui.GetComponentsInChildren<FigmaMissionCanvasFit>(false)) fit.Fit();
                foreach (var layout in f.Ui.GetComponentsInChildren<GardenShopLayout>(false)) layout.Refresh();
                foreach (var sheet in f.Ui.GetComponentsInChildren<GardenDetailSheet>(false)) sheet.Refresh();
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
