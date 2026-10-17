using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RainyApartmentVR
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class DoorInteractable : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float rotationSpeed = 180f;

        private XRSimpleInteractable xrInteractable;
        private Quaternion closedRotation;
        private Quaternion openRotation;
        private bool isOpen;
        private bool initialized;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            InitializeIfNeeded();
            xrInteractable = GetComponent<XRSimpleInteractable>();
            xrInteractable.selectEntered.AddListener(OnVrSelected);
        }

        private void OnDestroy()
        {
            if (xrInteractable != null)
            {
                xrInteractable.selectEntered.RemoveListener(OnVrSelected);
            }
        }

        private void Update()
        {
            InitializeIfNeeded();
            Quaternion target = isOpen ? openRotation : closedRotation;
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation, target, rotationSpeed * Time.deltaTime);
        }

        public void Interact()
        {
            isOpen = !isOpen;
        }

        public void SetOpenImmediate(bool open)
        {
            InitializeIfNeeded();
            isOpen = open;
            transform.localRotation = open ? openRotation : closedRotation;
            Physics.SyncTransforms();
        }

        private void OnVrSelected(SelectEnterEventArgs arguments)
        {
            Interact();
        }

        private void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            closedRotation = transform.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
            initialized = true;
        }
    }
}
