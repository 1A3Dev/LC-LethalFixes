using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalFixes.Patches
{
    [HarmonyPatch]
    internal static class Patches_Enemy
    {
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
                        Vector3 foundSpawnPositionNav = __instance.GetRandomNavMeshPositionInBoxPredictable(foundSpawnPosition, 10f, default, __instance.AnomalyRandom, __instance.GetLayermaskForEnemySizeLimit(enemyType));
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

        // [Host] Fixed enemies being able to be assigned to vents that were already occupied during the same hour
        [HarmonyPatch(typeof(RoundManager), "AssignRandomEnemyToVent")]
        [HarmonyPrefix]
        public static bool AssignRandomEnemyToVent(RoundManager __instance, ref EnemyVent vent)
        {
            if (vent.occupied)
            {
                List<EnemyVent> list = __instance.allEnemyVents.Where(x => !x.occupied).ToList();
                if (list.Count > 0)
                {
                    EnemyVent origVent = vent;
                    vent = list[__instance.AnomalyRandom.Next(list.Count)];
                    PluginLoader.logSource.LogInfo($"[AssignRandomEnemyToVent] Vent {origVent.GetInstanceID()} is already occupied, replacing with un-occupied vent: {vent.GetInstanceID()}!");
                }
                else
                {
                    PluginLoader.logSource.LogWarning("[AssignRandomEnemyToVent] All vents are occupied!");
                    return false;
                }
            }

            return true;
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

        // [Client] Fix the death sound of Baboon Hawk, Hoarder Bug & Nutcracker being set on the wrong field
        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPostfix]
        public static void EnemyAI_Start(EnemyAI __instance)
        {
            if (__instance.dieSFX == null && (__instance is BaboonBirdAI || __instance is HoarderBugAI || __instance is NutcrackerEnemyAI))
            {
                __instance.dieSFX = __instance.enemyType.deathSFX;
            }
        }

        // [Client] Fix a nullref on the RadMech missiles if the RadMech is destroyed
        [HarmonyPatch(typeof(RadMechMissile), "FixedUpdate")]
        [HarmonyPatch(typeof(RadMechMissile), "CheckCollision")]
        [HarmonyPrefix]
        public static void RadMech_MissileDestroy(RadMechMissile __instance)
        {
            if (__instance.RadMechScript == null)
            {
                Object.Destroy(__instance.gameObject);
            }
        }

        // [Host] Fix RadMech being unable to move after grabbing someone
        private static FieldInfo disableWalking = AccessTools.Field(typeof(RadMechAI), "disableWalking");
        private static FieldInfo attemptGrabTimer = AccessTools.Field(typeof(RadMechAI), "attemptGrabTimer");
        [HarmonyPatch(typeof(RadMechAI), "CancelTorchPlayerAnimation")]
        [HarmonyPostfix]
        public static void RadMech_CancelTorch(RadMechAI __instance)
        {
            if (__instance.IsServer)
            {
                disableWalking.SetValue(__instance, false);
                attemptGrabTimer.SetValue(__instance, 5f);
            }
        }

        // [Client] Fix RadMech teleporting to flight destinations on client for every flight after the first
        // [Client] Fix RadMech desyncing on clients (invisible robot bug)
        private static FieldInfo finishingFlight = AccessTools.Field(typeof(RadMechAI), "finishingFlight");
        private static FieldInfo inFlyingMode = AccessTools.Field(typeof(RadMechAI), "inFlyingMode");
        [HarmonyPatch(typeof(RadMechAI), "Update")]
        [HarmonyPrefix]
        public static void RadMech_SetFinishingFlight(RadMechAI __instance)
        {
            if (!__instance.IsServer && __instance.previousBehaviourStateIndex == 2 && __instance.currentBehaviourStateIndex != 2)
            {
                // Fix teleporting on the next flight
                if ((bool)finishingFlight.GetValue(__instance))
                {
                    finishingFlight.SetValue(__instance, false);
                    PluginLoader.logSource.LogInfo("[RadMech] Set finishingFlight to false");
                }
                // inFlyingMode is true but we're not in the flying state - desync bug happened
                if ((bool)inFlyingMode.GetValue(__instance))
                {
                    inFlyingMode.SetValue(__instance, false);
                    __instance.inSpecialAnimation = false;
                    PluginLoader.logSource.LogInfo("[RadMech] Set inFlyingMode to false");
                }
            }
        }

        // [Client] Fix Nutcracker not moving while aiming
        [HarmonyPatch(typeof(NutcrackerEnemyAI), "AimGunClientRpc")]
        [HarmonyPostfix]
        static void Nutcracker_FixClientMovement(NutcrackerEnemyAI __instance)
        {
            __instance.transform.position = __instance.serverPosition;
            __instance.inSpecialAnimation = false; //this bool disables the nutcracker position sync when true, stopping clients from seeing the aim-walk
        }

        [HarmonyPatch(typeof(NutcrackerEnemyAI), "Start")]
        [HarmonyPostfix]
        public static void Nutcracker_Start(NutcrackerEnemyAI __instance)
        {
            // Improve sync accuracy of nutcrackers hostside, will make their aim-walk less jerky in conjunction with the above fix
            __instance.updatePositionThreshold = 0.5f;
        }
    }
}