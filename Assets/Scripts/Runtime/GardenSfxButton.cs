using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Button.onClick covers accepted pointer clicks and keyboard/controller
    // submit; pointer-down/cancel and disabled controls never enter this path.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class GardenSfxButton : MonoBehaviour
    {
        [SerializeField] GardenSfxCue cue=GardenSfxCue.UiClick;
        [SerializeField] bool suppressed;
        Button button;

        public static GardenSfxCue CueForLabel(string label)
        {
            string value=(label ?? "").Trim().TrimStart('‹').Trim();
            switch(value)
            {
                case "이전":case "이전 단계":case "나가기":case "닫기":case "돌아가기":case "정원으로":case "취소":
                    return GardenSfxCue.UiBack;
                default:return GardenSfxCue.UiClick;
            }
        }

        public static GardenSfxButton Attach(Button target,GardenSfxCue cue=GardenSfxCue.UiClick)
        {
            if(target==null)return null;
            var helper=target.GetComponent<GardenSfxButton>();
            if(helper==null)helper=target.gameObject.AddComponent<GardenSfxButton>();
            helper.cue=cue;helper.suppressed=false;helper.Bind(target);return helper;
        }

        // Success-dependent actions play their semantic cue from the result
        // handler. Suppressing their generic click also leaves failures honest.
        public static void Suppress(Button target)
        {
            if(target==null)return;
            var helper=Attach(target);helper.suppressed=true;
        }

        void OnEnable(){Bind(GetComponent<Button>());}
        void Bind(Button target)
        {
            if(button!=null)button.onClick.RemoveListener(Clicked);
            button=target;
            if(button!=null){button.onClick.RemoveListener(Clicked);button.onClick.AddListener(Clicked);}
        }
        void Clicked()
        {
            // An earlier navigation listener may have hidden this button by
            // now. Button already checked active/interactable before dispatch.
            if(!suppressed)GardenSfxPlayer.Play(cue);
        }
        void OnDestroy(){if(button!=null)button.onClick.RemoveListener(Clicked);}
    }
}
