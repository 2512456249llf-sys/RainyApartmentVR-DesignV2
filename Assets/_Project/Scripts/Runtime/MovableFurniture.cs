using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RainyApartmentVR
{
    // 手柄抓取由原交互系统负责；家具松手后保持朝上，不被抛飞。
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class MovableFurniture : MonoBehaviour, IPlayerInteractable
    {
        private Rigidbody body;
        private XRGrabInteractable grab;
        private bool desktopHeld;
        private Transform desktopView;
        public bool HasHandGrab => grab != null;
        public bool stayWhereReleased = true;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            grab.selectEntered.AddListener(_ => desktopHeld = false);
            grab.selectExited.AddListener(_ => Settle());
            if (stayWhereReleased) Settle();
        }

        public void Interact()
        {
            if (grab && grab.isSelected) return;
            if (desktopHeld) { desktopHeld = false; Settle(); return; }
            if (!Camera.main) return;
            desktopView = Camera.main.transform;
            desktopHeld = true;
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void FixedUpdate()
        {
            if (!desktopHeld || !desktopView) return;
            Vector3 forward = Vector3.ProjectOnPlane(desktopView.forward, Vector3.up).normalized;
            body.MovePosition(Vector3.Lerp(body.position, desktopView.position + forward * 1.2f - Vector3.up * .55f, .3f));
            if (forward.sqrMagnitude > .1f) body.MoveRotation(Quaternion.LookRotation(-forward, Vector3.up));
        }

        public void Settle()
        {
            body.isKinematic = stayWhereReleased;
            body.useGravity = !stayWhereReleased;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            if (!stayWhereReleased) body.WakeUp();
            GetComponent<PersistentPlacement>()?.SaveNow();
        }
    }
}
