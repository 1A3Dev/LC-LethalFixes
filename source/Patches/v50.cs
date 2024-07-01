using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace LethalFixes.Patches
{
    [HarmonyPatch]
    internal static class Patches_v50
    {
        // [Client] Fixed the forest giant being able to insta-kill when spawning
        [HarmonyPatch(typeof(ForestGiantAI), "OnCollideWithPlayer")]
        [HarmonyPrefix]
        public static bool Fix_GiantInstantKill(ForestGiantAI __instance, Collider other)
        {
            PlayerControllerB playerController = __instance.MeetsStandardPlayerCollisionConditions(other);
            return playerController != null;
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

            return (alreadyReplaced ? newInstructions : instructions).AsEnumerable();
        }
    }
}