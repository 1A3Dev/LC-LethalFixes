using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalFixes.Patches
{
    [HarmonyPatch]
    internal static class Patches_Hazards
    {
        // [Host] Fixed spike trap entrance safety period activating when exiting the facility instead of when entering
        [HarmonyPatch(typeof(SpikeRoofTrap), "GetNearEntrance")]
        [HarmonyPrefix]
        public static bool GetNearEntrance(SpikeRoofTrap __instance, ref bool __result, ref EntranceTeleport ___nearEntrance)
        {
            bool flag = false;
            EntranceTeleport[] array = Object.FindObjectsByType<EntranceTeleport>(FindObjectsSortMode.None);
            for (int i = 0; i < array.Length; i++)
            {
                if (!array[i].isEntranceToBuilding && Vector3.Distance(__instance.spikeTrapAudio.transform.position, array[i].entrancePoint.position) < 7f)
                {
                    flag = true;
                    ___nearEntrance = array[i];
                }
            }

            bool replaced = false;
            if (flag)
            {
                for (int j = 0; j < array.Length; j++)
                {
                    if (array[j].entrancePoint == ___nearEntrance.exitPoint)
                    {
                        ___nearEntrance = array[j];
                        replaced = true;
                    }
                }
            }

            __result = replaced;

            return false;
        }

        internal static AudioClip spikeTrapActivateSound = null;
        internal static AudioClip spikeTrapDeactivateSound = null;
        [HarmonyPatch(typeof(SpikeRoofTrap), "Start")]
        [HarmonyPostfix]
        public static void Fix_SpikeTrapSafety_Start(ref SpikeRoofTrap __instance)
        {
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
        [HarmonyPatch(typeof(SpikeRoofTrap), "Update")]
        [HarmonyPrefix]
        public static void Fix_SpikeTrapSafety_Update(ref SpikeRoofTrap __instance, EntranceTeleport ___nearEntrance)
        {
            if (__instance.trapActive)
            {
                float safePeriodTime = 1.2f;

                if (___nearEntrance != null && Time.realtimeSinceStartup - ___nearEntrance.timeAtLastUse < safePeriodTime)
                {
                    __instance.timeSinceMovingUp = Time.realtimeSinceStartup;
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
        
        // [Client] Increased spike trap safety period for inverse from 0.2s to 1.2s
        [HarmonyPatch(typeof(ShipTeleporter), "SpikeTrapsReactToInverseTeleport")]
        [HarmonyPrefix]
        public static bool ShipTeleporter_SpikeTrapsReactToInverseTeleport()
        {
            foreach(SpikeRoofTrap spikeRoofTrap in Object.FindObjectsByType<SpikeRoofTrap>(FindObjectsSortMode.None))
            {
                spikeRoofTrap.timeSinceMovingUp = Time.realtimeSinceStartup;
            }

            return false;
        }
    }
}