using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalFixes
{
    [BepInPlugin(modGUID, "LethalFixes", modVersion)]
    internal class PluginLoader : BaseUnityPlugin
    {
        internal const string modGUID = "Dev1A3.LethalFixes";
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

            // Dissonance Lag Fix
            int dissonanceLogLevel = FixesConfig.LogLevelDissonance.Value;
            if (dissonanceLogLevel < 0 || dissonanceLogLevel > 4)
            {
                if (dissonanceLogLevel != -1)
                {
                    FixesConfig.LogLevelDissonance.Value = -1;
                    Config.Save();
                }
                dissonanceLogLevel = (int)Dissonance.LogLevel.Error;
            }
            Dissonance.Logs.SetLogLevel(Dissonance.LogCategory.Recording, (Dissonance.LogLevel)dissonanceLogLevel);
            Dissonance.Logs.SetLogLevel(Dissonance.LogCategory.Playback, (Dissonance.LogLevel)dissonanceLogLevel);
            Dissonance.Logs.SetLogLevel(Dissonance.LogCategory.Network, (Dissonance.LogLevel)dissonanceLogLevel);

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
        internal static ConfigEntry<float> NearActivityDistance;
        internal static ConfigEntry<bool> ExactItemScan;
        internal static ConfigEntry<bool> VACSpeakingIndicator;
        internal static ConfigEntry<bool> ModTerminalScan;
        internal static ConfigEntry<bool> SpikeTrapActivateSound;
        internal static ConfigEntry<bool> SpikeTrapDeactivateSound;
        internal static ConfigEntry<bool> SpikeTrapSafetyInverse;
        internal static ConfigEntry<int> LogLevelDissonance;
        internal static ConfigEntry<int> LogLevelNetworkManager;
        internal static void InitConfig()
        {
            AcceptableValueRange<float> AVR_NearActivityDistance = new AcceptableValueRange<float>(0f, 100f);
            NearActivityDistance = PluginLoader.Instance.Config.Bind("Settings", "Nearby Activity Distance", 7.7f, new ConfigDescription("How close should an enemy be to an entrance for it to be detected as nearby activity?", AVR_NearActivityDistance));
            PluginLoader.Instance.BindConfig(ref ExactItemScan, "Settings", "Exact Item Scan", false, "Should the terminal scan command show the exact total value?");
            PluginLoader.Instance.BindConfig(ref VACSpeakingIndicator, "Settings", "Voice Activity Icon", true, "Should the PTT speaking indicator be visible whilst using voice activation?");
            PluginLoader.Instance.BindConfig(ref ModTerminalScan, "Compatibility", "Terminal Scan Command", true, "Should the terminal scan command be modified by this mod?");
            PluginLoader.Instance.BindConfig(ref SpikeTrapActivateSound, "Spike Trap", "Sound On Enable", false, "Should spike traps make a sound when re-enabled after being disabled via the terminal?");
            PluginLoader.Instance.BindConfig(ref SpikeTrapDeactivateSound, "Spike Trap", "Sound On Disable", true, "Should spike traps make a sound when disabled via the terminal?");
            PluginLoader.Instance.BindConfig(ref SpikeTrapSafetyInverse, "Spike Trap", "Inverse Teleport Safety", false, "Should spike traps have the safe period if a player inverse teleports underneath?");

            AcceptableValueRange<int> AVR_LogLevelDissonance = new AcceptableValueRange<int>(-1, 4);
            LogLevelDissonance = PluginLoader.Instance.Config.Bind("Debug", "Log Level (Dissonance)", -1, new ConfigDescription("-1 = Mod Default, 0 = Trace, 1 = Debug, 2 = Info, 3 = Warn, 4 = Error", AVR_LogLevelDissonance));
            AcceptableValueRange<int> AVR_LogLevelNetworkManager = new AcceptableValueRange<int>(-1, 3);
            LogLevelNetworkManager = PluginLoader.Instance.Config.Bind("Debug", "Log Level (NetworkManager)", -1, new ConfigDescription("-1 = Mod Default, 0 = Developer, 1 = Normal, 2 = Error, 3 = Nothing", AVR_LogLevelNetworkManager));
        }
    }

    [HarmonyPatch]
    internal static class Patches_General
    {
        // [Client] RPC Lag Fix
        [HarmonyPatch(typeof(NetworkManager), "Awake")]
        [HarmonyPostfix]
        private static void Fix_RPCLogLevel(NetworkManager __instance)
        {
            int networkManagerLogLevel = FixesConfig.LogLevelNetworkManager.Value;
            if (networkManagerLogLevel < 0 || networkManagerLogLevel > 3)
            {
                if (networkManagerLogLevel != -1)
                {
                    FixesConfig.LogLevelNetworkManager.Value = -1;
                    PluginLoader.Instance.Config.Save();
                }
                networkManagerLogLevel = (int)Unity.Netcode.LogLevel.Normal;
            }

            __instance.LogLevel = (Unity.Netcode.LogLevel)networkManagerLogLevel;
        }

        public static List<string> removeLightShadows = new List<string>() { "FancyLamp", "LungApparatus" };
        private static FieldInfo metalObjects = AccessTools.Field(typeof(StormyWeather), "metalObjects");
        [HarmonyPatch(typeof(GrabbableObject), "Start")]
        [HarmonyPostfix]
        public static void Fix_ItemSpawn(ref GrabbableObject __instance)
        {
            // [Host] Fixed metal items spawned mid-round not attracting lightning until the next round
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

            // [Client] Fixed version of NoPropShadows
            if (removeLightShadows.Contains(__instance.itemProperties.name))
            {
                Light light = __instance.GetComponentInChildren<Light>();
                if (light != null)
                {
                    light.shadows = 0;
                }
            }
        }

        // [Host] Fixed stormy weather typically only working once each session
        [HarmonyPatch(typeof(StormyWeather), "OnDisable")]
        [HarmonyPostfix]
        public static void Fix_StormyNullRef(ref StormyWeather __instance)
        {
            ((List<GrabbableObject>)metalObjects.GetValue(__instance)).Clear();
        }

        // [Host] Fixed flooded weather only working for the first day of each session
        private static FieldInfo nextTimeSync = AccessTools.Field(typeof(TimeOfDay), "nextTimeSync");
        [HarmonyPatch(typeof(StartOfRound), "ResetStats")]
        [HarmonyPostfix]
        public static void Fix_FloodedWeather()
        {
            nextTimeSync.SetValue(TimeOfDay.Instance, 0);
        }

        // [Client] Fixed the start lever cooldown not being reset on the deadline if you initially try routing to a regular moon
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

        // [Client] Fixed the terminal scan command including items inside the ship in the calculation of the approximate value
        [HarmonyPatch(typeof(Terminal), "TextPostProcess")]
        [HarmonyPrefix]
        public static void Fix_TerminalScan(ref string modifiedDisplayText)
        {
            if (FixesConfig.ModTerminalScan.Value && modifiedDisplayText.Contains("[scanForItems]"))
            {
                System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed + 91);
                int outsideTotal = 0;
                int outsideValue = 0;
                int insideTotal = 0;
                int insideValue = 0;
                GrabbableObject[] array = Object.FindObjectsByType<GrabbableObject>(FindObjectsSortMode.InstanceID);
                for (int n = 0; n < array.Length; n++)
                {
                    if (array[n].itemProperties.isScrap)
                    {
                        if (!array[n].isInShipRoom && !array[n].isInElevator)
                        {
                            if (FixesConfig.ExactItemScan.Value)
                            {
                                outsideValue += array[n].scrapValue;
                            }
                            else if (array[n].itemProperties.maxValue >= array[n].itemProperties.minValue)
                            {
                                outsideValue += Mathf.Clamp(random.Next(array[n].itemProperties.minValue, array[n].itemProperties.maxValue), array[n].scrapValue - 6 * outsideTotal, array[n].scrapValue + 9 * outsideTotal);
                            }
                            outsideTotal++;
                        }
                        else
                        {
                            if (FixesConfig.ExactItemScan.Value)
                            {
                                insideValue += array[n].scrapValue;
                            }
                            else if (array[n].itemProperties.maxValue >= array[n].itemProperties.minValue)
                            {
                                insideValue += Mathf.Clamp(random.Next(array[n].itemProperties.minValue, array[n].itemProperties.maxValue), array[n].scrapValue - 6 * insideTotal, array[n].scrapValue + 9 * insideTotal);
                            }
                            insideTotal++;
                        }
                    }
                }
                if (FixesConfig.ExactItemScan.Value)
                {
                    modifiedDisplayText = modifiedDisplayText.Replace("[scanForItems]", string.Format("There are {0} objects outside the ship, totalling at an exact value of ${1}.", outsideTotal, outsideValue));
                }
                else
                {
                    //int randomMultiplier = 1000;
                    //outsideValue = Math.Max(0, random.Next(outsideValue - randomMultiplier, outsideValue + randomMultiplier));
                    //insideValue = Math.Max(0, random.Next(insideValue - randomMultiplier, insideValue + randomMultiplier));
                    modifiedDisplayText = modifiedDisplayText.Replace("[scanForItems]", string.Format("There are {0} objects outside the ship, totalling at an approximate value of ${1}.", outsideTotal, outsideValue));
                }
            }
        }

        // [Client] Fixed Negative Weight Speed Glitch
        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        public static void Fix_NegativeCarryWeight(PlayerControllerB __instance)
        {
            if (__instance.carryWeight < 1)
            {
                __instance.carryWeight = 1;
                PluginLoader.logSource.LogInfo("[NegativeCarryWeight] Carry Weight Changed To 1");
            }
        }

        // [Host] Notify the player that they were kicked
        [HarmonyPatch(typeof(StartOfRound), "KickPlayer")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> KickPlayer_Reason(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            bool foundClientId = false;
            bool alreadyReplaced = false;
            foreach (var instruction in instructions)
            {
                if (!alreadyReplaced)
                {
                    if (!foundClientId && instruction.opcode == OpCodes.Ldfld && instruction.operand?.ToString() == "System.UInt64 actualClientId")
                    {
                        foundClientId = true;
                        newInstructions.Add(instruction);

                        CodeInstruction kickReason = new CodeInstruction(OpCodes.Ldstr, "You have been kicked.");
                        newInstructions.Add(kickReason);

                        continue;
                    }
                    else if (foundClientId && instruction.opcode == OpCodes.Callvirt && instruction.operand?.ToString() == "Void DisconnectClient(UInt64)")
                    {
                        alreadyReplaced = true;
                        instruction.operand = AccessTools.Method(typeof(NetworkManager), "DisconnectClient", new Type[] { typeof(UInt64), typeof(string) });
                    }
                }

                newInstructions.Add(instruction);
            }

            if (!alreadyReplaced) PluginLoader.logSource.LogWarning("KickPlayer failed to add reason");

            return newInstructions.AsEnumerable();
        }

        // [Client] Fix shotgun damage
        [HarmonyPatch(typeof(ShotgunItem), "ShootGun")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Shotgun_ShootGun(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            bool alreadyReplaced = false;
            foreach (var instruction in instructions)
            {
                if (!alreadyReplaced)
                {
                    if (instruction.opcode == OpCodes.Ldfld && instruction.operand?.ToString() == "UnityEngine.RaycastHit[] enemyColliders")
                    {
                        alreadyReplaced = true;

                        Label retLabel = new Label();
                        CodeInstruction custIns1 = new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ShotgunItem), nameof(ShotgunItem.IsOwner)));
                        newInstructions.Add(custIns1);
                        CodeInstruction custIns2 = new CodeInstruction(OpCodes.Brtrue, retLabel);
                        newInstructions.Add(custIns2);
                        CodeInstruction custIns3 = new CodeInstruction(OpCodes.Ret);
                        newInstructions.Add(custIns3);
                        CodeInstruction custIns4 = new CodeInstruction(OpCodes.Ldarg_0);
                        custIns4.labels.Add(retLabel);
                        newInstructions.Add(custIns4);
                    }
                }

                newInstructions.Add(instruction);
            }

            if (!alreadyReplaced) PluginLoader.logSource.LogWarning("ShotgunItem failed to patch ShootGun");

            return newInstructions.AsEnumerable();
        }

        // [Host] Rank Fix
        [HarmonyPatch(typeof(HUDManager), "SetSavedValues")]
        [HarmonyPostfix]
        private static void HUDSetSavedValues(HUDManager __instance)
        {
            if (__instance.IsHost)
            {
                GameNetworkManager.Instance.localPlayerController.playerLevelNumber = __instance.localPlayerLevel;
            }
        }
    }
}