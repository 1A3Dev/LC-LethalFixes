using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalFixes.Patches;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalFixes
{
    [BepInPlugin(modGUID, "LethalFixes", modVersion)]
    internal class PluginLoader : BaseUnityPlugin
    {
        internal const string modGUID = "Dev1A3.LethalFixes";
        internal const string modVersion = "1.1.9";

        internal static readonly Harmony harmony = new Harmony(modGUID);

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

            harmony.PatchAll(typeof(Patches_General));
            harmony.PatchAll(typeof(Patches_Enemy));
            harmony.PatchAll(typeof(Patches_Hazards));
            harmony.PatchAll(typeof(Patches_UI));
        }

        public void BindConfig<T>(ref ConfigEntry<T> config, string section, string key, T defaultValue, string description = "")
        {
            config = Config.Bind<T>(section, key, defaultValue, description);
        }

        public static int GetCurrentGameVersion()
        {
            int currentVer = GameNetworkManager.Instance.gameVersionNum;
            if (currentVer >= 16480)
            {
                return currentVer - 16440;
            }
            else if (currentVer >= 9999)
            {
                return currentVer - 9950;
            }
            else
            {
                return currentVer;
            }
        }
    }

    internal class FixesConfig
    {
        internal static ConfigEntry<bool> ExactItemScan;
        internal static ConfigEntry<bool> PropShadows;
        internal static ConfigEntry<bool> VACSpeakingIndicator;
        internal static ConfigEntry<bool> ModTerminalScan;
        internal static ConfigEntry<bool> SpikeTrapActivateSound;
        internal static ConfigEntry<bool> SpikeTrapDeactivateSound;
        internal static ConfigEntry<bool> SpikeTrapSafetyInverse;
        internal static ConfigEntry<int> LogLevelDissonance;
        internal static ConfigEntry<int> LogLevelNetworkManager;
        internal static void InitConfig()
        {
            PluginLoader.Instance.BindConfig(ref ExactItemScan, "Settings", "Exact Item Scan", false, "Should the terminal scan command show the exact total value?");
            PluginLoader.Instance.BindConfig(ref PropShadows, "Settings", "Prop Shadows", false, "Setting this to false will disable prop shadows to improve performance.");
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

        //[HarmonyPatch(typeof(GameNetworkManager), "Start")]
        //[HarmonyPostfix]
        //private static void Start_VersionPatches()
        //{
        //    if (PluginLoader.GetCurrentGameVersion() == 50)
        //    {
        //        PluginLoader.logSource.LogInfo("Loading Version Patches: v50");
        //        PluginLoader.harmony.PatchAll(typeof(Patches_v50));
        //    }
        //}

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
            if (!FixesConfig.PropShadows.Value && removeLightShadows.Contains(__instance.itemProperties.name))
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
                                outsideValue += Mathf.Clamp((int)(random.Next(array[n].itemProperties.minValue, array[n].itemProperties.maxValue) * RoundManager.Instance.scrapValueMultiplier), array[n].scrapValue - 6 * outsideTotal, array[n].scrapValue + 9 * outsideTotal);
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
                                insideValue += Mathf.Clamp((int)(random.Next(array[n].itemProperties.minValue, array[n].itemProperties.maxValue) * RoundManager.Instance.scrapValueMultiplier), array[n].scrapValue - 6 * insideTotal, array[n].scrapValue + 9 * insideTotal);
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

            return (alreadyReplaced ? newInstructions : instructions).AsEnumerable();
        }

        // [Client] Fixed other players reloading a shotgun making the item you have in the same hotbar slot that they had their ammo in invisible
        [HarmonyPatch(typeof(PlayerControllerB), "DestroyItemInSlot")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Shotgun_ReloadItemInvis(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            bool foundActivatingItem = false;
            bool addedCall = false;
            bool alreadyReplaced = false;
            Label skipLabel = new Label();
            foreach (var instruction in instructions)
            {
                if (!foundActivatingItem && instruction.opcode == OpCodes.Stfld && instruction.operand?.ToString() == "System.Boolean activatingItem")
                {
                    foundActivatingItem = true;
                }
                else if (!addedCall && foundActivatingItem && instruction.opcode == OpCodes.Call && instruction.operand?.ToString() == "HUDManager get_Instance()")
                {
                    addedCall = true;

                    CodeInstruction custIns0 = new CodeInstruction(OpCodes.Ldarg_0);
                    custIns0.labels.AddRange(instruction.labels);
                    instruction.labels.Clear();
                    newInstructions.Add(custIns0);

                    CodeInstruction custIns1 = new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(PlayerControllerB), nameof(PlayerControllerB.IsOwner)));
                    newInstructions.Add(custIns1);
                    CodeInstruction custIns2 = new CodeInstruction(OpCodes.Brfalse, skipLabel);
                    newInstructions.Add(custIns2);
                }
                else if (!alreadyReplaced && addedCall && instruction.opcode == OpCodes.Ldarg_0)
                {
                    alreadyReplaced = true;
                    instruction.labels.Add(skipLabel);
                }

                newInstructions.Add(instruction);
            }

            if (!alreadyReplaced) PluginLoader.logSource.LogWarning("PlayerControllerB failed to patch DestroyItemInSlot");

            return (alreadyReplaced ? newInstructions : instructions).AsEnumerable();
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

        // [Client] Fix passenger being set to null on the local client
        [HarmonyPatch(typeof(VehicleController), "SetPassengerInCar")]
        [HarmonyPrefix]
        private static bool SetPassengerInCar(PlayerControllerB player)
        {
            return player != null;
        }

        // [Client] Fix steering wheel desync
        [HarmonyPatch(typeof(VehicleController), "SetCarEffects")]
        [HarmonyPrefix]
        public static void SetCarEffects(VehicleController __instance, ref float setSteering, ref float ___steeringWheelAnimFloat)
        {
            setSteering = 0f;
            ___steeringWheelAnimFloat = __instance.steeringInput / 6f;
        }

        // [Client] Fix getting stuck in the drivers seat when getting in at the same time as someone else
        public static IEnumerator CancelSpecialTriggerAnimationsAfterDelay(VehicleController __instance)
        {
            yield return new WaitForSeconds(1f);
            InteractTrigger driverSeatTrigger = __instance?.transform?.Find("Triggers/DriverSide/DriverSeatTrigger")?.GetComponent<InteractTrigger>();
            PlayerControllerB playerController = GameNetworkManager.Instance.localPlayerController;
            if (playerController.currentTriggerInAnimationWith == driverSeatTrigger)
            {
                playerController.CancelSpecialTriggerAnimations();
                PluginLoader.logSource.LogInfo("[TakeControlOfVehicle] Forced player out of drivers seat");
            }
        }
        [HarmonyPatch(typeof(VehicleController), "TakeControlOfVehicle")]
        [HarmonyPostfix]
        public static void TakeControlOfVehicle(VehicleController __instance)
        {
            if (!__instance.localPlayerInControl)
            {
                __instance.StartCoroutine(CancelSpecialTriggerAnimationsAfterDelay(__instance));
            }
        }

        [HarmonyPatch(typeof(RoundManager), "GenerateNewFloor")]
        [HarmonyPrefix]
        public static void Pre_GenerateNewFloor(RoundManager __instance)
        {
            if (__instance.currentLevel.dungeonFlowTypes == null || __instance.currentLevel.dungeonFlowTypes.Length != 0)
            {
                SoundManager.Instance.currentLevelAmbience = __instance.currentLevel.levelAmbienceClips;
            }
        }
    }
}