# Water System package

The local export is under Outputs/WaterSystem/Release. Uploading, licensing, repository rules, and the final release name remain owner-controlled.

## Import

1. Use Unity 6000.6.2f1 with Universal RP and Shader Graph 17.7.0 (the tested versions). Install those packages first.
2. Import WaterMaterial_Review.unitypackage into a URP project.
3. Choose Water > Set Up Active URP Renderer. The helper installs the volume depth feature and enables depth/opaque textures. It stops safely if layer 30 conflicts with an existing layer.
4. Open either WaterShowcase scene. Use the plane graph for a surface and the volume graph for a separate closed mesh on Water Volume / layer 30.

The showcase works with the Input System or legacy input manager. The material system itself needs neither camera presenter nor input package.

## Contents

- Two current masters and 34 organized subgraphs.
- Eight material presets: four styles for each geometry.
- Shared HLSL, back-depth shader, renderer feature, material Inspector, and setup helper.
- Both showcase scenes, demonstration meshes, material textures, and a 32-frame caustic atlas.
- Setup notes and local validation reports in the ZIP.

Old masters, backup graphs, the experimental reference graph, and project-specific URP settings stay in the repository and are excluded from the reusable package. Imported assets retain the same GUIDs as the project. The package does not change another project's render pipeline or layers until the setup menu is invoked.

## Release preparation

Review the water media first. Finalize the requested credit terms and repository/license files before public upload. No license, attribution identity, release tag, or public distribution has been invented or published by the assistant.
