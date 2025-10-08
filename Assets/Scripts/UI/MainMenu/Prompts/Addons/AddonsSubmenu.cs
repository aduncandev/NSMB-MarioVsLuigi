using NSMB.Addons;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts.Addons {
    public class AddonsSubmenu : PromptSubmenu {

        //---Static Variables
        private static readonly ScannedPath ParentFolder = new() {
            Name = "..",
            Type = ScannedPath.AddonType.Folder,
        };

        //---Serialized Variables
        [SerializeField] private GameObject loadingGraphic;
        [SerializeField] public ScrollRect scrollRect;
        [SerializeField] private AddonFileSystemEntry template;
        [SerializeField] private TMP_Text folderLabel;

        //---Private Variables
        private List<AddonFileSystemEntry> entries = new();
        private string currentPath = "", currentRelativePath = "";

        public override void Initialize() {
            base.Initialize();
        }

        public override void Show(bool first) {
            base.Show(first);
            _ = OpenFolder(".");
        }

        public async Awaitable OpenFolder(string newPath) {
            await Awaitable.MainThreadAsync();
            loadingGraphic.SetActive(true);
            string fullNewPath = new DirectoryInfo(Path.Combine(AddonManager.LocalFolderPath, currentRelativePath, newPath)).FullName;
            string previousPath = currentPath;
            currentPath = fullNewPath;
            currentRelativePath = folderLabel.text = Path.GetRelativePath(AddonManager.LocalFolderPath, fullNewPath).Replace(@"\", "/");

            // Prepare paths in background thread
            await Awaitable.BackgroundThreadAsync();
            List<ScannedPath> results = new();

            foreach (string subdirectoryPath in Directory.EnumerateDirectories(fullNewPath)) {
                string subdirectoryName = Path.GetFileName(subdirectoryPath);
                results.Add(new ScannedPath {
                    Type = ScannedPath.AddonType.Folder,
                    FullPath = subdirectoryPath,
                    Name = subdirectoryName,
                });
            }
            var addonManager = GlobalController.Instance.addonManager;
            foreach (string filePath in Directory.EnumerateFiles(fullNewPath)) {
                var addon = addonManager.FindAddon(filePath);
                string fileName = Path.GetFileName(filePath);

                if (addon != null) {
                    // This is an addon.
                    results.Add(new ScannedPath {
                        Type = ScannedPath.AddonType.AddonFile,
                        Addon = addon,
                        FullPath = filePath,
                        Name = fileName,
                    });
                } else {
                    results.Add(new ScannedPath {
                        Type = ScannedPath.AddonType.NonAddonFile,
                        FullPath = filePath,
                        Name = fileName,
                    });
                }
            }
            results.Sort();

            // Create gameobjects in main thread
            await Awaitable.MainThreadAsync();
            foreach (var entry in entries) {
                Destroy(entry.gameObject);
            }
            entries.Clear();

            bool isRoot = currentRelativePath == ".";
            if (!isRoot) {
                // Create "up" entry.
                var newEntry = Instantiate(template, template.transform.parent);
                newEntry.Initialize(this, ParentFolder);
                entries.Add(newEntry);
            }

            foreach (var result in results) {
                var newEntry = Instantiate(template, template.transform.parent);
                newEntry.Initialize(this, result);
                entries.Add(newEntry);
            }

            for (int i = 0; i < entries.Count; i++) {
                Navigation nav = new() { mode = Navigation.Mode.Explicit };
                if (i - 1 >= 0) {
                    // Previous
                    nav.selectOnUp = entries[i - 1].button;
                }
                if (i + 1 < entries.Count) {
                    //Next
                    nav.selectOnDown = entries[i + 1].button;
                } else {
                    Selectable backButtonSelectable = BackButton.GetComponentInChildren<Selectable>();
                    nav.selectOnDown = backButtonSelectable;

                    var backButtonNav = backButtonSelectable.navigation;
                    backButtonNav.selectOnUp = entries[i].button;
                    backButtonSelectable.navigation = backButtonNav;
                }
                entries[i].button.navigation = nav;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) scrollRect.transform);
            UnityEngine.Canvas.ForceUpdateCanvases();

            bool selected = false;
            foreach (var entry in entries) {
                if (entry.Path.FullPath == previousPath) {
                    Canvas.EventSystem.SetSelectedGameObject(entry.button.gameObject);
                    selected = true;
                    break;
                }
            }
            if (!selected) {
                Canvas.EventSystem.SetSelectedGameObject(entries[0].button.gameObject);
            }
            loadingGraphic.SetActive(false);
        }

        public class ScannedPath : IComparable<ScannedPath> {
            public AddonType Type;
            public Addon Addon;
            public string FullPath;
            public string Name;
            public bool IsFolder => Type == AddonType.Folder;

            public int CompareTo(ScannedPath other) {
                if (!IsFolder && other.IsFolder) {
                    return 1;
                } else if (IsFolder && !other.IsFolder) {
                    return -1;
                } else {
                    return string.Compare(Name, other.Name, true);
                }
            }

            public enum AddonType {
                AddonFile,
                Folder,
                NonAddonFile,
            }
        }
    }
}