using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Prototype;

namespace CapstoneDesign.Runtime
{
    public sealed partial class SensorRawDisplay
    {
        GameObject debugPasswordPanel;
        InputField debugPassword;
        Text debugGateStatus,qaModeStatus;
        Button qaFlagButton;
        void BuildDebugAccessControls(Transform parent,Transform tools)
        {
            debugPasswordPanel=Section(parent,"Debug access","디버그 모드 잠금 해제").gameObject;
            Copy(debugPasswordPanel.transform,"처음 한 번 비밀번호를 입력해 주세요.",GardenUi.BodySize,GardenUi.Muted);
            debugPassword=FigmaMissionUi.Input(debugPasswordPanel.transform,"비밀번호",0,0,300,48,8);
            debugPassword.name="Settings.DebugPassword";debugPassword.contentType=InputField.ContentType.Password;
            debugPassword.keyboardType=TouchScreenKeyboardType.NumberPad;
            debugPassword.inputType=InputField.InputType.Password;
            var size=debugPassword.gameObject.AddComponent<LayoutElement>();size.preferredHeight=56;
            Control(debugPasswordPanel.transform,"잠금 해제",()=>
            {
                string value=debugPassword.text;debugPassword.SetTextWithoutNotify("");
                if(!DebugAccess.Enter(value)){debugGateStatus.text="비밀번호 또는 저장 상태를 확인해 주세요.";return;}
                debugPasswordPanel.SetActive(false);OpenAuthorizedDebugTools();
            }).name="Settings.DebugUnlock";
            Control(debugPasswordPanel.transform,"취소",()=>{debugPassword.SetTextWithoutNotify("");debugPasswordPanel.SetActive(false);}).name="Settings.DebugUnlockCancel";
            debugGateStatus=Copy(debugPasswordPanel.transform,"",GardenUi.CaptionSize,GardenUi.Warning);
            debugPasswordPanel.SetActive(false);
            Copy(tools,"발표·기능 검사 도구",GardenUi.HeadingSize,GardenUi.Green);
            qaFlagButton=SwitchRow(tools,"QA 데이터로 수집",()=>
            {
                if(!DebugAccess.Enabled)return;
                if(!DebugAccess.SetQa(!DebugAccess.Qa))qaModeStatus.text="설정을 저장하지 못했어요. 다시 시도해 주세요.";
                else RefreshDebugAccess();
            });qaFlagButton.name="Settings.QaFlag";
            qaModeStatus=Copy(tools,"",GardenUi.CaptionSize,GardenUi.Muted);
            RowControl(tools,"P/M 미션 시연",()=>
            {
                if(!DebugAccess.Enabled)return;
                transform.root.GetComponent<MockupNavigation>()?.ShowActivities();Loop?.ShowMissionDebugPicker();
            }).name="Settings.MissionDemo";
            RowControl(tools,"정원의 시간",OpenVisualDebug).name="Settings.GardenTimeDebug";
            RowControl(tools,"디버그 모드 끄기",DisableDebugTools).name="Settings.DebugExit";
            RefreshDebugAccess();
        }
        void DisableDebugTools()
        {
            if(!DebugAccess.Enabled)return;
            Loop?.RestoreMissionDebug();
            if(Loop?.DebugWalkEnabled==true && !Loop.ToggleDebugWalk())return;
            GardenVisualClock.UseSystemTime();
            if(!DebugAccess.Exit()){qaModeStatus.text="설정을 저장하지 못했어요.";return;}
            CancelMissionReset();SetDisclosure(diagnosticsDisclosure,false,false);SetDisclosure(testToolsDisclosure,false,false);
            RefreshDebugAccess();
        }
        public void RequestDebugTools()
        {
            if(DebugAccess.Enabled){DisableDebugTools();return;}
            if(DebugAccess.Authorized)
            {
                if(DebugAccess.Enter())OpenAuthorizedDebugTools();
                return;
            }
            debugPasswordPanel.SetActive(true);debugGateStatus.text="";debugPassword.SetTextWithoutNotify("");
            // Reveal the newly appended form before the user opens the keyboard.
            // Auto-focus here used to open an IME while the entire form was offscreen.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(PrimaryScroll.content);
            PrimaryScroll.StopMovement();
            PrimaryScroll.verticalNormalizedPosition=0;
        }
        void OpenAuthorizedDebugTools()
        {
            SetDisclosure(testToolsDisclosure,true,false,TestToolsToggleButton);
            RefreshDebugAccess();RefreshText();
        }
        void RefreshDebugAccess()
        {
            UpdateSwitch(TestToolsToggleButton,DebugAccess.Enabled);
            if(qaFlagButton==null)return;
            UpdateSwitch(qaFlagButton,DebugAccess.Qa);
            qaModeStatus.text=DebugAccess.Qa?"지금 생성되는 기록은 QA로 분류돼요.":"지금 생성되는 기록은 일반 데이터로 분류돼요.";
        }
    }
}
