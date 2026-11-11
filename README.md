# audiolink outline

用于 VRChat PC 角色的 AudioLink 音乐响应轮廓。当前版本：**1.6.6**。

## 安装

从 [Releases](https://github.com/QianCJLU/audiolink-outline/releases/latest) 下载 unitypackage，导入 Unity 项目。

需要 Unity 2022.3、VRChat Avatars SDK、Modular Avatar 1.11+、NDMF 1.11+；lilToon 角色可直接使用。

将 `Assets/audiolink outline/audiolink outline.prefab` 拖到带 Avatar Descriptor 的角色根对象下，在检查器中展开目标列表勾选网格，通过 MA Menu Installer 选择菜单位置。构建时生成轮廓。

## 功能

- AudioLink / 呼吸波动、色相、亮度与轮廓宽度调节。
- 四向偏移与归中，内部同步压缩为两个轴，合计 **66 bits** 同步参数。
- 呼吸波长、频率默认 50%；100% 频率为原来的两倍。
- 亮度为 0 时使用场景光照，并继承对应 lilToon 材质的亮度限制等设置。
- 支持透明贴图与 lilToon Alpha Mask；其他具有常规主纹理属性的着色器通过通用适配支持。
- 新增轮廓和遮罩合计默认 70,000 三角面，上限 200,000。

不复刻第三方着色器的所有特殊效果；静态网格、粒子及 Android 暂不支持。

