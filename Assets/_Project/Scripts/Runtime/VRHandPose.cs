using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace RainyApartmentVR
{
    // 手部跟随现有手柄追踪节点；握把和扳机分别控制手指弯曲。
    public sealed class VRHandPose : MonoBehaviour
    {
        [SerializeField] private bool leftHand;
        [SerializeField] private Transform[] fingerRoots;
        [SerializeField] private Transform[] fingerTips;

        private Quaternion[] rootRest;
        private Quaternion[] tipRest;
        private InputDevice device;
        private SphereCollider touchProbe;
        private readonly List<SphereCollider> touchProbes = new List<SphereCollider>();
        private CharacterTouchSurfaces touchSurfaces;
        private Vector3 trackedLocalPosition;
        private Quaternion trackedLocalRotation;
        private Vector3 previousTrackedPosition;
        private bool hasPreviousTrackedPosition;
        private bool touchingCharacter;
        private float nextTouchPulse;
        private int touchLookupFrame;
        private float grip;
        private float trigger;
        private XRBaseInteractor[] interactors;
        public bool IsHoldingObject
        {
            get
            {
                if (interactors == null) interactors = (transform.parent ? transform.parent : transform).GetComponentsInChildren<XRBaseInteractor>(true);
                foreach (var interactor in interactors) if (interactor && interactor.hasSelection) return true;
                return false;
            }
        }

        public bool IsLeftHand => leftHand;
        public float GripValue => grip;
        public Vector3 ContactDisplacement => Application.isPlaying && transform.parent
            ? transform.position - transform.parent.TransformPoint(trackedLocalPosition) : Vector3.zero;

        public void Configure(bool isLeft, Transform[] roots, Transform[] tips)
        {
            leftHand = isLeft;
            fingerRoots = roots;
            fingerTips = tips;
            CacheRestPose();
            EnsureTouchProbe();
        }

        private void Awake()
        {
            CacheRestPose();
            EnsureTouchProbe();
        }

        private void CacheRestPose()
        {
            trackedLocalPosition = transform.localPosition;
            trackedLocalRotation = transform.localRotation;
            if (fingerRoots == null || fingerTips == null || fingerRoots.Length != 5 || fingerTips.Length != 5) return;
            rootRest = new Quaternion[5];
            tipRest = new Quaternion[5];
            for (int i = 0; i < 5; i++)
            {
                rootRest[i] = fingerRoots[i].localRotation;
                tipRest[i] = fingerTips[i].localRotation;
            }
        }

        private void LateUpdate()
        {
            transform.localPosition = trackedLocalPosition;
            transform.localRotation = trackedLocalRotation;
            if (rootRest == null) return;
            if (!device.isValid) device = InputDevices.GetDeviceAtXRNode(leftHand ? XRNode.LeftHand : XRNode.RightHand);
            float targetGrip = 0f;
            float targetTrigger = 0f;
            if (device.isValid)
            {
                device.TryGetFeatureValue(CommonUsages.grip, out targetGrip);
                device.TryGetFeatureValue(CommonUsages.trigger, out targetTrigger);
            }
            grip = Mathf.MoveTowards(grip, Mathf.Clamp01(targetGrip), Time.deltaTime * 8f);
            trigger = Mathf.MoveTowards(trigger, Mathf.Clamp01(targetTrigger), Time.deltaTime * 10f);
            for (int i = 0; i < 5; i++)
            {
                float curl = i == 1 ? trigger : i == 0 ? grip * .75f : grip;
                float firstAngle = i == 0 ? 42f : 66f;
                float secondAngle = i == 0 ? 36f : 58f;
                fingerRoots[i].localRotation = rootRest[i] * Quaternion.Euler(firstAngle * curl, 0f, 0f);
                fingerTips[i].localRotation = tipRest[i] * Quaternion.Euler(secondAngle * curl, 0f, 0f);
            }
            ResolveCharacterTouch();
        }

        private void EnsureTouchProbe()
        {
            touchProbes.Clear();
            var node = transform.Find("虚拟手触摸探测");
            if (!node)
            {
                node = new GameObject("虚拟手触摸探测").transform;
                node.SetParent(transform, false);
            }
            node.localPosition = new Vector3(0f, 0f, .035f);
            var collider = node.GetComponent<SphereCollider>();
            if (!collider) collider = node.gameObject.AddComponent<SphereCollider>();
            collider.radius = .031f;
            collider.isTrigger = true;
            touchProbe = collider;
            touchProbes.Add(collider);

            if (fingerTips == null) return;
            for (int i = 0; i < fingerTips.Length; i++)
            {
                var finger = fingerTips[i];
                if (!finger) continue;
                var distal = finger.Find("末节");
                var renderer = distal ? distal.GetComponent<Renderer>() : null;
                if (!renderer) continue;

                var tipNode = finger.Find("指尖触摸探测");
                if (!tipNode)
                {
                    tipNode = new GameObject("指尖触摸探测").transform;
                    tipNode.SetParent(finger, false);
                }
                Vector3 extents = renderer.bounds.extents;
                Vector3 direction = finger.forward;
                float halfLength = Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.y) * extents.y + Mathf.Abs(direction.z) * extents.z;
                tipNode.position = renderer.bounds.center + direction * halfLength * .92f;
                var tipCollider = tipNode.GetComponent<SphereCollider>();
                if (!tipCollider) tipCollider = tipNode.gameObject.AddComponent<SphereCollider>();
                tipCollider.radius = Mathf.Clamp(Mathf.Max(extents.x, extents.y) * .78f, .006f, .011f);
                tipCollider.isTrigger = true;
                touchProbes.Add(tipCollider);
            }
        }

        private void ResolveCharacterTouch()
        {
            if (Time.frameCount >= touchLookupFrame || !touchSurfaces)
            {
                touchSurfaces = FindFirstObjectByType<CharacterTouchSurfaces>();
                touchLookupFrame = Time.frameCount + 120;
            }

            Vector3 trackedPosition = transform.position;
            Vector3 handMovement = hasPreviousTrackedPosition ? trackedPosition - previousTrackedPosition : Vector3.zero;
            previousTrackedPosition = trackedPosition;
            hasPreviousTrackedPosition = true;
            bool touching = false;
            Collider strongestSurface = null;
            Vector3 strongestNormal = Vector3.up;
            float strongestPenetration = 0f;
            if (touchSurfaces && touchProbe &&
                (touchProbe.transform.position - touchSurfaces.transform.position).sqrMagnitude < 1.7f * 1.7f)
            {
                Physics.SyncTransforms();
                var origin = trackedPosition;
                for (int pass = 0; pass < 4; pass++)
                {
                    bool corrected = false;
                    foreach (var probe in touchProbes)
                    {
                        if (!probe || !probe.enabled) continue;
                        foreach (var surface in touchSurfaces.Surfaces)
                        {
                            if (!surface || !surface.enabled) continue;
                            if (!Physics.ComputePenetration(probe, probe.transform.position, probe.transform.rotation,
                                    surface, surface.transform.position, surface.transform.rotation, out var direction, out float distance)) continue;

                            if (distance > strongestPenetration)
                            {
                                strongestPenetration = distance;
                                strongestSurface = surface;
                                strongestNormal = direction;
                            }
                            float correction = Mathf.Min(distance + .0015f, .045f);
                            Vector3 proposed = transform.position + direction * correction;
                            Vector3 fromTrackedPose = proposed - origin;
                            if (fromTrackedPose.magnitude > .14f) proposed = origin + fromTrackedPose.normalized * .14f;
                            transform.position = proposed;
                            touching = true;
                            corrected = true;
                        }
                    }
                    if (!corrected) break;
                }
            }

            bool contactStarted = touching && !touchingCharacter;
            if (touchSurfaces)
                touchSurfaces.GetComponent<CharacterTouchPerception>()?.Report(leftHand, touching && !IsHoldingObject ? strongestSurface : null,
                    trackedPosition, handMovement, strongestNormal, Time.unscaledTime);
            if (touching && touchSurfaces)
                touchSurfaces.NotifyTouched(strongestSurface, handMovement, strongestNormal, contactStarted);
            touchingCharacter = touching;
            if (touching && Time.unscaledTime >= nextTouchPulse)
            {
                if (!device.isValid) device = InputDevices.GetDeviceAtXRNode(leftHand ? XRNode.LeftHand : XRNode.RightHand);
                if (device.isValid && device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
                {
                    float speed = handMovement.magnitude / Mathf.Max(Time.unscaledDeltaTime, .001f);
                    float stroke = Mathf.Clamp01(speed / .35f);
                    float amplitude = contactStarted ? .27f : Mathf.Lerp(.08f, .19f, stroke);
                    device.SendHapticImpulse(0, amplitude, contactStarted ? .055f : .032f);
                    nextTouchPulse = Time.unscaledTime + (stroke > .12f ? .16f : .52f);
                }
                else nextTouchPulse = Time.unscaledTime + .52f;
            }
        }
    }
}
