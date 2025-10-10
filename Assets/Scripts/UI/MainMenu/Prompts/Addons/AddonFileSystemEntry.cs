using NSMB.Utilities.Extensions;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts.Addons {
    public class AddonFileSystemEntry : MonoBehaviour, ISelectHandler {

        //---Static Variables
        private static readonly Dictionary<AddonsSubmenu.ScannedPath.AddonType, string> SpriteNames = new() {
            { AddonsSubmenu.ScannedPath.AddonType.Folder,       "<sprite name=folder>" },
            { AddonsSubmenu.ScannedPath.AddonType.AddonFile,    "<sprite name=addon>" },
            { AddonsSubmenu.ScannedPath.AddonType.NonAddonFile, "<sprite name=unknown_file>" },
        };

        //---Properties
        public AddonsSubmenu.ScannedPath Path => scannedPath;

        //---Public Variables
        public Button button;

        //---Serialized Variables
        [SerializeField] private TMP_Text text;
        [SerializeField] private GameObject enabledText;
        [SerializeField] private SelectablePromptLabel promptLabel;

        //---Private Variables
        private AddonsSubmenu parent;
        private AddonsSubmenu.ScannedPath scannedPath;

        public void Initialize(AddonsSubmenu parent, AddonsSubmenu.ScannedPath scannedPath) {
            this.parent = parent;
            this.scannedPath = scannedPath;

            text.text = scannedPath.Name;
            if (SpriteNames.TryGetValue(scannedPath.Type, out string sprite)) {
                if (sprite == SpriteNames[AddonsSubmenu.ScannedPath.AddonType.Folder] && scannedPath.Name == "..") {
                    // Manual override for "up"
                    sprite = "<sprite name=up>";
                }
                text.text = sprite + text.text;
            }

            promptLabel.translationKey = text.text;
            UpdateEnabledState();
            gameObject.SetActive(true);
        }

        public async void OnClicked() {
            switch (scannedPath.Type) {
            case AddonsSubmenu.ScannedPath.AddonType.Folder:
                if (scannedPath.Name == "..") {
                    GlobalController.Instance.PlaySound(SoundEffect.UI_Back);
                } else {
                    GlobalController.Instance.PlaySound(SoundEffect.UI_Decide);
                }
                _ = parent.OpenFolder(scannedPath.Name);
                break;
            case AddonsSubmenu.ScannedPath.AddonType.AddonFile:
                var addonManager = GlobalController.Instance.addonManager;
                if (addonManager.IsAddonLoaded(scannedPath.Addon)) {
                    await addonManager.UnloadAddon(scannedPath.Addon.Definition.Guid);
                    GlobalController.Instance.PlaySound(SoundEffect.Player_Sound_Powerdown);
                } else {
                    var loadedAddon = await addonManager.LoadAddon(scannedPath.Addon);
                    if (loadedAddon != null) {
                        GlobalController.Instance.PlaySound(SoundEffect.Player_Sound_PowerupCollect);
                    } else {
                        GlobalController.Instance.PlaySound(SoundEffect.UI_Error);
                    }
                }
                UpdateEnabledState();
                break;
            case AddonsSubmenu.ScannedPath.AddonType.NonAddonFile:
                GlobalController.Instance.PlaySound(SoundEffect.UI_Error);
                break;
            }
        }

        public void UpdateEnabledState() {
            enabledText.SetActive(scannedPath.Addon != null && GlobalController.Instance.addonManager.IsAddonLoaded(scannedPath.Addon));
        }

        public void OnSelect(BaseEventData eventData) {
            parent.scrollRect.verticalNormalizedPosition = parent.scrollRect.ScrollToCenter((RectTransform) transform, false);
        }
    }
}