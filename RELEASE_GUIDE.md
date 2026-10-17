# Water System v1.0.0

A reusable water material system for Unity 6 and URP, with plane and closed-volume materials in four styles: Realistic, Lightly Stylized, Strongly Stylized, and Graphic.

## Downloads

Download one of the six `.unitypackage` assets attached to this release:

| Package | Contents |
| --- | --- |
| `WaterSystem_Realistic.unitypackage` | Realistic plane and volume presets |
| `WaterSystem_LightlyStylized.unitypackage` | Lightly Stylized plane and volume presets |
| `WaterSystem_StronglyStylized.unitypackage` | Strongly Stylized plane and volume presets |
| `WaterSystem_Graphic.unitypackage` | Graphic plane and volume presets |
| `WaterSystem_GraphsOnly.unitypackage` | Both masters and all 34 subgraphs; create your own materials |
| `WaterSystem_Complete.unitypackage` | All four styles and all eight material presets |

Each package includes the required editable graphs, HLSL, textures, caustic atlas, material Inspector, volume renderer support, licence, and credit notice. Import into a Unity URP project, then choose **Water → Set Up Active URP Renderer**. Assign a plane material to a surface mesh or a volume material to a separate closed mesh on **Water Volume / layer 30**.

The packages use shared asset identities and can be imported together. `SHA256SUMS.txt` contains the package checksums.

## Compatibility

Verified on Windows with DX11 in **Unity 6.3 LTS (6000.3.12f1)** and **Unity 6.6 (6000.6.2f1)**. The LTS check imported the release package into a separate URP project and rendered all eight presets without shader errors or pink materials. The rendering path requires URP with Render Graph enabled. Built-in and HDRP are not supported. Underwater cameras and overlapping water volumes are not supported.

## Licence

Free to use, modify, combine, and redistribute, including in commercial projects, with no licence fee or royalty. **Commercial use requires visible credit to LazySakana**, linked to https://lazysakana.co.in, in the project's credits or documentation. Public credit is optional for noncommercial use. Retain the licence and copyright notice when redistributing the water system itself.

> Water system by [LazySakana](https://lazysakana.co.in).

See `LICENSE.md` for the full custom attribution terms. They are included in every package.
