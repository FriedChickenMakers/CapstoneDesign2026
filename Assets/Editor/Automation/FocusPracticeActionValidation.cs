using System;
using System.Linq;
using CapstoneDesign.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Called by the isolated QA integration entry. Uses the existing preview
    // fixture and memory store; never opens device data or real preferences.
    internal static class FocusPracticeActionValidation
    {
        public static void Untimed(FocusMissionValidation.Report report, Action<bool,string> check, bool capture)
        {
            foreach (int height in new[] { 1688, 1300 })
            foreach (string code in new[] { "M01", "M03", "M04", "M06", "M16" })
            using (var f = new FocusMissionValidation.Fixture(report))
            {
                f.Resize(780,height);
                string id = "untimed-action-" + code + "-" + height;
                check(f.Service.BeginFocusSession(id,code),f.Service.LastError);
                f.Invoke("SelectFocusSession",id);
                check(string.IsNullOrEmpty(f.Latest.StartedUtc),"Opening the overview is not a session start.");
                string actionName = code == "M01" ? "Focus.IconAction" : "Focus.Action";
                f.Click(actionName);
                string started = f.Latest.StartedUtc;
                check(!string.IsNullOrEmpty(started),"The existing overview action still starts the session.");
                if (code == "M01") f.Click(actionName); // Optional method choice -> the original single light action.
                int target = code == "M03" || code == "M06" || code == "M16" ? 1 : 0;
                while (f.Latest.InstructionStep < target) f.Click("Focus.Action");
                f.Invoke("ShowFocusPractice");
                check(f.Ui.GetComponentsInChildren<Button>(false).All(b=>b.name!="Focus.PracticeStart"),
                    code+": no duplicate untimed start control.");
                check(f.Ui.GetComponentsInChildren<Button>(false).Count(b=>b.name==actionName)==1,
                    code+": exactly one completion action, using its current footer or illustrated presentation.");
                if (code == "M01") check(f.Ui.GetComponentsInChildren<Button>(false).All(b=>b.name!="Focus.Action"),
                    "M01: the illustrated action has no duplicate footer completion button.");
                check(f.Ui.GetComponentInChildren<FocusPracticeClock>(false)==null,
                    code+": an untimed activity does not acquire a countdown.");
                check(f.Latest.StartedUtc==started && string.IsNullOrEmpty(f.Latest.FocusStepRecords[target].PracticeStartedUtc),
                    code+": rerender preserves session start and does not invent a step-start observation.");
                var action=f.Ui.GetComponentsInChildren<Button>(false).Single(b=>b.name==actionName);
                var actionBounds=Bounds((RectTransform)action.transform);
                foreach(var other in action.transform.parent.GetComponentsInChildren<Button>(false).Where(b=>b!=action))
                    check(!Overlaps(actionBounds,Bounds((RectTransform)other.transform)),code+": completion overlaps footer control "+other.name);
                foreach(var text in action.transform.parent.GetComponentsInChildren<Text>(false)
                    .Where(t=>t.transform.parent==action.transform.parent))
                    check(!Overlaps(actionBounds,Bounds(text.rectTransform)),code+": completion overlaps its footer hint.");
                if(code=="M03" || code=="M04")f.Capture("qa-untimed-single-action-"+code+"-"+height,capture);
                f.Click(actionName);
                var saved=f.Latest.FocusStepRecords[target];
                check(!string.IsNullOrEmpty(saved.ConfirmedUtc),code+": existing completion works without a new mandatory start tap.");
                check(string.IsNullOrEmpty(saved.PracticeStartedUtc),code+": completion must not fabricate an earlier step start.");
                check(f.Latest.StartedUtc==started,code+": existing session-start timestamp is preserved.");
            }
        }

        public static void Timed(FocusMissionValidation.Report report, Action<bool,string> check)
        {
            using(var f=new FocusMissionValidation.Fixture(report))
            {
                check(f.Service.BeginFocusSession("timed-action-preserved","M17"),f.Service.LastError);
                f.Invoke("SelectFocusSession","timed-action-preserved");
                f.Click("Focus.Action");f.Click("Focus.Action");
                var clock=f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                var shorten=f.Ui.GetComponentsInChildren<Button>(false).Single(b=>b.name=="Focus.TooLong");
                check(clock!=null && !clock.HasStarted && !clock.Running && !shorten.interactable,
                    "A countdown still requires an actual timer action before shortening is allowed.");
                check(string.IsNullOrEmpty(f.Latest.FocusStepRecords[1].PracticeStartedUtc),"Rendering a timed activity does not start practice.");
                f.Click("Focus.TimerToggle");
                check(clock.HasStarted && clock.Running && shorten.interactable
                    && !string.IsNullOrEmpty(f.Latest.FocusStepRecords[1].PracticeStartedUtc),"Timer start retains its real interaction marker.");
                clock.ElapsedSeconds=12;f.Clock.UtcNow=f.Clock.UtcNow.AddSeconds(12);
                check(clock.Flush(),"Actual timed practice persists.");
                f.Click("Focus.TooLong");check(!clock.Running,"Shortening modal pauses the countdown.");
                f.Click("Focus.AdjustmentCancel");check(clock.Running,"Cancel restores the running countdown.");
                f.Click("Focus.TooLong");f.Click("Focus.AdjustmentConfirm");
                var saved=f.Latest.FocusStepRecords[1];
                check(saved.EndReason=="too_long" && saved.ElapsedSeconds==12 && !string.IsNullOrEmpty(saved.ConfirmedUtc),
                    "Shortening retains actual time and the agreed completion contract.");
            }
        }

        static Rect Bounds(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            float left=corners.Min(p=>p.x),bottom=corners.Min(p=>p.y);
            return new Rect(left,bottom,corners.Max(p=>p.x)-left,corners.Max(p=>p.y)-bottom);
        }
        static bool Overlaps(Rect a,Rect b) => Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin)>.01f
            && Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin)>.01f;
    }
}
