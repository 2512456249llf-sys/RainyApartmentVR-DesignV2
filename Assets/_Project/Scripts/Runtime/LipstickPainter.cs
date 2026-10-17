using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RainyApartmentVR
{
    public sealed class LipstickPainter : MonoBehaviour
    {
        public Transform tip;
        public bool faceOnly;
        private GrabbableProp desktopGrab;
        private XRGrabInteractable handGrab;
        private UnityEngine.XR.InputDevice device;
        private LipstickDrawingMemory.Stroke stroke;
        private DrawingSurface lastSurface;
        private Vector3 lastPoint;
        private FacePaintSurface face;
        private SkinnedPaintSurface[] bodySurfaces;
        private bool activationPressed;
        public bool IsDrawing { get; private set; }
        private void Awake()
        {
            desktopGrab = GetComponent<GrabbableProp>();
            handGrab = GetComponent<XRGrabInteractable>();
            if (handGrab) { handGrab.selectEntered.AddListener(OnGrabbed); handGrab.selectExited.AddListener(OnReleased); handGrab.activated.AddListener(OnActivated); handGrab.deactivated.AddListener(OnDeactivated); }
        }
        private void OnGrabbed(SelectEnterEventArgs args)
        {
            var hand = args.interactorObject.transform.GetComponentInParent<VRHandPose>();
            bool left = hand ? hand.IsLeftHand : args.interactorObject.transform.GetComponentsInParent<Transform>(true).Any(t => t.name.Contains("Left") || t.name.Contains("左"));
            device = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
        }
        private void OnReleased(SelectExitEventArgs args) { activationPressed = false; EndDrawing(); }
        private void OnActivated(ActivateEventArgs args) { activationPressed = true; }
        private void OnDeactivated(DeactivateEventArgs args) { activationPressed = false; EndDrawing(); }
        private void Update()
        {
            bool desktop = desktopGrab && desktopGrab.IsHeld;
            bool vr = handGrab && handGrab.isSelected;
            bool pressed = desktop && Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (vr && activationPressed) pressed = true;
            if (vr && device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float trigger)) pressed |= trigger > .55f;
            if ((!desktop && !vr) || !pressed || !tip) { EndDrawing(); return; }
            Ray ray = desktop && Camera.main ? new Ray(Camera.main.transform.position, Camera.main.transform.forward)
                : new Ray(tip.position - tip.up * .025f, tip.up);
            float distance = desktop ? 2.5f : .06f;
            DrawingSurface target = null;
            Vector3 point = Vector3.zero, normal = Vector3.zero;
            float closest = distance;
            SkinnedPaintSurface.Binding binding = null;
            foreach (var hit in Physics.RaycastAll(ray, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= closest) continue;
                var candidate = hit.collider.GetComponentInParent<DrawingSurface>();
                if (!candidate || faceOnly) continue;
                target = candidate; point = hit.point; normal = hit.normal; closest = hit.distance;
            }
            if (!face) face = FindFirstObjectByType<FacePaintSurface>();
            if (face && face.Raycast(ray, closest, out var facePoint, out var faceNormal))
            { target = face.Surface; point = facePoint; normal = faceNormal; closest = Vector3.Distance(ray.origin, point); }
            if (!faceOnly)
            {
                if (bodySurfaces == null) bodySurfaces = FindObjectsByType<SkinnedPaintSurface>(FindObjectsSortMode.None);
                foreach (var surface in bodySurfaces)
                    if (surface && surface.Raycast(ray, closest + .001f, out var bodyPoint, out var bodyNormal, out var bodyBinding))
                    { target = surface.Surface; point = bodyPoint; normal = bodyNormal; binding = bodyBinding; closest = Vector3.Distance(ray.origin, point); }
            }
            if (!target) { EndDrawing(); return; }
            DrawAt(target, point + normal * .0018f, binding);
        }
        public bool DrawAt(DrawingSurface surface, Vector3 point, SkinnedPaintSurface.Binding binding = null)
        {
            var memory = LipstickDrawingMemory.Instance;
            if (!memory || !surface) return false;
            if (stroke == null || lastSurface != surface || Vector3.Distance(lastPoint, point) > .08f)
                stroke = memory.BeginStroke(surface);
            if (stroke == null) return false;
            if (!memory.AddPoint(stroke, surface, point, binding)) { EndDrawing(); return false; }
            lastSurface = surface;
            lastPoint = point;
            IsDrawing = true;
            return true;
        }
        public void EndDrawing()
        {
            if (IsDrawing) LipstickDrawingMemory.Instance?.SaveNow();
            IsDrawing = false; stroke = null; lastSurface = null;
        }
        private void OnDisable() { EndDrawing(); }
        private void OnDestroy()
        {
            if (handGrab) { handGrab.selectEntered.RemoveListener(OnGrabbed); handGrab.selectExited.RemoveListener(OnReleased); handGrab.activated.RemoveListener(OnActivated); handGrab.deactivated.RemoveListener(OnDeactivated); }
        }
    }
}
