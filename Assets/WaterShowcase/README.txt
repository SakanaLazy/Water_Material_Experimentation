# Water System examples

This folder contains the eight water material presets and the project's demonstration scenes. Download reusable .unitypackage files from:
https://github.com/SakanaLazy/Water_Material_Experimentation/releases/tag/v1.0.0

Realistic, Lightly Stylized, Strongly Stylized, and Graphic each provide a plane preset and a volume preset. Complete contains all eight presets. Graphs Only contains the editable graphs and required shader support without preset materials. The downloadable packages do not include demonstration scenes.

Use plane materials on water surfaces and volume materials on separate closed meshes. In a Unity 6 URP project with Render Graph enabled, choose Water > Set Up Active URP Renderer after import. Closed water meshes use Water Volume / layer 30. If that layer is occupied, configure a dedicated layer and the renderer feature manually.

In the example scenes, press 0 for the overview and 1-4 to select a style. Right-drag to orbit and scroll to zoom. The materials themselves do not require the example camera or an input package.

Volume water is viewed from outside. This release does not include an underwater view when the camera enters the water. An underwater rendering system is planned for the next update.

Free to use and modify. Commercial use requires visible credit to LazySakana, linked to https://lazysakana.co.in. See Assets/Water/Documentation/LICENSE.txt for the full terms.
