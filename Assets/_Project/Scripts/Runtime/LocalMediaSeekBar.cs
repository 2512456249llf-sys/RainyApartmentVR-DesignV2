using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace RainyApartmentVR
{
    public sealed class LocalMediaFocusRelease : MonoBehaviour, IPointerUpHandler
    {
        public void OnPointerUp(PointerEventData value){if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);}
    }
    // 拖动期间只预览位置；松开时提交一次跳转，避免逐帧向解码器发寻址。
    public sealed class LocalMediaSeekBar : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IEndDragHandler
    {
        public bool IsDragging { get; private set; }
        private Slider slider;
        private Func<double> duration;
        private Action<float> commit;
        public void Configure(Slider value,Func<double> length,Action<float> action) { slider=value;duration=length;commit=action; }
        public void OnPointerDown(PointerEventData value) { if(slider&&slider.interactable)IsDragging=true; }
        public void OnPointerUp(PointerEventData value) => Finish();
        public void OnEndDrag(PointerEventData value) => Finish();
        private void Finish() { if(!IsDragging)return;IsDragging=false;if(duration()!=0)commit?.Invoke(slider.value); }
        private void OnDisable() => IsDragging=false;
    }
}
