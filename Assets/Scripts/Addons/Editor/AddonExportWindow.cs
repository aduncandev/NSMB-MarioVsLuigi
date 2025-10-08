using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
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
    }
}