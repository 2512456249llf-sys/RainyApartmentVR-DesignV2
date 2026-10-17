using UnityEngine;
using UnityEngine.Rendering;

namespace RainyApartmentVR
{
    [ExecuteAlways]
    public sealed class DesignAmbient : MonoBehaviour
    {
        private void OnEnable()
        {
            // 没有烘焙光照贴图时提供稳定的室内间接光，防止夜景暗部全黑。
            var probe=new SphericalHarmonicsL2();
            probe.AddAmbientLight(new Color(.075f,.105f,.14f));
            RenderSettings.ambientProbe=probe;
        }
    }
}
