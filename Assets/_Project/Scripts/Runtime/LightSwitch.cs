using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RainyApartmentVR
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class LightSwitch : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private Light targetLight;
        [SerializeField] private Renderer indicatorRenderer;
        [SerializeField] private bool startsOn = true;

        private XRSimpleInteractable xrInteractable;
        private bool isOn;

        public bool IsOn => isOn;

        public void Configure(Light lightToControl, Renderer indicator, bool initialState)
        {
            targetLight = lightToControl;
            indicatorRenderer = indicator;
            startsOn = initialState;
        }

        private void Awake()
        {
            xrInteractable = GetComponent<XRSimpleInteractable>();
            xrInteractable.selectEntered.AddListener(OnVrSelected);
            SetState(startsOn);
        }

        private void OnDestroy()
        {
            if (xrInteractable != null)
            {
                xrInteractable.selectEntered.RemoveListener(OnVrSelected);
            }
        }

        public void Interact()
        {
            SetState(!isOn);
        }

        public void SetState(bool enabled)
        {
            isOn = enabled;
            if (targetLight != null)
            {
                targetLight.enabled = isOn;
            }

            if (indicatorRenderer != null)
            {
                var properties = new MaterialPropertyBlock();
                indicatorRenderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", isOn
                    ? new Color(1f, 0.72f, 0.3f)
                    : new Color(0.16f, 0.18f, 0.22f));
                indicatorRenderer.SetPropertyBlock(properties);
            }
        }

        private void OnVrSelected(SelectEnterEventArgs arguments)
        {
            Interact();
        }
    }
}
