using UnityEngine;
using UnityEngine.XR.Management;

namespace RainyApartmentVR
{
    public sealed class PlayerModeBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject desktopPlayer;
        [SerializeField] private GameObject xrPlayer;

        public void Configure(GameObject desktop, GameObject xr)
        {
            desktopPlayer = desktop;
            xrPlayer = xr;
        }

        private void Awake()
        {
            XRManagerSettings manager = XRGeneralSettings.Instance != null
                ? XRGeneralSettings.Instance.Manager
                : null;
            bool xrIsReady = manager != null && manager.activeLoader != null;

            if (xrPlayer != null)
            {
                xrPlayer.SetActive(xrIsReady);
            }

            if (desktopPlayer != null)
            {
                desktopPlayer.SetActive(!xrIsReady);
            }

            Debug.Log(xrIsReady
                ? "检测到 OpenXR 设备：已启用 PC VR 模式。"
                : "未检测到 OpenXR 设备：已启用桌面测试模式。");
        }
    }
}
