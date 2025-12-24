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
                if (spawnDenialPoints[j] && Vector3.Distance(spawnPosition, spawnDenialPoints[j].transform.position) < 16f)
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

        [HarmonyPatch(typeof(MaskedPlayerEnemy), "DoAIInterval")]
        [HarmonyPostfix]
        public static void MaskedEnemy_DoAIInterval(MaskedPlayerEnemy __instance)
        {
            if (GameNetworkManager.Instance.gameVersionNum < 64 && !__instance.isEnemyDead && __instance.currentBehaviourStateIndex == 0 && __instance.elevatorScript == null)
            {
                if (!(__instance.interestInShipCooldown >= 17f && Vector3.Distance(__instance.transform.position, StartOfRound.Instance.elevatorTransform.position) < 22f))
                {
                    if (Time.realtimeSinceStartup - __instance.timeAtLastUsingEntrance > 3f && !__instance.GetClosestPlayer(!__instance.isOutside, false, false))
                    {
                        bool flag2 = __instance.GoTowardsEntrance();
                        if (Vector3.Distance(__instance.transform.position, __instance.mainEntrancePosition) < 1f)
                        {
                            __instance.TeleportMaskedEnemyAndSync(RoundManager.FindMainEntrancePosition(true, !__instance.isOutside), !__instance.isOutside);
                        }
                        else if (flag2 && __instance.searchForPlayers.inProgress)
                        {
                            __instance.StopSearch(__instance.searchForPlayers, true);
                        }
                    }
                }
            }
        }

        public static void RadMech_FixThreatTransform(RadMechAI __instance)
        {
            if (!__instance.focusedThreatTransform && __instance.currentBehaviourStateIndex == 1 && GameNetworkManager.Instance.gameVersionNum < 64)
            {
                GameObject emptObject = GameObject.Find("RadMechTarget") ?? new GameObject("RadMechTarget");
                emptObject.transform.position = new Vector3(0f, -5000f, 0f);
                __instance.focusedThreatTransform = emptObject.transform;
                //PluginLoader.logSource.LogInfo("[RadMech] Set focusedThreatTransform to temp transform");
            }
        }

        [HarmonyPatch(typeof(RadMechAI), "MoveTowardsThreat")]
        [HarmonyPrefix]
        public static void RadMechAI_MoveTowardsThreat(RadMechAI __instance)
        {
            RadMech_FixThreatTransform(__instance);
        }

        [HarmonyPatch(typeof(RadMechAI), "Update")]
        [HarmonyPrefix]
        public static void RadMechAI_Update(RadMechAI __instance)
        {
            RadMech_FixThreatTransform(__instance);
        }
        
        // Fix blob not roaming if no players are nearby
        [HarmonyPatch(typeof(EnemyAI), "StartSearch")]
        [HarmonyPrefix]
        public static bool EnemyAI_StartSearch(EnemyAI __instance, Vector3 startOfSearch, AISearchRoutine newSearch = null)
        {
            if (__instance is BlobAI && newSearch != null && newSearch.inProgress)
            {
                return false;
            }

            return true;
        }
    }
}