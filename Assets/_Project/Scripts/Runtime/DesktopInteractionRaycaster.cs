using UnityEngine;
using UnityEngine.InputSystem;

namespace RainyApartmentVR
{
    public sealed class DesktopInteractionRaycaster : MonoBehaviour
    {
        [SerializeField] private float interactionDistance = 3f;

        private void Update()
        {
            bool pressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            if (!pressed)
            {
                return;
            }

            foreach (var prop in FindObjectsByType<GrabbableProp>(FindObjectsSortMode.None))
                if (prop.IsHeld) { prop.Drop(); return; }

            if (LocalMediaCenter.TryInteractFromView(transform)) return;

            if (!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit,
                    interactionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            MonoBehaviour[] behaviours = hit.collider.GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IPlayerInteractable interactable)
                {
                    interactable.Interact();
                    return;
                }
            }
        }
    }
}
