using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Fixes1A3
{
    [BepInPlugin(modGUID, "Fixes1A3", modVersion)]
    internal class PluginLoader : BaseUnityPlugin
    {
        internal const string modGUID = "Dev1A3.Fixes1A3";
        internal const string modVersion = "1.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static bool initialized;

        public static PluginLoader Instance { get; private set; }

        internal static ManualLogSource logSource;

        private void Awake()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            Instance = this;
            logSource = Logger;

            FixesConfig.InitConfig();

            Assembly patches = Assembly.GetExecutingAssembly();
            harmony.PatchAll(patches);
        }

        public void BindConfig<T>(ref ConfigEntry<T> config, string section, string key, T defaultValue, string description = "")
        {
            config = Config.Bind<T>(section, key, defaultValue, description);
        }
    }
    internal class FixesConfig
    {
        internal static ConfigEntry<bool> ExactItemScan;
        internal static void InitConfig()
        {
            PluginLoader.Instance.BindConfig(ref ExactItemScan, "Settings", "Exact Item Scan", true, "Should the terminal scan command use the exact values?");
        }
    }

    [HarmonyPatch]
    internal static class FixesPatch
    {
        // [Host] Fix for dead enemies still opening doors
        [HarmonyPatch(typeof(DoorLock), "OnTriggerStay")]
        [HarmonyPrefix]
        public static bool Fix_DeadEnemyDoors(Collider other)
        {
            if (other.CompareTag("Enemy"))
            {
                EnemyAICollisionDetect component = other.GetComponent<EnemyAICollisionDetect>();
                if (component != null && component.mainScript.isEnemyDead)
                {
                    return false;
                }
            }
            return true;
        }

        // [Host] Fix for newly spawned items not attracting lightning
        private static FieldInfo metalObjects = AccessTools.Field(typeof(StormyWeather), "metalObjects");
        [HarmonyPatch(typeof(GrabbableObject), "Start")]
        [HarmonyPostfix]
        public static void Fix_ItemLightning_New(ref GrabbableObject __instance)
        {
            if (__instance.itemProperties.isConductiveMetal)
            {
                StormyWeather stormyWeather = Object.FindFirstObjectByType<StormyWeather>();
                if (stormyWeather != null)
                {
                    List<GrabbableObject> metalObjectsVal = (List<GrabbableObject>)metalObjects.GetValue(stormyWeather);
                    if (metalObjectsVal.Count > 0)
                    {
                        if (!metalObjectsVal.Contains(__instance))
                        {
                            metalObjectsVal.Add(__instance);
                            metalObjects.SetValue(stormyWeather, metalObjectsVal);
                        }
                    }
                }
            }
        }

        // [Host] Fix for flooded weather only working during the first day of each session
        private static FieldInfo nextTimeSync = AccessTools.Field(typeof(TimeOfDay), "nextTimeSync");
        [HarmonyPatch(typeof(StartOfRound), "ResetStats")]
        [HarmonyPostfix]
        public static void Fix_FloodedWeather()
        {
            nextTimeSync.SetValue(TimeOfDay.Instance, 0);
        }

        // [Host] Fix to make spike trap safety cooldown apply when entering instead of exiting
        internal static FieldInfo nearEntrance = AccessTools.Field(typeof(SpikeRoofTrap), "nearEntrance");
        internal static AudioClip spikeTrapActivateSound = null;
        internal static AudioClip spikeTrapDeactivateSound = null;
        [HarmonyPatch(typeof(SpikeRoofTrap), "Start")]
        [HarmonyPostfix]
        public static void Fix_SpikeTrapSafety_Start(ref SpikeRoofTrap __instance)
        {
            EntranceTeleport nearEntranceVal = (EntranceTeleport)nearEntrance.GetValue(__instance);
            if (nearEntranceVal != null)
            {
                EntranceTeleport[] array = Object.FindObjectsByType<EntranceTeleport>(FindObjectsSortMode.None);
                for (int i = 0; i < array.Length; i++)
                {
                    if (array[i].isEntranceToBuilding != nearEntranceVal.isEntranceToBuilding && array[i].entranceId == nearEntranceVal.entranceId)
                    {
                        nearEntrance.SetValue(__instance, array[i]);
                        break;
                    }
                }
            }

            //spikeTrapActivateSound = Resources.FindObjectsOfTypeAll<Landmine>()?[0]?.mineDeactivate;
            spikeTrapDeactivateSound = Resources.FindObjectsOfTypeAll<Landmine>()?[0]?.mineDeactivate;

            Light trapLight = __instance.transform.parent.Find("Spot Light").GetComponent<Light>();
            if (trapLight != null)
            {
                trapLight.intensity = 5;
            }
        }
        // [Client] Adds a sound when spike traps are activated/deactivated
        internal static FieldInfo laserLight = AccessTools.Field(typeof(SpikeRoofTrap), "laserLight");
        [HarmonyPatch(typeof(SpikeRoofTrap), "ToggleSpikesEnabledLocalClient")]
        [HarmonyPostfix]
        public static void Fix_SpikeTrapSafety_ToggleSound(SpikeRoofTrap __instance, bool enabled)
        {
            if (enabled)
            {
                if (spikeTrapActivateSound != null)
                {
                    __instance.spikeTrapAudio.PlayOneShot(spikeTrapActivateSound);
                    WalkieTalkie.TransmitOneShotAudio(__instance.spikeTrapAudio, spikeTrapActivateSound, 1f);
                }
            }
            else
            {
                if (spikeTrapDeactivateSound != null)
                {
                    __instance.spikeTrapAudio.PlayOneShot(spikeTrapDeactivateSound);
                    WalkieTalkie.TransmitOneShotAudio(__instance.spikeTrapAudio, spikeTrapDeactivateSound, 1f);
                }
            }

            Light trapLight = __instance.transform.parent.Find("Spot Light").GetComponent<Light>();
            if (trapLight != null)
            {
                trapLight.enabled = enabled;
            }
        }
        // [Client] Fix to make spike trap safety cooldown also apply to player detection traps
        internal static FieldInfo slamOnIntervals = AccessTools.Field(typeof(SpikeRoofTrap), "slamOnIntervals");
        [HarmonyPatch(typeof(SpikeRoofTrap), "Update")]
        [HarmonyPrefix]
        public static void Fix_SpikeTrapSafety_Update(ref SpikeRoofTrap __instance)
        {
            if (__instance.trapActive && !__instance.slammingDown)
            {
                bool slamOnIntervalsVal = (bool)slamOnIntervals.GetValue(__instance);
                if (!slamOnIntervalsVal)
                {
                    EntranceTeleport nearEntranceVal = (EntranceTeleport)nearEntrance.GetValue(__instance);
                    if (nearEntranceVal != null && Time.realtimeSinceStartup - nearEntranceVal.timeAtLastUse < 1.2f)
                    {
                        __instance.timeSinceMovingUp = Time.realtimeSinceStartup;
                    }
                }
            }
        }

        // [Client] Fix for start lever being slow when routing to the company building
        [HarmonyPatch(typeof(StartMatchLever), "BeginHoldingInteractOnLever")]
        [HarmonyPostfix]
        public static void Fix_LeverDeadline(ref StartMatchLever __instance)
        {
            if (TimeOfDay.Instance.daysUntilDeadline <= 0 && __instance.playersManager.inShipPhase && StartOfRound.Instance.currentLevel.planetHasTime)
            {
                __instance.triggerScript.timeToHold = 4f;
            }
            else
            {
                __instance.triggerScript.timeToHold = 0.7f;
            }
        }

        // [Host] Fix the hoarder bug not dropping the held item if it's killed before it can switch state
        internal static MethodInfo DropItemAndCallDropRPC = AccessTools.Method(typeof(HoarderBugAI), "DropItemAndCallDropRPC");
        [HarmonyPatch(typeof(HoarderBugAI), "KillEnemy")]
        [HarmonyPostfix]
        public static void Fix_HoarderDeathItem(HoarderBugAI __instance)
        {
            if (__instance.IsOwner && __instance.heldItem != null)
            {
                DropItemAndCallDropRPC?.Invoke(__instance, new object[] { __instance.heldItem.itemGrabbableObject.GetComponent<NetworkObject>(), false });
                PluginLoader.logSource.LogInfo("[Hoarder Bug] Forced Item Drop Due To Death");
            }
        }

        // [Client] Fix forest giant being able to insta-kill.
        [HarmonyPatch(typeof(ForestGiantAI), "OnCollideWithPlayer")]
        [HarmonyPrefix]
        public static bool Fix_GiantInstantKill(ForestGiantAI __instance, Collider other)
        {
            PlayerControllerB playerController = __instance.MeetsStandardPlayerCollisionConditions(other);
            return playerController != null;
        }

        // [Client] Fix randomization of terminal scan command
        [HarmonyPatch(typeof(Terminal), "TextPostProcess")]
        [HarmonyPrefix]
        public static void Fix_TerminalScan(ref string modifiedDisplayText)
        {
            if (modifiedDisplayText.Contains("[scanForItems]"))
            {
                System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed + 91);
                int num3 = 0;
                int num4 = 0;
                GrabbableObject[] array = Object.FindObjectsByType<GrabbableObject>(FindObjectsSortMode.InstanceID);
                for (int n = 0; n < array.Length; n++)
                {
                    if (array[n].itemProperties.isScrap && !array[n].isInShipRoom && !array[n].isInElevator)
                    {
                        if (FixesConfig.ExactItemScan.Value)
                        {
                            num4 += array[n].scrapValue;
                        }
                        else if (array[n].itemProperties.maxValue >= array[n].itemProperties.minValue)
                        {
                            num4 += Mathf.Clamp(random.Next(array[n].itemProperties.minValue, array[n].itemProperties.maxValue), array[n].scrapValue - 6 * num3, array[n].scrapValue + 9 * num3);
                        }
                        num3++;
                    }
                }
                if (FixesConfig.ExactItemScan.Value)
                {
                    modifiedDisplayText = modifiedDisplayText.Replace("[scanForItems]", string.Format("There are {0} objects outside the ship, totalling at an exact value of ${1}.", num3, num4));
                }
                else
                {
                    modifiedDisplayText = modifiedDisplayText.Replace("[scanForItems]", string.Format("There are {0} objects outside the ship, totalling at an approximate value of ${1}.", num3, num4));
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
        // Sort Item Dropdown Alphabetically
        internal static List<string> debugItemList = new List<string>();
        [HarmonyPatch(typeof(QuickMenuManager), "Debug_SetAllItemsDropdownOptions")]
        [HarmonyPrefix]
        public static bool Fix_DebugMenu_ItemOrder_Init(QuickMenuManager __instance)
        {
            __instance.allItemsDropdown.ClearOptions();
            debugItemList = StartOfRound.Instance.allItemsList.itemsList.Select(x => x.itemName).OrderBy(x => x).ToList();
            __instance.allItemsDropdown.AddOptions(debugItemList);
            return false;
        }
        [HarmonyPatch(typeof(QuickMenuManager), "Debug_SetItemToSpawn")]
        [HarmonyPrefix]
        public static void Fix_DebugMenu_ItemOrder_Spawn(ref int itemId)
        {
            int itemIdRaw = itemId;
            itemId = StartOfRound.Instance.allItemsList.itemsList.FindIndex(x => x.itemName == debugItemList[itemIdRaw]);
        }
        // Sort Enemy Dropdown Alphabetically
        internal static FieldInfo enemyTypeId = AccessTools.Field(typeof(QuickMenuManager), "enemyTypeId");
        internal static List<string> debugEnemyList = new List<string>();
        [HarmonyPatch(typeof(QuickMenuManager), "Debug_SetEnemyDropdownOptions")]
        [HarmonyPrefix]
        public static bool Fix_DebugMenu_EnemyOrder_Init(QuickMenuManager __instance)
        {
            if (__instance.testAllEnemiesLevel == null)
            {
                return false;
            }

            __instance.debugEnemyDropdown.ClearOptions();
            int enemyTypeIdVal = (int)enemyTypeId.GetValue(__instance);
            switch (enemyTypeIdVal)
            {
                case 0:
                    {
                        debugEnemyList = __instance.testAllEnemiesLevel.Enemies.Select(x => x.enemyType.enemyName).OrderBy(x => x).ToList();
                        break;
                    }
                case 1:
                    {
                        debugEnemyList = __instance.testAllEnemiesLevel.OutsideEnemies.Select(x => x.enemyType.enemyName).OrderBy(x => x).ToList();
                        break;
                    }
                case 2:
                    {
                        debugEnemyList = __instance.testAllEnemiesLevel.DaytimeEnemies.Select(x => x.enemyType.enemyName).OrderBy(x => x).ToList();
                        break;
                    }
            }
            __instance.debugEnemyDropdown.AddOptions(debugEnemyList);
            __instance.Debug_SetEnemyToSpawn(0);
            return false;
        }
        [HarmonyPatch(typeof(QuickMenuManager), "Debug_SetEnemyToSpawn")]
        [HarmonyPrefix]
        public static void Fix_DebugMenu_EnemyOrder_Spawn(QuickMenuManager __instance, ref int enemyId)
        {
            if (__instance.testAllEnemiesLevel == null)
            {
                return;
            }

            int enemyIdRaw = enemyId;
            int enemyTypeIdVal = (int)enemyTypeId.GetValue(__instance);
            switch (enemyTypeIdVal)
            {
                case 0:
                    {
                        enemyId = __instance.testAllEnemiesLevel.Enemies.FindIndex(x => x.enemyType.enemyName == debugEnemyList[enemyIdRaw]);
                        break;
                    }
                case 1:
                    {
                        enemyId = __instance.testAllEnemiesLevel.OutsideEnemies.FindIndex(x => x.enemyType.enemyName == debugEnemyList[enemyIdRaw]);
                        break;
                    }
                case 2:
                    {
                        enemyId = __instance.testAllEnemiesLevel.DaytimeEnemies.FindIndex(x => x.enemyType.enemyName == debugEnemyList[enemyIdRaw]);
                        break;
                    }
            }
        }
    }
}