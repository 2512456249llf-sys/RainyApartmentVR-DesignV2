character.fbx —— VR 乙女游戏角色（身高 1.87m，Blender 导出）

【导入设置建议】
- Rig: 选 Humanoid（骨骼已用标准命名，能自动映射，无需手动绑骨）
  Hips / Spine / Chest / UpperChest / Neck / Head
  LeftUpperArm / LeftLowerArm / LeftHand / LeftUpperLeg / LeftLowerLeg / LeftFoot / LeftToes
  LeftThumbProximal / LeftThumbDistal / LeftIndexProximal ... （五指各 2 节，共 49 骨）
- Import Blend Shapes: 打开（眨眼/口型靠它）

【模型内容】10 个网格，全部已蒙皮
  Body                  身体
  output                头（有 Blendshape: blink_L / blink_R / mouth_open）
  Hair                  头发（实心低模）
  Shirt                 高领毛衣
  low_poly_unwrapped    裤子
  mesh_node / .001      左右鞋
  Eye_L / Eye_R         眼球
  Lashes                睫毛

【FBX 内不含动画】只有骨架 + 蒙皮。动作来源建议：Mixamo 上传此 FBX 自动套动作，
或 Unity 里用 Humanoid Avatar 重定向任意 Humanoid 动画。

【贴图】（已内嵌在 FBX，同名 PNG 也在本文件夹）
  T_Skin_Base.png     M_Head         脸部（含手绘眉毛/痣/腮红），请勿用纯色替代
  T_Eye_Base.png      M_Eye          眼球（球面 UV，含瞳孔/虹膜/眼白）
  T_Body_Base.png     M_Skin         身体（AO 阴影已烘进贴图）
  T_Shirt_Base.png    M_Turtleneck   高领（AO 已烘进）
  T_Pants_Base.png    M_Pants        裤子（AO 已烘进）
  纯色材质：M_Hair(近黑) / M_Shoes(近黑) / M_Lash(睫毛，近黑，务必哑光)

【已知问题】
1. 右手手指骨骼的位置/长度本身是偏移的（AI 原骨架遗留）→ 右手手指动画会不正常，
   需要重做右手镜像才能彻底修好。左手正常。
2. 没有 AO 贴图单独提供（已烘进各基础色贴图），不要另外去找 AO 图。
