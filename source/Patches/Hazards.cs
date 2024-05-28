using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalFixes.Patches
{
    [HarmonyPatch]
    internal static class Patches_Hazards
    {
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
    }
}