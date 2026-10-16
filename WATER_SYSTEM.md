# Water System

Unity 6.6 (`6000.6.2f1`), URP / Shader Graph 17.7. Tested in the Windows Editor with DX11.

## Open the showcase

- `Assets/WaterShowcase/WaterShowcase_Planes.unity`: four plane materials.
- `Assets/WaterShowcase/WaterShowcase_Volumes.unity`: four closed water blocks and curved sphere samples.

In Play mode, use **0** for the overview and **1–4** for individual styles. Right-drag to orbit; scroll to zoom. The ivory objects demonstrate transmission through the realistic material.

## Materials

The eight presets in `Assets/WaterShowcase` cover Realistic, Lightly Stylized, Strongly Stylized, and Graphic water for both geometries. Duplicate a preset to make a new material. The map pairs are Water 4 for Realistic, Water 1 for Lightly Stylized, Water 3 for Strongly Stylized, and Water 2 for Graphic. Both geometries use the same style assignment.

Use `SG_Water_Plane` for a surface mesh and `SG_Water_Volume` for a closed mesh. Both masters live in `Assets/Materials/Water/Shader/Masters` and share the smaller effect subgraphs and `WaterSystem.hlsl`.

Each master has six groups: Waves, Top Mask, Foam, Surface, Depth / Refraction, and Surface / Debug. Open its stage subgraphs for the individual systems. The original six foundation subgraphs retain their calculations and connections; their layout has been organized.

## Shader folders

- `Masters`: the two current plane and volume graphs.
- `Subgraphs/Core`: the original foundation graphs.
- `Subgraphs/Foam`, `Depth`, `Surface`, `Waves`: individual effects.
- `Subgraphs/Stages/Plane` and `Volume`: the six-group master stages.
- `Includes` and `Hidden`: shared HLSL and the volume depth pass.
- `References`: the original drop reference.
- `Deprecated`: previous masters and backups; references retain their GUIDs.

## Controls

| System | Controls |
| --- | --- |
| Waves | Two animated texture layers, plus swell height, length, and speed |
| Crest Foam | Height thresholds, noise, detail, and crest strength |
| Foam - Procedural | Coverage, direction, drift speed, stretch, scale, and intensity |
| Foam Texture | Bubble scale and bump strength; bump is restricted to Realistic and Lightly Stylized presets |
| Contact Foam | Width, breakup scale, speed, and intensity |
| Coastal Foam | Shallow zone width, breaker frequency, speed, and intensity |
| Depth | Shallow/deep colours, absorption density, opacity |
| Refraction | Enable and distortion strength |
| Caustics | Enable, scale, speed, strength, depth fade, atlas |
| Style | Continuous art direction from realistic to graphic |
| Debug | Beauty, four foam masks, thickness, normals, colour, transmission, caustics |

Caustics and refraction are enabled on Realistic and Lightly Stylized presets. Lightly Stylized uses a weaker caustic projection while retaining its stylized colour and lighting. Strongly Stylized and Graphic keep these effects disabled. Foam texture is shared across all four styles; bubble bump is enabled only for Realistic and Lightly Stylized. Foam can be enabled separately for each system.

Bubble detail repeats more tightly within each foam patch. The latest close-up revision doubles bump count within the same area, raising bubble scale from 525 to 742.462 (525 × √2). Bump strength stays at 0.10 for Realistic and 0.075 for Lightly Stylized; the other two styles keep bump disabled. Bubble slopes overlap smoothly rather than switching to a nearest cell, removing the triangular shading cuts between bubbles. Fine grain repetition remains shared across all four styles and is independent of this bump-only adjustment.

Foam amounts are separate from bubble tiling. Moving coastal wave bands change from 3× to 2× (two thirds of the previous setting). Wave-top crest patches and drifting surface patches both change from 3× to 1.5× (half of their previous settings). Shoreline contact foam stays unchanged. These scale mask strengths, not literal screen-space area; patch layout, drift, widths, and wave texture tiling stay independent of bubble size.

## Renderer setup

For an imported package, use **Water → Set Up Active URP Renderer**. This enables camera depth/opaque textures, names layer 30, and installs the volume feature on Universal Renderers in the active pipeline. If layer 30 is already used for another purpose, the helper stops before changing settings; configure a dedicated layer and renderer feature manually instead.

Enable the URP depth and opaque textures on the water camera. This project's showcase cameras already do so.

Closed volume renderers use layer **30 / Water Volume**. The **Water volume back-face depth** feature is installed on `Assets/Settings/PC_Renderer.asset`. It supplies the exit depth used for thickness and checks whether caustic receivers are inside the water. The back-face pass uses the same deformation as the visible material.

The showcase's opaque seabed is inset from the water walls to prevent the striped side intersections. All six water-box faces use matching boundary subdivisions. Sphere samples use `WaterCurvedSample` to reapply their per-renderer overrides after scene reloads. Shore/contact/crest effects are disabled on those samples, and the smaller swell stays consistent in Edit and Play modes.

## Caustics

`CausticsAtlas.png` contains 32 frames generated by refracting rays through a periodic height field. The shader interpolates frames and projects the result in world space onto submerged opaque receivers. It fades with depth and respects the main light's shadow map. This is an animated projection approximation rather than a runtime ray-traced solution.

## Editing and regeneration

Normal material and Shader Graph editing works directly in Unity. The files in `Automation` reproduce generated assets and check their structure:

```text
node Automation/build-water-graphs.mjs
python Automation/generate-caustics.py
python Automation/check-water-graphs.py
```

The Python atlas generator needs NumPy and Pillow. The audit needs Git and the project's existing HEAD baseline. Graph regeneration rewrites the generated masters/stage graphs and organizes the subgraphs; preserve manual graph edits before regenerating.

`WaterSystemAutomation.cs` supplies the showcase builder and a local Editor command queue in `Temp/WaterSystem`. It uses no network server. `WaterShowcasePresenter.cs` is only the inspection camera, and is not needed to reuse the shaders. `WaterCurvedSample.cs` preserves the demonstration spheres’ settings; ordinary closed meshes do not need it.

## Captures and checks

The local review pack is in `Outputs/WaterSystem/Media`. It contains eight 2560 × 1700 material renders, overview images, graph screenshots, caustic comparisons, and motion previews. The `Outputs` directory is ignored by Git, so the media remains separate from the Unity source files.

Validation reports in `Outputs/WaterSystem` cover shader compilation, feature toggles, Play mode cameras, graph references, preservation of the original subgraph calculations, and missing scene references. Public publishing, licensing, and attribution terms remain pending the owner's visual review.

## Current limits

- Refraction uses the opaque scene colour; transparent objects behind the water are not captured.
- The volume depth pass assumes a camera outside separate closed volumes. Intersecting/overlapping volumes and underwater cameras need additional rendering work.
- Swells and foam are procedural visual effects, without fluid simulation or physical breaking-wave geometry.
- Desktop Editor rendering and a Windows development build are checked in the local validation reports. Mobile, XR, and performance budgets have not been qualified.
