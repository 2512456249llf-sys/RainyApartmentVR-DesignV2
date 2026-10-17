using UnityEngine;
namespace RainyApartmentVR
{
    // 编号保存在场景中；笔迹以表面局部坐标记录，移动物品时仍附着原表面。
    public sealed class DrawingSurface : MonoBehaviour
    {
        public string stableId;
        public Transform Anchor => transform;
    }
}
