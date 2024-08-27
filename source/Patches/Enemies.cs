using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace LethalFixes.Patches
{
    [HarmonyPatch]
    internal static class Patches_Enemy
    {
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

        //[HarmonyPatch(typeof(RoundManager), "AssignRandomEnemyToVent")]
        //[HarmonyPostfix]
        //static void AssignRandomEnemyToVent_GroupsOf(RoundManager __instance, EnemyVent vent, int ___currentHour, bool __result)
        //{
        //    EnemyType enemy = vent.enemyType;
        //    if (__result && enemy.spawnInGroupsOf > 1)
        //    {
        //        int enemyIndex = vent.enemyTypeIndex;
        //        int time = (int)vent.spawnTime;
        //        PluginLoader.logSource.LogInfo($"Enemy \"{enemy.enemyName}\" spawned in vent, requesting group of {enemy.spawnInGroupsOf}");

        //        int spawnsLeft = enemy.spawnInGroupsOf - 1;
        //        List<EnemyVent> vents = __instance.allEnemyVents.Where(enemyVent => !enemyVent.occupied).ToList();

        //        while (spawnsLeft > 0)
        //        {
        //            if (vents.Count <= 0) return;
        //            if (enemy.PowerLevel > __instance.currentMaxInsidePower - __instance.currentEnemyPower) return;

        //            EnemyVent vent2 = vents[__instance.AnomalyRandom.Next(0, vents.Count)];

        //            __instance.currentEnemyPower += enemy.PowerLevel;
        //            vent2.enemyType = enemy;
        //            vent2.enemyTypeIndex = enemyIndex;
        //            vent2.occupied = true;
        //            vent2.spawnTime = time;
        //            if (__instance.timeScript.hour - ___currentHour <= 0)
        //            {
        //                vent2.SyncVentSpawnTimeClientRpc(time, enemyIndex);
        //            }
        //            enemy.numberSpawned++;

        //            __instance.enemySpawnTimes.Add(time);
        //            vents.Remove(vent2);

        //            PluginLoader.logSource.LogInfo($"Spawned additional \"{enemy.enemyName}\" in vents");
        //            spawnsLeft--;
        //        }

        //        if (spawnsLeft < enemy.spawnInGroupsOf - 1)
        //            __instance.enemySpawnTimes.Sort();
        //    }
        //}

        // [Client] Fix the death sound of Baboon Hawk, Hoarder Bug & Nutcracker being set on the wrong field
        [HarmonyPatch(typeof(EnemyAI), "Start")]
        [HarmonyPostfix]
        public static void EnemyAI_Start(EnemyAI __instance)
        {
            if (__instance.dieSFX == null && __instance.enemyType.deathSFX != null)
            {
                if (__instance is BaboonBirdAI || __instance is HoarderBugAI || __instance is NutcrackerEnemyAI)
                {
                    __instance.dieSFX = __instance.enemyType.deathSFX;
                }
            }
        }

        // [Client] Fix RadMech teleporting to flight destinations on client for every flight after the first
        // [Client] Fix RadMech desyncing on clients (invisible robot bug)
        [HarmonyPatch(typeof(RadMechAI), "Update")]
        [HarmonyPrefix]
        public static void RadMech_SetFinishingFlight(RadMechAI __instance, ref bool ___finishingFlight, ref bool ___inFlyingMode)
        {
            if (!__instance.IsServer && __instance.previousBehaviourStateIndex == 2 && __instance.currentBehaviourStateIndex != 2)
            {
                // Fix teleporting on the next flight
                if (___finishingFlight)
                {
                    ___finishingFlight = false;
                    //PluginLoader.logSource.LogInfo("[RadMech] Set finishingFlight to false");
                }
                // inFlyingMode is true but we're not in the flying state - desync bug happened
                if (___inFlyingMode)
                {
                    ___inFlyingMode = false;
                    __instance.inSpecialAnimation = false;
                    //PluginLoader.logSource.LogInfo("[RadMech] Set inFlyingMode to false");
                }
            }
        }

        internal static MethodInfo Internal_GoTowardsEntrance = AccessTools.Method(typeof(MaskedPlayerEnemy), "GoTowardsEntrance");
        internal static MethodInfo Internal_TeleportMaskedEnemyAndSync = AccessTools.Method(typeof(MaskedPlayerEnemy), "TeleportMaskedEnemyAndSync");
        [HarmonyPatch(typeof(MaskedPlayerEnemy), "DoAIInterval")]
        [HarmonyPostfix]
        public static void MaskedEnemy_DoAIInterval(MaskedPlayerEnemy __instance, MineshaftElevatorController ___elevatorScript, float ___interestInShipCooldown, float ___timeAtLastUsingEntrance, Vector3 ___mainEntrancePosition)
        {
            if (!__instance.isEnemyDead && __instance.currentBehaviourStateIndex == 0 && ___elevatorScript == null)
            {
                if (!(___interestInShipCooldown >= 17f && Vector3.Distance(__instance.transform.position, StartOfRound.Instance.elevatorTransform.position) < 22f))
                {
                    if (Time.realtimeSinceStartup - ___timeAtLastUsingEntrance > 3f && !__instance.GetClosestPlayer(!__instance.isOutside, false, false))
                    {
                        bool flag2 = (bool)Internal_GoTowardsEntrance?.Invoke(__instance, []);
                        if (Vector3.Distance(__instance.transform.position, ___mainEntrancePosition) < 1f)
                        {
                            Internal_TeleportMaskedEnemyAndSync?.Invoke(__instance, [RoundManager.FindMainEntrancePosition(true, !__instance.isOutside), !__instance.isOutside]);
                        }
                        else if (flag2 && __instance.searchForPlayers.inProgress)
                        {
                            __instance.StopSearch(__instance.searchForPlayers, true);
                        }
                    }
                }
            }
        }
    }
}