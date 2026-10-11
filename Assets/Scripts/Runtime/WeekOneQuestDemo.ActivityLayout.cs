using System;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        GameObject activityFrame;
        Transform activityActions;
        Text activityEyebrow;

        void ClearPageView()
        {
            var previous = activityFrame != null ? activityFrame : pageViewport;
            if (previous != null)
            {
                previous.SetActive(false);
                if (Application.isPlaying) Destroy(previous); else DestroyImmediate(previous);
            }
            activityFrame = null;
            pageViewport = null;
            activityActions = null;
            stateSummary = null;
        }

        // The title and action dock belong to the frame, never to ScrollRect.content.
        Transform FocusPage(string title, string context)
        {
            ClearRecordHeartRefresh();
            ClearIntegratedPurchasePreview();
            ClearPageView();
            var background = GetComponent<Image>();
            if (background != null) background.color = GardenUi.Background;
            activityFrame = GardenUi.Box(transform, "Activity focus frame", 0, .105f, 1, .895f, GardenUi.Background);
            var frame = (RectTransform)activityFrame.transform;
            frame.offsetMax = new Vector2(0, GardenUi.HasSharedHeader(transform) ? -96 : 0);
            var layout = activityFrame.AddComponent<GardenActivityLayout>();
            var header = GardenUi.Box(frame, "Activity fixed heading", 0, 1, 1, 0);
            var headerRect = (RectTransform)header.transform;
            headerRect.pivot = new Vector2(.5f, 1);
            var headerFlow = GardenUi.VerticalFlow(header.transform, 12, 24, 20);
            activityEyebrow = GardenUi.FlowLabel(headerFlow, context, GardenUi.CaptionSize, GardenUi.Muted);
            var heading = GardenUi.FlowLabel(headerFlow, title, GardenUi.TitleSize);
            heading.fontStyle = FontStyle.Bold;
            var body = GardenUi.ScrollPage(frame, "Local garden flow", 0, 1, 0);
            pageViewport = body.parent.gameObject;
            view = body.gameObject;
            GardenUi.VerticalFlow(body, 24, 16, 32);
            var fit = body.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var footer = GardenUi.Box(frame, "Activity fixed actions", 0, 0, 1, 0, GardenUi.Background);
            var footerRect = (RectTransform)footer.transform;
            footerRect.pivot = new Vector2(.5f, 0);
            activityActions = GardenUi.VerticalFlow(footer.transform, 12, 16, 24);
            var divider = GardenUi.Box(footer.transform, "Action surface divider", 0, 1, 1, 0, GardenUi.Border);
            divider.AddComponent<LayoutElement>().ignoreLayout = true;
            ((RectTransform)divider.transform).offsetMin = new Vector2(48, 0);
            ((RectTransform)divider.transform).offsetMax = new Vector2(-48, 2);
            notice = GardenUi.FlowLabel(footer.transform, string.IsNullOrEmpty(error) ? "" : "저장하지 못했어요. 입력을 유지했으니 다시 시도해 주세요.", GardenUi.CaptionSize);
            notice.gameObject.SetActive(!string.IsNullOrEmpty(error));
            notice.color = GardenUi.Warning;
            layout.Header = headerRect;
            layout.Body = (RectTransform)pageViewport.transform;
            layout.Footer = footerRect;
            return body;
        }

        void DockAction(string title, Action action, bool primary = false)
        {
            var button = GardenUi.Button(activityActions, title, 0, 0, 1, 0, action, primary);
            button.gameObject.AddComponent<GardenFlowButton>();
        }
        void DockPair(string left, Action leftAction, string right, Action rightAction, bool rightPrimary = false)
        {
            var row = GardenUi.Box(activityActions, "Action row", 0, 0, 1, 0);
            var flow = row.AddComponent<HorizontalLayoutGroup>();
            flow.spacing = 16; flow.childControlWidth = flow.childControlHeight = true;
            flow.childForceExpandWidth = true;
            GardenUi.Button(row.transform, left, 0, 0, 1, 1, leftAction).gameObject.AddComponent<GardenFlowButton>();
            GardenUi.Button(row.transform, right, 0, 0, 1, 1, rightAction, rightPrimary).gameObject.AddComponent<GardenFlowButton>();
        }
        Transform Guidance(Transform parent, string text)
        {
            var card = GardenUi.Card(parent, "Practice guidance", 0, 0, 1, 0, GardenUi.Pale);
            var flow = card.AddComponent<VerticalLayoutGroup>();
            flow.padding = new RectOffset(24, 24, 24, 24);
            flow.childControlWidth = flow.childControlHeight = true;
            flow.childForceExpandHeight = false;
            GardenUi.FlowLabel(card.transform, text, GardenUi.BodySize);
            return card.transform;
        }
        void PausePractice(ActivitySession session)
        {
            if (SaveActivePracticeDraft() && Apply(Service.PauseSession(session.SessionId)))
            { ClearPracticeDraft(); ShowSession(); }
        }
        void CancelSelectedPractice(ActivitySession session)
        {
            if (!Apply(Service.CancelSelection(session.SessionId))) return;
            ClearPracticeDraft(); sessionId = null; ShowHome();
        }
        void EndPractice(ActivitySession session)
        {
            if (!SaveActivePracticeDraft() || !Apply(Service.EndParticipation(session.SessionId))) return;
            if (session.MissionId == "free-mission:M15") AndroidPlatformBridge.CancelMissionRewardReminder(session.SessionId);
            ClearPracticeDraft(); sessionId = null; ShowHome();
        }
        void PreviousPractice(ActivitySession session)
        {
            if (SaveActivePracticeDraft() && Apply(Service.SavePracticeStep(session.SessionId, session.InstructionStep - 1)))
            { ClearPracticeDraft(); ShowSession(); }
        }
    }
}
