using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;

namespace RainyApartmentVR
{
    public sealed class PersistentPlacement : MonoBehaviour
    {
        public string stableId;
        private Vector3 rememberedPosition, rememberedScale;
        private Quaternion rememberedRotation;
        private XRGrabInteractable grab;
        public bool HasChanged => Vector3.Distance(transform.position, rememberedPosition) > .0005f
            || Quaternion.Angle(transform.rotation, rememberedRotation) > .1f
            || (transform.localScale - rememberedScale).sqrMagnitude > .000001f;
        private void Start()
        {
            ScenePlacementMemory.Instance?.Register(this);
            grab = GetComponent<XRGrabInteractable>();
            if (grab) grab.selectExited.AddListener(OnReleased);
        }
        public void RememberCurrent()
        {
            rememberedPosition = transform.position;
            rememberedRotation = transform.rotation;
            rememberedScale = transform.localScale;
        }
        private void OnReleased(SelectExitEventArgs args) { SaveNow(); }
        public void SaveNow() { ScenePlacementMemory.Instance?.MarkChanged(); ScenePlacementMemory.Instance?.SaveNow(); }
        private void OnDestroy() { if (grab) grab.selectExited.RemoveListener(OnReleased); }
    }
}
