using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace LethalFixes.Patches
{
    [HarmonyPatch]
    internal static class Patches_v55
    {
        // Fix hotbar breaking when grabbing an object from the shelves whilst holding one
        [HarmonyPatch(typeof(VehicleController), "Start")]
        [HarmonyPostfix]
        public static void PlaceObject(VehicleController __instance)
        {
            Transform rightShelf = __instance.transform.Find("Triggers/RightShelfPlacementCollider");
            if (rightShelf != null)
            {
                InteractTrigger interactTrigger = rightShelf.GetComponent<InteractTrigger>();
                interactTrigger.holdInteraction = true;
                interactTrigger.timeToHold = 0.35f;
            }

            Transform leftShelf = __instance.transform.Find("Triggers/LeftShelfPlacementCollider");
            if (leftShelf != null)
            {
                InteractTrigger interactTrigger = leftShelf.GetComponent<InteractTrigger>();
                interactTrigger.holdInteraction = true;
                interactTrigger.timeToHold = 0.35f;
            }

            Transform centerShelf = __instance.transform.Find("Triggers/CenterShelfPlacementCollider");
            if (centerShelf != null)
            {
                InteractTrigger interactTrigger = centerShelf.GetComponent<InteractTrigger>();
                interactTrigger.holdInteraction = true;
                interactTrigger.timeToHold = 0.35f;
            }
        }

        [HarmonyPatch(typeof(PlaceableObjectsSurface), "PlaceObject")]
        [HarmonyPrefix]
        public static bool PlaceObject(PlayerControllerB playerWhoTriggered)
        {
            return !playerWhoTriggered.isGrabbingObjectAnimation;
        }
    }
}