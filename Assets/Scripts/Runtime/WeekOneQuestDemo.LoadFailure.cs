using System;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        void ShowGardenLoadFailure(string key, string title, Action retryPage)
        {
            var root = BeginFigmaPage(key, retryPage, FigmaMissionUi.Background, true, title);
            FigmaHeader(root, title, "");
            FigmaMissionUi.Label(root, "저장된 내용을 불러오지 못했어요.\n기존 기록은 그대로 보관하고 있어요.",
                24, 184, 342, 100, 18, FigmaMissionUi.Ink).name = "State.LoadFailure";
            FigmaMissionUi.Button(root, "다시 불러오기", 24, 312, 342, 56, () =>
            {
                if (Service == null || root == null || view != root.gameObject) return;
                opened = Service.Open();
                error = opened ? null : Service.LastError;
                if (opened && !HasPendingFullReset) { RefreshCycle(); UpdateGarden(); }
                if (HasPendingFullReset) ShowFullResetRecovery(); else retryPage();
            }).name = "State.RetryLoad";
        }
    }
}
