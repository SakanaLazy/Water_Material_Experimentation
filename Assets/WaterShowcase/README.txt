# Water System package

The six separate Unity packages are under `Outputs/WaterSystem/Release/SplitPackages`. Uploading, licensing, repository rules, and the final release name remain owner-controlled.

| Package | Contents |
| --- | --- |
| `WaterSystem_Realistic.unitypackage` | Realistic plane and volume presets |
| `WaterSystem_LightlyStylized.unitypackage` | Lightly Stylized plane and volume presets |
| `WaterSystem_StronglyStylized.unitypackage` | Strongly Stylized plane and volume presets |
| `WaterSystem_Graphic.unitypackage` | Graphic plane and volume presets |
| `WaterSystem_GraphsOnly.unitypackage` | Two masters, 34 subgraphs, and required shader support; no preset materials |
| `WaterSystem_Complete.unitypackage` | All eight water presets and both showcases |

Each package includes its project-owned dependencies, a package-specific install note, and a resolved Unity package dependency list. Shared GUIDs allow the packages to be imported together.

## Import

1. Use Unity 6000.6.2f1. Install Universal RP and Shader Graph in Package Manager first. The current export resolves both to 17.6.0; the complete dependency list is in `Assets/Water/Documentation/Packages/Dependencies.json`. Package Manager components are installation prerequisites and are not copied from Library/PackageCache into these archives.
2. Import the desired Unity package into a URP project.
3. Choose Water > Set Up Active URP Renderer. The helper installs the volume depth feature and enables depth/opaque textures. It stops safely if layer 30 conflicts with an existing layer.
4. Assign the imported plane preset to a surface mesh or the volume preset to a separate closed mesh on Water Volume / layer 30. The GraphsOnly package requires creating and configuring a material from a master graph. Both WaterShowcase scenes are included in Complete.

The showcase works with the Input System or legacy input manager. The material system itself needs neither camera presenter nor input package.

## Contents

- Two current masters and 34 organized subgraphs.
- Eight material presets: four styles for each geometry.
- Shared HLSL, back-depth shader, renderer feature, material Inspector, and setup helper.
- Both showcase scenes and demonstration meshes in Complete; required material textures and a 32-frame caustic atlas in the relevant packages.
- Setup notes and local validation reports in the ZIP.

Old masters, backup graphs, the experimental reference graph, export tools, and project-specific URP settings stay in the repository and are excluded from the reusable packages. Imported assets retain the same GUIDs as the project. Import does not change another project's render pipeline or layers; the setup menu performs those settings changes when invoked.

## Release preparation

Review the water media first. Finalize the requested credit terms and repository/license files before public upload. No license, attribution identity, release tag, or public distribution has been invented or published by the assistant.
