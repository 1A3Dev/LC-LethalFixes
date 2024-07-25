using GameNetcodeStuff;
using HarmonyLib;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LethalFixes.Patches
{
    [HarmonyPatch]
    internal static class Patches_UI
    {
        // [Client] Show outdated warning for people still on the public beta
        [HarmonyPatch(typeof(MenuManager), "Awake")]
        [HarmonyPostfix]
        public static void MenuManager_Awake(MenuManager __instance)
        {
            try
            {
                if (Steamworks.SteamApps.CurrentBetaName == "public_beta")
                {
                    string expectedVersion = null;
                    if (Steamworks.SteamApps.BuildId <= 14953330)
                    {
                        expectedVersion = "56";
                    }

                    if (expectedVersion != null)
                    {
                        __instance.menuNotificationText.SetText($"You are on an outdated version of v{expectedVersion}. Please ensure beta participation is disabled in the preferences when right clicking the game on Steam!", true);
                        __instance.menuNotificationButtonText.SetText("[ CLOSE ]", true);
                        __instance.menuNotification.SetActive(true);
                    }
                }
            }
            catch { }
        }

        // [Client] Speaking indicator for voice activity
        [HarmonyPatch(typeof(StartOfRound), "DetectVoiceChatAmplitude")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        public static void SpeakingIndicator_VAC(StartOfRound __instance)
        {
            if (__instance.voiceChatModule != null)
            {
                Dissonance.VoicePlayerState voicePlayerState = __instance.voiceChatModule.FindPlayer(__instance.voiceChatModule.LocalPlayerName);
                HUDManager.Instance.PTTIcon.enabled = voicePlayerState.IsSpeaking && IngamePlayerSettings.Instance.settings.micEnabled && !__instance.voiceChatModule.IsMuted && (IngamePlayerSettings.Instance.settings.pushToTalk || FixesConfig.VACSpeakingIndicator.Value);
            }
        }

        // [Client] Fix LAN Above Head Usernames
        [HarmonyPatch(typeof(NetworkSceneManager), "PopulateScenePlacedObjects")]
        [HarmonyPostfix]
        public static void Fix_LANUsernameBillboard()
        {
            foreach (PlayerControllerB newPlayerScript in StartOfRound.Instance.allPlayerScripts) // Fix for billboards showing as Player # with no number in LAN (base game issue)
            {
                newPlayerScript.usernameBillboardText.text = newPlayerScript.playerUsername;
            }
        }

        // [Client] Align Menu Buttons
        [HarmonyPatch(typeof(MenuManager), "OnEnable")]
        [HarmonyPostfix]
        public static void AlignMenuButtons(MenuManager __instance, Button ___startHostButton)
        {
            if (___startHostButton != null)
            {
                float x = ___startHostButton.transform.localPosition.x;
                float z = ___startHostButton.transform.localPosition.z;
                Button[] componentsInChildren = __instance.menuButtons.GetComponentsInChildren<Button>(true);
                foreach (Button val in componentsInChildren)
                {
                    val.transform.localPosition = new Vector3(x, val.transform.localPosition.y, z);
                }
            }
        }

        // Replace button text of toggle test room & invincibility to include the state
        [HarmonyPatch(typeof(QuickMenuManager), "OpenQuickMenu")]
        [HarmonyPostfix]
        public static void DebugMenu_ButtonStateText(QuickMenuManager __instance)
        {
            TextMeshProUGUI testRoomText = __instance.debugMenuUI.transform.Find("Image/ToggleTestRoomButton/Text (TMP)").GetComponent<TextMeshProUGUI>();
            testRoomText.text = StartOfRound.Instance.testRoom != null ? "Test Room: Enabled" : "Test Room: Disabled";
            testRoomText.fontSize = 12;
            __instance.debugMenuUI.transform.Find("Image/ToggleInvincibility/Text (TMP)").GetComponent<TextMeshProUGUI>().text = !StartOfRound.Instance.allowLocalPlayerDeath ? "God Mode: Enabled" : "God Mode: Disabled";
        }
        [HarmonyPatch(typeof(StartOfRound), "Debug_EnableTestRoomClientRpc")]
        [HarmonyPostfix]
        public static void DebugMenu_ButtonStateText_TestRoom(bool enable)
        {
            QuickMenuManager quickMenuManager = Object.FindFirstObjectByType<QuickMenuManager>();
            quickMenuManager.debugMenuUI.transform.Find("Image/ToggleTestRoomButton/Text (TMP)").GetComponent<TextMeshProUGUI>().text = enable ? "Test Room: Enabled" : "Test Room: Disabled";
        }
        [HarmonyPatch(typeof(StartOfRound), "Debug_ToggleAllowDeathClientRpc")]
        [HarmonyPostfix]
        public static void DebugMenu_ButtonStateText_Invincibility(bool allowDeath)
        {
            QuickMenuManager quickMenuManager = Object.FindFirstObjectByType<QuickMenuManager>();
            quickMenuManager.debugMenuUI.transform.Find("Image/ToggleInvincibility/Text (TMP)").GetComponent<TextMeshProUGUI>().text = !allowDeath ? "God Mode: Enabled" : "God Mode: Disabled";
        }
    }
}