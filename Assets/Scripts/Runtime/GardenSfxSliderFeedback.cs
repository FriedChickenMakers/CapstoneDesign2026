using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // A single quiet preview on accepted pointer release, never on each drag
    // value. Keyboard adjustments use the ordinary volume-save debounce.
    [DisallowMultipleComponent, RequireComponent(typeof(Slider))]
    public sealed class GardenSfxSliderFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        Slider slider;
        bool pressed;
        bool cancelForFocusLoss;
        int pointerId;

        void Awake(){cancelForFocusLoss=Application.platform!=RuntimePlatform.Android;}

        public static void Attach(Slider target)
        {
            if(target==null)return;
            var helper=target.GetComponent<GardenSfxSliderFeedback>();
            if(helper==null)helper=target.gameObject.AddComponent<GardenSfxSliderFeedback>();
            helper.slider=target;
        }
        public void OnPointerDown(PointerEventData data)
        {
            if(!Application.isPlaying || data.button!=PointerEventData.InputButton.Left)return;
            if(slider==null)slider=GetComponent<Slider>();
            pressed=slider!=null && slider.IsActive() && slider.IsInteractable();pointerId=data.pointerId;
        }
        public void OnPointerUp(PointerEventData data)
        {
            if(!pressed || data.pointerId!=pointerId || data.button!=PointerEventData.InputButton.Left)return;
            pressed=false;
            if(!Application.isPlaying || slider==null || !slider.IsActive() || !slider.IsInteractable())return;
            GardenSfxPlayer.SaveVolume();GardenSfxPlayer.Play(GardenSfxCue.UiClick);
        }
        void OnApplicationPause(bool value){if(value)pressed=false;}
        void OnApplicationFocus(bool value){if(!value && cancelForFocusLoss)pressed=false;}
        void OnDisable(){pressed=false;}
    }
}
