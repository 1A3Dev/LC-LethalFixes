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
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
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
        internal static ConfigEntry<bool> DebugMenuAlphabetical;
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
            PluginLoader.Instance.BindConfig(ref DebugMenuAlphabetical, "Settings", "Alphabetical Debug Menu", false, "Should the enemy & item list in the base game debug menu be alphabetical? This does not work properly if you have multiple items with the same name!");
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
    internal static class FixesPatch
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

        // [Host] Fixed dead enemies being able to open doors
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

        // [Host] Fixed metal items spawned mid-round not attracting lightning until the next round
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

        // [Host] Fixed flooded weather only working for the first day of each session
        private static FieldInfo nextTimeSync = AccessTools.Field(typeof(TimeOfDay), "nextTimeSync");
        [HarmonyPatch(typeof(StartOfRound), "ResetStats")]
        [HarmonyPostfix]
        public static void Fix_FloodedWeather()
        {
            nextTimeSync.SetValue(TimeOfDay.Instance, 0);
        }

        // [Client] Fixed spike trap entrance safety period not existing when inverse teleporting
        public static Dictionary<int, float> lastInverseTime = new Dictionary<int, float>();
        public static Dictionary<int, Vector3> lastInversePos = new Dictionary<int, Vector3>();
        [HarmonyPatch(typeof(ShipTeleporter), "TeleportPlayerOutWithInverseTeleporter")]
        [HarmonyPostfix]
        public static void Fix_SpikeTrapSafety_InverseTeleport(int playerObj, Vector3 teleportPos)
        {
            if (!StartOfRound.Instance.allPlayerScripts[playerObj].isPlayerDead)
            {
                if (lastInversePos.ContainsKey(playerObj))
                {
                    lastInversePos[playerObj] = teleportPos;
                }
                else
                {
                    lastInversePos.Add(playerObj, teleportPos);
                }
                if (lastInverseTime.ContainsKey(playerObj))
                {
                    lastInverseTime[playerObj] = Time.realtimeSinceStartup;
                }
                else
                {
                    lastInverseTime.Add(playerObj, Time.realtimeSinceStartup);
                }
            }
        }

        // [Host] Fixed spike trap entrance safety period activating when exiting the facility instead of when entering
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

            spikeTrapActivateSound = Resources.FindObjectsOfTypeAll<Landmine>()?[0]?.mineDeactivate;
            spikeTrapDeactivateSound = Resources.FindObjectsOfTypeAll<Landmine>()?[0]?.mineDeactivate;

            // It would be nice if it was possible to turn off the red lights instead of just the emissive
            Light trapLight = __instance.transform.parent.Find("Spot Light").GetComponent<Light>();
            if (trapLight != null)
            {
                trapLight.intensity = 5;
            }
        }

        // [Client] Fixed spike trap entrance safety period not preventing death if the trap slams at the exact same time that you enter
        // [Client] Fixed player detection spike trap having no entrance safety period
        internal static FieldInfo slamOnIntervals = AccessTools.Field(typeof(SpikeRoofTrap), "slamOnIntervals");
        [HarmonyPatch(typeof(SpikeRoofTrap), "Update")]
        [HarmonyPrefix]
        public static void Fix_SpikeTrapSafety_Update(ref SpikeRoofTrap __instance)
        {
            if (__instance.trapActive)
            {
                float safePeriodTime = 1.2f;
                float safePeriodDistance = 5f;

                EntranceTeleport nearEntranceVal = (EntranceTeleport)nearEntrance.GetValue(__instance);
                if (nearEntranceVal != null && Time.realtimeSinceStartup - nearEntranceVal.timeAtLastUse < safePeriodTime)
                {
                    __instance.timeSinceMovingUp = Time.realtimeSinceStartup;
                }
                else if (FixesConfig.SpikeTrapSafetyInverse.Value)
                {
                    bool slamOnIntervalsVal = (bool)slamOnIntervals.GetValue(__instance);
                    if (slamOnIntervalsVal)
                    {
                        foreach (KeyValuePair<int, float> keyValue in lastInverseTime)
                        {
                            if (Time.realtimeSinceStartup - keyValue.Value < safePeriodTime)
                            {
                                if (lastInversePos.ContainsKey(keyValue.Key) && Vector3.Distance(lastInversePos[keyValue.Key], __instance.laserEye.position) <= safePeriodDistance)
                                {
                                    __instance.timeSinceMovingUp = Time.realtimeSinceStartup;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Player Detection
                        int playerClientId = (int)GameNetworkManager.Instance.localPlayerController.playerClientId;
                        if (lastInverseTime.ContainsKey(playerClientId) && Time.realtimeSinceStartup - lastInverseTime[playerClientId] < safePeriodTime)
                        {
                            if (lastInversePos.ContainsKey(playerClientId) && Vector3.Distance(lastInversePos[playerClientId], __instance.laserEye.position) <= safePeriodDistance)
                            {
                                __instance.timeSinceMovingUp = Time.realtimeSinceStartup;
                            }
                        }
                    }
                }
            }
        }

        // [Client] Fixed spike traps having no indication when disabled via the terminal
        [HarmonyPatch(typeof(SpikeRoofTrap), "ToggleSpikesEnabledLocalClient")]
        [HarmonyPostfix]
        public static void Fix_SpikeTrapSafety_ToggleSound(SpikeRoofTrap __instance, bool enabled)
        {
            if (enabled)
            {
                if (FixesConfig.SpikeTrapActivateSound.Value && spikeTrapActivateSound != null)
                {
                    __instance.spikeTrapAudio.PlayOneShot(spikeTrapActivateSound);
                    WalkieTalkie.TransmitOneShotAudio(__instance.spikeTrapAudio, spikeTrapActivateSound, 1f);
                }
            }
            else
            {
                if (FixesConfig.SpikeTrapDeactivateSound.Value && spikeTrapDeactivateSound != null)
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

        // [Host] Fixed the hoarder bug not dropping the held item if it's killed too quickly
        internal static MethodInfo DropItemAndCallDropRPC = AccessTools.Method(typeof(HoarderBugAI), "DropItemAndCallDropRPC");
        [HarmonyPatch(typeof(HoarderBugAI), "KillEnemy")]
        [HarmonyPostfix]
        public static void Fix_HoarderDeathItem(HoarderBugAI __instance)
        {
            if (__instance.IsOwner && __instance.heldItem != null)
            {
                DropItemAndCallDropRPC?.Invoke(__instance, new object[] { __instance.heldItem.itemGrabbableObject.GetComponent<NetworkObject>(), false });
            }
        }

        // [Client] Fixed the forest giant being able to insta-kill when spawning
        [HarmonyPatch(typeof(ForestGiantAI), "OnCollideWithPlayer")]
        [HarmonyPrefix]
        public static bool Fix_GiantInstantKill(ForestGiantAI __instance, Collider other)
        {
            PlayerControllerB playerController = __instance.MeetsStandardPlayerCollisionConditions(other);
            return playerController != null;
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

        // [Host] Fixed outdoor enemies being able to spawn inside the outdoor objects (rocks/pumpkins etc)
        internal static Dictionary<string, int> outsideObjectWidths = new Dictionary<string, int>();
        internal static List<Transform> cachedOutsideObjects = new List<Transform>();
        public static bool ShouldDenyLocation(GameObject[] spawnDenialPoints, Vector3 spawnPosition)
        {
            bool shouldDeny = false;

            // Block Spawning In The Ship
            for (int j = 0; j < spawnDenialPoints.Length; j++)
            {
                if (Vector3.Distance(spawnPosition, spawnDenialPoints[j].transform.position) < 16f)
                {
                    shouldDeny = true;
                    break;
                }
            }

            if (!shouldDeny)
            {
                // Block Spawning In Rocks/Pumpkins etc
                foreach (Transform child in cachedOutsideObjects)
                {
                    if (child == null) continue;

                    string formattedName = child.name.Replace("(Clone)", "");
                    if (outsideObjectWidths.ContainsKey(formattedName) && Vector3.Distance(spawnPosition, child.position) <= outsideObjectWidths[formattedName])
                    {
                        shouldDeny = true;
                        break;
                    }
                }
            }

            return shouldDeny;
        }
        [HarmonyPatch(typeof(RoundManager), "SpawnMapObjects")]
        [HarmonyPostfix]
        public static void Fix_OutdoorEnemySpawn_CacheValues(RoundManager __instance)
        {
            outsideObjectWidths.Clear();
            cachedOutsideObjects.Clear();

            if (__instance.currentLevel.spawnableMapObjects.Length >= 1)
            {
                SpawnableOutsideObject[] outsideObjectsRaw = __instance.currentLevel.spawnableOutsideObjects.Select(x => x.spawnableObject).ToArray();
                foreach (SpawnableOutsideObject outsideObject in outsideObjectsRaw)
                {
                    if (outsideObject.prefabToSpawn != null && !outsideObjectWidths.ContainsKey(outsideObject.prefabToSpawn.name))
                    {
                        outsideObjectWidths.Add(outsideObject.prefabToSpawn.name, outsideObject.objectWidth);
                    }
                }

                foreach (Transform child in __instance.mapPropsContainer.transform)
                {
                    if (child != null && outsideObjectWidths.ContainsKey(child.name.Replace("(Clone)", "")))
                    {
                        cachedOutsideObjects.Add(child);
                    }
                }
            }

            PluginLoader.logSource.LogInfo($"Cached {cachedOutsideObjects.Count} Outside Map Objects");
        }
        [HarmonyPatch(typeof(RoundManager), "PositionWithDenialPointsChecked")]
        [HarmonyPrefix]
        public static bool Fix_OutdoorEnemySpawn_Denial(ref RoundManager __instance, ref Vector3 __result, Vector3 spawnPosition, GameObject[] spawnPoints, EnemyType enemyType)
        {
            if (spawnPoints.Length == 0)
            {
                return true;
            }

            if (ShouldDenyLocation(__instance.spawnDenialPoints, spawnPosition))
            {
                bool newSpawnPositionFound = false;
                List<Vector3> unusedSpawnPoints = spawnPoints.Select(x => x.transform.position).OrderBy(x => Vector3.Distance(spawnPosition, x)).ToList();
                while (!newSpawnPositionFound && unusedSpawnPoints.Count > 0)
                {
                    Vector3 foundSpawnPosition = unusedSpawnPoints[0];
                    unusedSpawnPoints.RemoveAt(0);
                    if (!ShouldDenyLocation(__instance.spawnDenialPoints, foundSpawnPosition))
                    {
                        Vector3 foundSpawnPositionNav = __instance.GetRandomNavMeshPositionInBoxPredictable(foundSpawnPosition, 10f, default(NavMeshHit), __instance.AnomalyRandom, __instance.GetLayermaskForEnemySizeLimit(enemyType));
                        if (!ShouldDenyLocation(__instance.spawnDenialPoints, foundSpawnPositionNav))
                        {
                            newSpawnPositionFound = true;
                            __result = foundSpawnPositionNav;
                            //PluginLoader.logSource.LogInfo($"[PositionWithDenialPointsChecked] Spawn Position Modified");
                            break;
                        }
                    }
                }

                if (newSpawnPositionFound)
                {
                    //PluginLoader.logSource.LogInfo($"[PositionWithDenialPointsChecked] Spawn Position Changed: {spawnPosition} > {__result}");
                }
                else
                {
                    __result = spawnPosition;
                    //PluginLoader.logSource.LogInfo($"[PositionWithDenialPointsChecked] Spawn Position Fallback: {spawnPosition} > {__result}");
                }
            }
            else
            {
                __result = spawnPosition;
                //PluginLoader.logSource.LogInfo($"[PositionWithDenialPointsChecked] Spawn Position Unchanged: {spawnPosition} > {__result}");
            }
            return false;
        }

        // [Client] Fixed entrance nearby activity including dead enemies
        internal static FieldInfo entranceExitPoint = AccessTools.Field(typeof(EntranceTeleport), "exitPoint");
        internal static FieldInfo entranceTriggerScript = AccessTools.Field(typeof(EntranceTeleport), "triggerScript");
        internal static FieldInfo checkForEnemiesInterval = AccessTools.Field(typeof(EntranceTeleport), "checkForEnemiesInterval");
        [HarmonyPatch(typeof(EntranceTeleport), "Update")]
        [HarmonyPrefix]
        public static bool Fix_NearActivityDead(EntranceTeleport __instance)
        {
            InteractTrigger triggerScriptVal = (InteractTrigger)entranceTriggerScript.GetValue(__instance);
            float checkForEnemiesIntervalVal = (float)checkForEnemiesInterval.GetValue(__instance);
            if (__instance.isEntranceToBuilding && triggerScriptVal != null && checkForEnemiesIntervalVal <= 0f)
            {
                Transform exitPointVal = (Transform)entranceExitPoint.GetValue(__instance);
                if (!exitPointVal)
                {
                    if (__instance.FindExitPoint())
                    {
                        exitPointVal = (Transform)entranceExitPoint.GetValue(__instance);
                    }
                }

                if (exitPointVal != null)
                {
                    checkForEnemiesInterval.SetValue(__instance, 1f);
                    bool flag = false;
                    for (int i = 0; i < RoundManager.Instance.SpawnedEnemies.Count; i++)
                    {
                        EnemyAI enemyAI = RoundManager.Instance.SpawnedEnemies[i];
                        if (enemyAI != null && !enemyAI.isEnemyDead && !enemyAI.isOutside && Vector3.Distance(enemyAI.transform.position, exitPointVal.transform.position) < FixesConfig.NearActivityDistance.Value)
                        {
                            flag = true;
                            break;
                        }
                    }

                    string newTip = flag ? "[Near activity detected!]" : "Enter: [LMB]";
                    if (triggerScriptVal.hoverTip != newTip)
                    {
                        triggerScriptVal.hoverTip = newTip;
                    }

                    return false;
                }
            }
            return true;
        }

        // [Client] Fixed Negative Weight Speed Glitch
        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        public static void Fix_NegativeCarryWeight(PlayerControllerB __instance)
        {
            if (__instance.carryWeight < 1)
            {
                __instance.carryWeight = 1;
                PluginLoader.logSource.LogInfo("Fixing Negative Carry Weight");
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
            bool foundAddLoc = false;
            bool alreadyReplaced = false;
            Label retLabel = new Label();
            foreach (var instruction in instructions)
            {
                if (!alreadyReplaced)
                {
                    if (!foundAddLoc && instruction.opcode == OpCodes.Callvirt && instruction.operand?.ToString() == "Void Play()")
                    {
                        foundAddLoc = true;
                        newInstructions.Add(instruction);

                        CodeInstruction ldArg0 = new CodeInstruction(OpCodes.Ldarg_0);
                        newInstructions.Add(ldArg0);

                        CodeInstruction isOwner = new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ShotgunItem), nameof(ShotgunItem.IsOwner)));
                        newInstructions.Add(isOwner);

                        CodeInstruction brCode = new CodeInstruction(OpCodes.Brtrue, retLabel);
                        newInstructions.Add(brCode);

                        CodeInstruction retCode = new CodeInstruction(OpCodes.Ret);
                        newInstructions.Add(retCode);

                        continue;
                    }
                    else if (foundAddLoc)
                    {
                        alreadyReplaced = true;
                        instruction.labels.Add(retLabel);
                    }
                }

                newInstructions.Add(instruction);
            }

            if (!alreadyReplaced) PluginLoader.logSource.LogWarning("ShotgunItem failed to patch ShootGun");

            return newInstructions.AsEnumerable();
        }

        // Fix the death sound of Baboon Hawk, Hoarder Bug & Nutcracker being set on the wrong field
        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPostfix]
        public static void EnemyAI_Start(EnemyAI __instance)
        {
            if (__instance.dieSFX == null && (__instance is BaboonBirdAI || __instance is HoarderBugAI || __instance is NutcrackerEnemyAI))
            {
                __instance.dieSFX = __instance.enemyType.deathSFX;
            }
        }

        // Fix a nullref on the RadMech missiles if the RadMech is destroyed
        [HarmonyPatch(typeof(RadMechMissile), "FixedUpdate")]
        [HarmonyPatch(typeof(RadMechMissile), "CheckCollision")]
        [HarmonyPrefix]
        public static void RadMechMissile_Destroy(RadMechMissile __instance)
        {
            if (__instance.RadMechScript == null)
            {
                Object.Destroy(__instance.gameObject);
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
            if (!FixesConfig.DebugMenuAlphabetical.Value)
            {
                return true;
            }

            __instance.allItemsDropdown.ClearOptions();
            debugItemList = StartOfRound.Instance.allItemsList.itemsList.Select(x => x.itemName).OrderBy(x => x).ToList();
            __instance.allItemsDropdown.AddOptions(debugItemList);
            return false;
        }
        [HarmonyPatch(typeof(QuickMenuManager), "Debug_SetItemToSpawn")]
        [HarmonyPrefix]
        public static void Fix_DebugMenu_ItemOrder_Spawn(ref int itemId)
        {
            if (FixesConfig.DebugMenuAlphabetical.Value)
            {
                int itemIdRaw = itemId;
                itemId = StartOfRound.Instance.allItemsList.itemsList.FindIndex(x => x.itemName == debugItemList[itemIdRaw]);
            }
        }
        // Sort Enemy Dropdown Alphabetically
        internal static FieldInfo enemyTypeId = AccessTools.Field(typeof(QuickMenuManager), "enemyTypeId");
        internal static List<string> debugEnemyList = new List<string>();
        [HarmonyPatch(typeof(QuickMenuManager), "Debug_SetEnemyDropdownOptions")]
        [HarmonyPrefix]
        public static bool Fix_DebugMenu_EnemyOrder_Init(QuickMenuManager __instance)
        {
            __instance.debugEnemyDropdown.ClearOptions();

            if (__instance.testAllEnemiesLevel == null)
            {
                return false;
            }
            if (!FixesConfig.DebugMenuAlphabetical.Value)
            {
                return true;
            }

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
            if (__instance.testAllEnemiesLevel == null || !FixesConfig.DebugMenuAlphabetical.Value)
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