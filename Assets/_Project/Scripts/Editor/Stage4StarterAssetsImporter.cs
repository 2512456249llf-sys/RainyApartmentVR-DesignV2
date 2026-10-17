using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace RainyApartmentVR.Editor
{
    public static class Stage4StarterAssetsImporter
    {
        private const string PackageName = "com.unity.xr.interaction.toolkit";
        private const string PackageVersion = "3.3.0";

        public static void ImportFromCommandLine()
        {
            Sample starterAssets = Sample.FindByPackage(PackageName, PackageVersion)
                .FirstOrDefault(sample => sample.displayName == "Starter Assets");

            if (string.IsNullOrEmpty(starterAssets.displayName))
            {
                throw new System.InvalidOperationException("找不到 XR Interaction Toolkit 的 Starter Assets。");
            }

            if (!starterAssets.isImported && !starterAssets.Import(Sample.ImportOptions.OverridePreviousImports))
            {
                throw new System.InvalidOperationException("Starter Assets 导入失败。");
            }

            AssetDatabase.Refresh();
            Debug.Log("阶段 4：官方 Starter Assets 已导入。");
        }
    }
}
