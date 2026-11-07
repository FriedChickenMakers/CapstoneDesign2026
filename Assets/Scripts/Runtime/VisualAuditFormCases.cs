#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Supplementary cases for the generic scrolling forms. Native keyboard
    // screenshots must be supplied by the Android host at each keyboard-open
    // boundary; an Editor frame alone does not prove keyboard avoidance.
    public static class VisualAuditFormCases
    {
        const string Sample = "합성 화면 검토용 기록입니다.\n정원에서 잠시 쉬며 천천히 숨을 골랐어요.\n마지막 입력란도 가려지지 않는지 살펴봅니다.";
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        public static IEnumerable<string> Cases(MockupNavigation nav, WeekOneQuestDemo ui,
            GardenStateService service, VisualAuditTraversal.Clock clock)
        {
            Require(ui.Service == service && ui.HasOpenState, "Form audit needs the injected service.");
            Require(AndroidPlatformBridge.InputMode == PlatformInputMode.Mock,
                "Form audit requires mock platform input.");
            Require(service.Snapshot.InstallationId == "synthetic-visual-audit",
                "Form fixtures require the synthetic audit garden.");
            nav.ShowActivities(); ui.ShowExperienceTypes();
            VisualAuditTraversal.Click(ui, "중립"); yield return null;
            for (int step = 0; step < 4; step++)
            {
                var input = Inputs(ui).Single(); input.text = Sample;
                foreach (string frame in Focus(input, "form-experience-step-" + step)) yield return frame;
                if (step < 3) { VisualAuditTraversal.Click(ui, "다음"); yield return null; }
            }
            VisualAuditTraversal.Click(ui, "기록 취소"); yield return "form-experience-cancelled";

            ui.ShowPlanField("warning-sign"); yield return null;
            var plan = Inputs(ui).Single(); plan.text = Sample;
            foreach (string frame in Focus(plan, "form-plan")) yield return frame;
            VisualAuditTraversal.Click(ui, "저장"); yield return "form-plan-saved";

            const string experienceId = "synthetic-form-keyboard-experience";
            Require(service.SaveExperience(experienceId, "중립", Sample, Sample, "차분함", Sample), service.LastError);
            ui.ShowExperienceRecord(experienceId); yield return null;
            VisualAuditTraversal.Click(ui, "기록 수정"); yield return null;
            var experienceInputs = Inputs(ui);
            Require(experienceInputs.Length == 4, "Expected all four experience edit fields.");
            for (int index = 0; index < experienceInputs.Length; index++)
                foreach (string frame in Focus(experienceInputs[index], "form-edit-experience-field-" + index)) yield return frame;
            VisualAuditTraversal.Click(ui, "저장"); yield return "form-edit-experience-saved";

            const string sessionId = "synthetic-form-keyboard-reflection";
            Require(service.BeginSession(sessionId, "free-mission:M17"), service.LastError);
            Require(service.StartSession(sessionId), service.LastError);
            clock.Time = clock.Time.AddMinutes(4);
            Require(service.CompleteSession(sessionId, "차분함", Sample), service.LastError);
            ui.ShowRecord(sessionId); yield return null;
            VisualAuditTraversal.Click(ui, "감정 기록 수정"); yield return null;
            var reflectionInputs = Inputs(ui);
            Require(reflectionInputs.Length == 2, "Expected both reflection edit fields.");
            for (int index = 0; index < reflectionInputs.Length; index++)
                foreach (string frame in Focus(reflectionInputs[index], "form-edit-reflection-field-" + index)) yield return frame;
            VisualAuditTraversal.Click(ui, "저장"); yield return "form-edit-reflection-saved";

            foreach (string frame in NotificationUnavailable(nav)) yield return frame;
            nav.ShowIsland(); yield return "forms-final-garden";
        }

        static InputField[] Inputs(WeekOneQuestDemo ui) => ui.GetComponentsInChildren<InputField>()
            .Where(field => field.isActiveAndEnabled && field.interactable).ToArray();

        static IEnumerable<string> Focus(InputField field, string key)
        {
            VisualAuditTraversal.Settle();
            Reveal(field.transform);
            yield return key + "-before-focus";
            try
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(field.gameObject);
                field.ActivateInputField();
                yield return key + "-keyboard-opening";
                // Do not scroll after activation: the app's own avoidance code
                // must reveal this field when the Android keyboard changes size.
                yield return key + "-keyboard-open";
            }
            finally
            {
                field.DeactivateInputField();
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }
            yield return null;
            yield return key + "-keyboard-closed";
        }

        static IEnumerable<string> NotificationUnavailable(MockupNavigation nav)
        {
            var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
            Require(sensor != null, "Settings component is missing.");
            sensor.PrepareView(); nav.ShowSettings(); yield return null;
            var timing = Field<Button>(sensor, "notificationTiming");
            var options = Field<GameObject>(sensor, "notificationOptions");
            var status = Field<Text>(sensor, "notificationActionStatus");
            bool oldEnabled = sensor.enabled, oldOptions = options.activeSelf, oldStatus = status.gameObject.activeSelf;
            string oldText = status.text;
            sensor.enabled = false;
            try
            {
                options.SetActive(true);
                Reveal(timing.transform);
                yield return "settings-time-picker-mock-before-callback";
                // The mock row is intentionally disabled. Invoke its existing
                // callback to render the unavailable result; ChooseTime exits at
                // the mock bridge guard, so no native picker or setting is changed.
                timing.onClick.Invoke();
                Require(status.gameObject.activeSelf && status.text == "시간 선택을 열지 못했어요. 다시 시도해 주세요.",
                    "Expected notification time picker unavailable feedback.");
                VisualAuditTraversal.Settle(); Reveal(status.transform);
                yield return "settings-time-picker-mock-unavailable-feedback";
            }
            finally
            {
                status.text = oldText; status.gameObject.SetActive(oldStatus);
                options.SetActive(oldOptions); sensor.enabled = oldEnabled;
            }
        }

        static void Reveal(Transform target)
        {
            var scroll = target.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content == null || !scroll.vertical) return;
            VisualAuditTraversal.Settle();
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            float shift = bounds.min.y < viewport.rect.yMin + 16 ? viewport.rect.yMin + 16 - bounds.min.y
                : bounds.max.y > viewport.rect.yMax - 16 ? viewport.rect.yMax - 16 - bounds.max.y : 0;
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + shift, 0, Mathf.Max(0, scroll.content.rect.height - viewport.rect.height));
            scroll.StopMovement(); scroll.content.anchoredPosition = position;
        }

        static T Field<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, Hidden);
            Require(field != null, "Missing form audit field: " + name);
            return (T)field.GetValue(target);
        }

        static void Require(bool value, string message) => VisualAuditTraversal.Require(value, message);
    }
}
#endif
