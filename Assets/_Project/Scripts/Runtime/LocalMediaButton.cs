using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace RainyApartmentVR
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class LocalMediaButton : MonoBehaviour, IPlayerInteractable
    {
        private XRSimpleInteractable interactable;
        private Action pressed;
        private float nextPress;
        public void Configure(Action callback) => pressed = callback;
        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnSelected);
        }
        private void OnSelected(SelectEnterEventArgs args) => Interact();
        public void Interact()
        {
            if (Time.unscaledTime < nextPress) return;
            nextPress = Time.unscaledTime + .22f;
            pressed?.Invoke();
        }
        private void OnDestroy() { if (interactable) interactable.selectEntered.RemoveListener(OnSelected); }
    }
}
