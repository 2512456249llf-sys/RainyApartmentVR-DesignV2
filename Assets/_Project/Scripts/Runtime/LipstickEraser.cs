using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace RainyApartmentVR
{
    // 方巾拿在手里接触笔迹时擦除，桌面试玩拿住后按住左键擦拭。
    [DefaultExecutionOrder(13500)]
    public sealed class LipstickEraser : MonoBehaviour
    {
        public float radius = .055f;
        private GrabbableProp desktop;
        private XRGrabInteractable grab;
        private bool changed;
        private void Awake() { desktop = GetComponent<GrabbableProp>(); grab = GetComponent<XRGrabInteractable>(); }
        private void LateUpdate()
        {
            var memory = LipstickDrawingMemory.Instance;
            if (!memory) return;
            if (grab && grab.isSelected)
            {
                changed |= memory.EraseNear(transform.position, radius) > 0;
                return;
            }
            if (desktop && desktop.IsHeld && Mouse.current != null && Mouse.current.leftButton.isPressed && Camera.main)
            {
                var ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
                float closest = 2.5f; bool found = Physics.Raycast(ray, out var hit, closest, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                Vector3 point = found ? hit.point : Vector3.zero;
                if (found) closest = hit.distance;
                foreach (var skin in FindObjectsByType<SkinnedPaintSurface>(FindObjectsSortMode.None))
                    if (skin.Raycast(ray, closest, out var p, out _, out _)) { point = p; closest = Vector3.Distance(ray.origin, p); found = true; }
                if (found) changed |= memory.EraseNear(point, radius) > 0;
                return;
            }
            if (changed) { memory.SaveNow(); changed = false; }
        }
        private void OnDisable() { if (changed) LipstickDrawingMemory.Instance?.SaveNow(); changed = false; }
    }
}
