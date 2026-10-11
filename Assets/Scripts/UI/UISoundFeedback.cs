using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TacticalRPG.Core;
namespace TacticalRPG.UI
{
    [DisallowMultipleComponent]
    public sealed class UISoundFeedback : MonoBehaviour,IPointerEnterHandler,ISelectHandler,IBeginDragHandler,IEndDragHandler,IScrollHandler
    {
        private Selectable _control; private Slider _slider; private Toggle _toggle; private Button _button;
        private void Awake()
        {
            _control=GetComponent<Selectable>();_slider=_control as Slider;_toggle=_control as Toggle;_button=_control as Button;
            if(_slider!=null)_slider.onValueChanged.AddListener(Slide);
            if(_toggle!=null)_toggle.onValueChanged.AddListener(Toggle);
            if(_button!=null)_button.onClick.AddListener(Click);
        }
        private void Start()=>PresentationTheme.StyleControl(_control);
        private void OnDestroy(){if(_slider!=null)_slider.onValueChanged.RemoveListener(Slide);if(_toggle!=null)_toggle.onValueChanged.RemoveListener(Toggle);if(_button!=null)_button.onClick.RemoveListener(Click);}
        private bool Ready=>_control==null||_control.IsInteractable();
        public void OnPointerEnter(PointerEventData e){if(Ready)SoundSignals.Play("ui_hover",.35f);}
        public void OnSelect(BaseEventData e){if(Ready)SoundSignals.Play("ui_focus",.3f);}
        public void OnBeginDrag(PointerEventData e){if(Ready&&_slider==null)SoundSignals.Play("item_drag");}
        public void OnEndDrag(PointerEventData e){if(Ready&&_slider==null)SoundSignals.Play("item_drop");}
        public void OnScroll(PointerEventData e){if(Ready)SoundSignals.Play("scroll",.3f);}
        private void Slide(float v){if(isActiveAndEnabled&&Ready&&(UnityEngine.Input.GetMouseButton(0)||EventSystem.current?.currentSelectedGameObject==gameObject))SoundSignals.Play("ui_slider",.5f);}
        private void Toggle(bool v){if(isActiveAndEnabled&&Ready)SoundSignals.Play("ui_toggle");}
        private void Click(){if(Ready)SoundSignals.Play("ui_click");}
    }
}
