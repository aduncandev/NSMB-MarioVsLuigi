using JimmysUnityUtilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NSMB.Addons {

    public class AddonBuildWindow : EditorWindow {

        private static readonly BuildTarget[] BuildTargets = {
            BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneWindows,
            //BuildTarget.StandaloneOSX,
            //BuildTarget.StandaloneLinux64,
            //BuildTarget.Android,
            //BuildTarget.iOS,
            //BuildTarget.WebGL
        };

        public class BuildableAddon {
            public string FolderPath;
            public AddonDefinition AddonDef;

            public string FolderName => new DirectoryInfo(FolderPath).Name;
        }

        private List<BuildableAddon> availableAddonFolders;
        private int? selectedAddonFolder;
        private Vector2 addonFolderSelectScroll;


        [MenuItem("Tools/MvLO/Addons/Build")]
        public static void BuildAddons() {
            GetWindow<AddonBuildWindow>();
        }

        public void OnEnable() {
            availableAddonFolders = new();
            foreach (var folderPath in Directory.GetDirectories("Assets/Addons")) {
                if (new DirectoryInfo(folderPath).Name.StartsWith('.')) {
                    continue;
                }

                BuildableAddon buildableAddon = new() {
                    FolderPath = folderPath
                };
                string addonDefPath = folderPath + "/addon.json";
                try {
                    string addonDefJson = File.ReadAllText(addonDefPath);
                    buildableAddon.AddonDef = JsonConvert.DeserializeObject<AddonDefinition>(addonDefJson);
                    try {
                        buildableAddon.AddonDef.IconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(buildableAddon.AddonDef.IconAssetPath);
                    } catch { }
                } catch (Exception e) {
                    Debug.LogWarning($"Failed to find/parse addon definition of addon folder {folderPath} (path: {addonDefPath})");
                    Debug.LogError(e);
                }
                availableAddonFolders.Add(buildableAddon);
            }
        }

        public void OnDisable() {
            foreach (var addon in availableAddonFolders) {
                string addonDefPath = addon.FolderPath + "/addon.json";
                File.WriteAllText(addonDefPath, JsonConvert.SerializeObject(addon.AddonDef));
            }
        }

        public void OnGUI() {
            if (availableAddonFolders.Count <= 0) {
                EditorGUILayout.LabelField("No addon folders exist!", EditorStyles.boldLabel);
                return;
            }

            EditorGUILayout.LabelField("Select a folder to build into an addon", EditorStyles.boldLabel);
            addonFolderSelectScroll = EditorGUILayout.BeginScrollView(addonFolderSelectScroll);
            int? prev = selectedAddonFolder;
            selectedAddonFolder = GUILayout.SelectionGrid(selectedAddonFolder ??= 0, availableAddonFolders.Select(ba => " " + ba.FolderName).ToArray(), 1, EditorStyles.radioButton);
            EditorGUILayout.EndScrollView();

            // Separator line
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            BuildableAddon selectedAddon = availableAddonFolders[selectedAddonFolder.Value];
            selectedAddon.AddonDef.DisplayName = EditorGUILayout.TextField("Name", selectedAddon.AddonDef.DisplayName);
            selectedAddon.AddonDef.Author = EditorGUILayout.TextField("Author", selectedAddon.AddonDef.Author);
            selectedAddon.AddonDef.Version = EditorGUILayout.TextField("Version", selectedAddon.AddonDef.Version);
            selectedAddon.AddonDef.Description = EditorGUILayout.TextField("Description", selectedAddon.AddonDef.Description);
            selectedAddon.AddonDef.IconTexture = (Texture2D) EditorGUILayout.ObjectField("Icon", selectedAddon.AddonDef.IconTexture, typeof(Texture2D), false);
            if (selectedAddon.AddonDef.IconTexture) {
                selectedAddon.AddonDef.IconAssetPath = AssetDatabase.GetAssetPath(selectedAddon.AddonDef.IconTexture);
            }

            if (GUILayout.Button("Build")) {
                // Ask if we want to overwrite.
                string finalZipPath = $"ExportedAddons/{selectedAddon.FolderName}-{selectedAddon.AddonDef.Version}{AddonManager.AddonExtension}";
                if (File.Exists(finalZipPath)) {
                    if (!EditorUtility.DisplayDialog("Addon exists", $"An Addon with the name \"{selectedAddon.FolderName}-{selectedAddon.AddonDef.Version}{AddonManager.AddonExtension}\" already exists.\nCreating an addon with the same name + version as another might be confusing.\n\nWould you like to overwrite the existing file and continue the build anyway?", "Yes", "No")) {
                        return;
                    }
                }

                // Clean old addon folder
                string exportPath = "ExportedAddons/temp";
                try {
                    Directory.Delete(exportPath, true);
                } catch { }
                Directory.CreateDirectory(exportPath);

                // Update the addon.json
                selectedAddon.AddonDef.ReleaseGuid = Guid.NewGuid();

                // Write it to the Unity asset
                File.WriteAllText(
                    selectedAddon.FolderPath + "/addon.json",
                    JsonConvert.SerializeObject(selectedAddon.AddonDef, Formatting.Indented)
                );

                // Copy it to the export folder
                File.Copy(selectedAddon.FolderPath + "/addon.json", exportPath + "/addon.json");

                // Generate list of asset bundles.
                List<AssetBundleBuild> buildMap = new();
                string[] sceneAssets =
                    AssetDatabase.FindAssets("t:Scene", new[] { selectedAddon.FolderPath })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .ToArray();
                if (sceneAssets.Length > 0) {
                    buildMap.Add(new() {
                        assetBundleName = selectedAddon.AddonDef.ReleaseGuid + "-scenes",
                        assetNames = sceneAssets,
                    });
                }

                string[] nonSceneAssets = 
                    AssetDatabase.FindAssets("", new[] { selectedAddon.FolderPath })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Except(sceneAssets)
                    .ToArray();
                if (nonSceneAssets.Length > 0) {
                    buildMap.Add(new() {
                        assetBundleName = selectedAddon.AddonDef.ReleaseGuid + "-assets",
                        assetNames = nonSceneAssets,
                    });
                }

                // Build asset bundles
                int steps = BuildTargets.Length + 1;
                int counter = 0;

                var oldBuildTarget = EditorUserBuildSettings.activeBuildTarget;
                var oldBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
                var buildMapArray = buildMap.ToArray();
                List<BuildTarget> failedBuilds = new();
                foreach (var buildTarget in BuildTargets) {
                    try {
                        EditorUtility.DisplayProgressBar("Building addon", $"Building for {buildTarget} build target...", (float) ++counter / steps);
                        string buildPath = exportPath + "/" + buildTarget;
                        Directory.CreateDirectory(buildPath);
                        BuildPipeline.BuildAssetBundles(buildPath, buildMapArray, BuildAssetBundleOptions.AppendHashToAssetBundleName | BuildAssetBundleOptions.AssetBundleStripUnityVersion, buildTarget);
                        Debug.Log($"Successfully built addon for platform {buildTarget}");

                        // Delete {buildTarget} bundle- it only contains a manifest and causes name collisions.
                        File.Delete(buildPath + "/" + buildTarget);
                    } catch (Exception e) {
                        Debug.LogError($"Failed to export addon for platform {buildTarget}");
                        Debug.LogError(e);
                        failedBuilds.Add(buildTarget);
                    }
                }
                //EditorUserBuildSettings.SwitchActiveBuildTarget(oldBuildTargetGroup, oldBuildTarget);

                // Delete manifests
                foreach (var manifest in Directory.EnumerateFiles(exportPath, "*.manifest", new EnumerationOptions { RecurseSubdirectories = true })) {
                    File.Delete(manifest);
                }

                // Add icon
                if (selectedAddon.AddonDef.IconTexture) {
                    RenderTexture outputTexture = RenderTexture.GetTemporary(selectedAddon.AddonDef.IconTexture.width, selectedAddon.AddonDef.IconTexture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
                    Graphics.Blit(selectedAddon.AddonDef.IconTexture, outputTexture);
                    Texture2D writeableIcon = outputTexture.ToTexture2D();
                    File.WriteAllBytes(exportPath + "/icon.png", writeableIcon.EncodeToPNG());
                    RenderTexture.ReleaseTemporary(outputTexture);
                    DestroyImmediate(writeableIcon);
                }
                    
                // Zip folder + clean
                EditorUtility.DisplayProgressBar("Building addon", "Compressing into .mvladdon...", (float) ++counter / steps);
                try {
                    File.Delete(finalZipPath);
                } catch { }
                ZipFile.CreateFromDirectory(exportPath, finalZipPath);
                try {
                    Directory.Delete(exportPath, true);
                } catch { }
                EditorUtility.ClearProgressBar();

                if (failedBuilds.Count > 0) {
                    Debug.LogError($"Addon build error: The following builds failed:\n* {string.Join("\n* ", failedBuilds)}\n\nThe addon was saved to {finalZipPath}");
                    EditorUtility.DisplayDialog("Build(s) Failed", $"The following builds failed:\n* {string.Join("\n* ", failedBuilds)}\n\nThe addon was saved to {finalZipPath}", "OK");
                } else {
                    Debug.Log($"Addon build successful: The addon was saved to {finalZipPath}");
                    EditorUtility.DisplayDialog("Build Successful", $"The addon was saved to {finalZipPath}", "OK");
                }

                Close();
            }
        }


        /*
        private string addonName = "New Addon", author = "Unknown", version = "v1.0", description = "A brand-new MvLO addon.";

        private Vector2 scroll;
        private List<AddressableAssetGroup> availableGroups = new();
        private int? selected;

        [MenuItem("Tools/MvLO/Addons/Build")]
        public static void BuildAddons() {
            GetWindow<AddonBuildWindow>();
        }

        public void OnEnable() {
            availableGroups = AddressableAssetSettingsDefaultObject.Settings.groups
                .Where(g => !g.IsDefaultGroup())
                .ToList();
        }

        public void OnGUI() {
            if (availableGroups.Count <= 0) {
                EditorGUILayout.LabelField("No addon definitions exist!", EditorStyles.boldLabel);
                if (GUILayout.Button("Open Addon Creation Window")) {
                    Close();
                    GetWindow<AddonCreateWindow>();
                }
                return;
            }

            EditorGUILayout.LabelField("Select addon to build", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            int? prev = selected;
            selected = GUILayout.SelectionGrid(selected ??= 0, availableGroups.Select(g => " " + g.Name).ToArray(), 1, EditorStyles.radioButton);
            EditorGUILayout.EndScrollView();

            var group = availableGroups[selected.Value];
            string addonDefPath = Path.Combine("Assets", "Addons", group.name, "addon.json");
            string addonDefJson = File.ReadAllText(addonDefPath);
            if (prev != selected) {
                try {
                    var addonDef = JsonConvert.DeserializeObject<AddonDefinition>(addonDefJson);
                    addonName = addonDef.Name;
                    author = addonDef.Author;
                    version = addonDef.Version;
                    description = addonDef.Description;
                } catch {
                    EditorGUILayout.LabelField("Failed to find addon.json for this addon...");
                    return;
                }
            }
            // Separator line
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            addonName = EditorGUILayout.TextField("Name", addonName);
            author = EditorGUILayout.TextField("Author", author);
            version = EditorGUILayout.TextField("Version", version);
            description = EditorGUILayout.TextField("Description", description);

            if (GUILayout.Button("Build")) {
                // Clean old addon folder
                string exportFolder = Path.Combine("ExportedAddons", "temp");
                try {
                    Directory.Delete(exportFolder, true);
                } catch { }
                Directory.CreateDirectory(exportFolder);

                // Update the addon.json
                File.WriteAllText(
                    addonDefPath,
                    JsonConvert.SerializeObject(new AddonDefinition {
                        Guid = Guid.NewGuid(),
                        Name = addonName,
                        Author = author,
                        Version = version,
                        Description = description
                    }, Formatting.Indented)
                );
                AssetDatabase.ImportAsset(addonDefPath);

                File.Copy(addonDefPath, Path.Combine(exportFolder, "addon.json"));

                // Build addressables
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                string loadPathId = settings.profileSettings.CreateValue(group.Name + "-LoadPath", "{MOD_PATH}");
                string buildPathId = settings.profileSettings.CreateValue(group.Name + "-BuildPath", $"ExportedAddons/temp");

                List<BuildTarget> failedBuilds = new();
                AddressableAssetSettings.CleanPlayerContent();
                foreach (var buildTarget in BuildTargets) {
                    if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(buildTarget), buildTarget)) {
                        Debug.LogError("Failed to switch build target to " + buildTarget);
                        continue;
                    }
                    settings.profileSettings.SetValue(settings.activeProfileId, group.Name + "-LoadPath", "{MOD_PATH}");
                    settings.profileSettings.SetValue(settings.activeProfileId, group.Name + "-BuildPath", $"ExportedAddons/temp/{AddonManager.GetFolderForPlatform()}");
                    AddressableAssetSettings.BuildPlayerContent();
                }

                // Zip folder + clean
                string zipPath = Path.Combine("ExportedAddons", $"{addonName}-{version}{AddonManager.AddonExtension}");
                try {
                    File.Delete(zipPath);
                } catch { }
                ZipFile.CreateFromDirectory(exportFolder, zipPath);
                try {
                    Directory.Delete(exportFolder, true);
                } catch { }

                if (failedBuilds.Count > 0) {
                    EditorUtility.DisplayDialog("Build(s) Failed", "The following builds failed:\n\n* " + string.Join("\n* ", failedBuilds), "OK");
                }

                Close();
            }
        }
    }

    public class AddonCreateWindow : EditorWindow {

        private string addonName = "New Addon", author = "Unknown", version = "v1.0", description = "A brand-new MvLO addon.";

        [MenuItem("Tools/MvLO/Addons/Create")]
        public static void CreateAddon() {
            GetWindow<AddonCreateWindow>();
        }

        public void OnGUI() {
            EditorGUILayout.LabelField("Addon information", EditorStyles.boldLabel);
            addonName = EditorGUILayout.TextField("Name", addonName);
            author = EditorGUILayout.TextField("Author", author);
            version = EditorGUILayout.TextField("Version", version);
            description = EditorGUILayout.TextField("Description", description);

            if (GUILayout.Button("Create")) {
                string id = string.Concat(addonName.Split(Path.GetInvalidFileNameChars()));

                var settings = AddressableAssetSettingsDefaultObject.Settings;
                var existingGroup = settings.FindGroup(addonName);

                if (existingGroup) {
                    if (!EditorUtility.DisplayDialog("Addon already exists!", $"An addon with the name \"{addonName}\" already exists.\nDo you want to overwrite this addon definition?\n(Assets will be preserved.)", "Yes", "Cancel")) {
                        return;
                    }
                }

                // Create folder
                string folder = Path.Combine("Assets", "Addons", addonName);
                Directory.CreateDirectory(folder);
                File.WriteAllText(
                    Path.Combine(folder, "addon.json"),
                    JsonUtility.ToJson(new AddonDefinition {
                        Name = addonName,
                        Author = author,
                        Version = version,
                        Description = description,
                    }, true));

                string loadPathId = settings.profileSettings.CreateValue(addonName + "-LoadPath", "{MOD_PATH}");
                string buildPathId = settings.profileSettings.CreateValue(addonName + "-BuildPath", $"ExportedAddons/{addonName}");
                settings.profileSettings.SetValue(settings.activeProfileId, addonName + "-LoadPath", "{MOD_PATH}");
                settings.profileSettings.SetValue(settings.activeProfileId, addonName + "-BuildPath", $"ExportedAddons/{addonName}");

                // Fix build script
                var builder = Resources.FindObjectsOfTypeAll<BuildScriptPackedMultiCatalogMode>()[0];
                if (existingGroup) {
                    settings.RemoveGroup(existingGroup);
                    foreach (var existingExternalCatalog in builder.ExternalCatalogs.ToList()) {
                        existingExternalCatalog.RemoveAssetGroupFromCatalog(existingGroup);
                        if (existingExternalCatalog.AssetGroups.Count <= 0) {
                            builder.ExternalCatalogs.Remove(existingExternalCatalog);
                            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(existingExternalCatalog));
                        }
                    }
                }
                builder.ExternalCatalogs.RemoveAll(ecs => !ecs);

                // Create asset group
                var newGroup = settings.CreateGroup(addonName, false, false, false, null, typeof(BundledAssetGroupSchema));
                var schema = newGroup.GetSchema<BundledAssetGroupSchema>();
                schema.LoadPath.SetVariableById(settings, loadPathId);
                schema.BuildPath.SetVariableById(settings, buildPathId);
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(folder), newGroup);
                EditorUtility.SetDirty(settings);

                // Add asset group to external catalog system
                string externalCatalogFolder = Path.Combine("Assets", "AddressableAssetsData", "ExtraCatalogs");
                Directory.CreateDirectory(externalCatalogFolder);

                var externalCatalog = ScriptableObject.CreateInstance<ExternalCatalogSetup>();
                externalCatalog.CatalogName = "catalog";
                externalCatalog.AddAssetGroupToCatalog(newGroup);
                externalCatalog.RuntimeLoadPath.SetVariableById(settings, loadPathId);
                externalCatalog.BuildPath.SetVariableById(settings, buildPathId);

                AssetDatabase.CreateAsset(externalCatalog, externalCatalogFolder + $"/{addonName}-catalog.asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                builder.ExternalCatalogs.Add(externalCatalog);

                EditorUtility.SetDirty(builder);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Close();
            }
        }

        */
    }
}