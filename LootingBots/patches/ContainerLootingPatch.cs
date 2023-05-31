using System;
using System.Reflection;

using Aki.Reflection.Patching;

using EFT;
using EFT.Interactive;

using LootingBots.Patch.Util;

using UnityEngine;

namespace LootingBots.Patch
{
    // Degug spheres from DrakiaXYZ Waypoints https://github.com/DrakiaXYZ/SPT-Waypoints/blob/master/Helpers/GameObjectHelper.cs
    public class GameObjectHelper
    {
        public static GameObject DrawSphere(Vector3 position, float size, Color color)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.GetComponent<Renderer>().material.color = color;
            sphere.GetComponent<Collider>().enabled = false;
            sphere.transform.position = new Vector3(position.x, position.y, position.z);
            sphere.transform.localScale = new Vector3(size, size, size);

            return sphere;
        }
    }

    public class ContainerLooting
    {
        public void Enable()
        {
            try
            {
                // new ReservePatrolContainerPatch().Enable();
                new HasNearbyContainerPatch().Enable();
                new ContainerManualUpdatePatch().Enable();
                new ContainerUpdateCheckPatch().Enable();
            }
            catch (Exception e)
            {
                LootingBots.LootLog.LogError(e.StackTrace);
            }
        }
    }

    public class ContainerUpdateCheckPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass326).GetMethod(
                "UpdateCheck",
                BindingFlags.Public | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static bool PatchPrefix(
            ref GClass326 __instance,
            ref BotOwner ___botOwner_0,
            ref GClass264 ___gclass264_0,
            ref float ___float_5,
            ref bool ___bool_1,
            ref bool ___bool_2
        )
        {
            BotLootData botContainerData = LootCache.GetLootData(___botOwner_0.Id);

            // Check if we have looted an item and the wait timer has completed
            bool boolean_0 = ___bool_1 && ___float_5 < Time.time;

            // If there is not an active container or there is a body saved, execute the original method
            if (!botContainerData?.ActiveContainer || ___gclass264_0 != null)
            {
                return true;
            }

            // If we have a container to loot, check to see if it no longer meets the criteria to loot
            if (___bool_2)
            {
                CheckContainerStatus(
                    boolean_0,
                    ref ___botOwner_0,
                    ref ___bool_2,
                    ref ___bool_1,
                    botContainerData.ActiveContainer
                );
                return false;
            }
            return true;
        }

        // Original function is GClass326.method_2
        public static void CheckContainerStatus(
            bool doneLootingTimer, // Boolean_0
            ref BotOwner botOwner, // botOwner_0
            ref bool shallLoot, // bool_2
            ref bool hasLooted, // bool_1
            LootableContainer container
        )
        {
            // If we have just looted a container, and the wait timer is finished cleanup the container from the map
            if (doneLootingTimer)
            {
                LootingBots.LootLog.LogWarning(
                    $"Removing successfully looted container: {container.name} ({container.Id})"
                );
                LootCache.Cleanup(botOwner, container.Id);
                shallLoot = false;
                hasLooted = false;
                LootCache.AddVisitedLoot(botOwner.Id, container.Id);
                return;
            }
        }
    }

    public class ContainerManualUpdatePatch : ModulePatch
    {
        public static float TimeToLoot = 8f;

        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass326).GetMethod(
                "ManualUpdate",
                BindingFlags.Public | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static bool PatchPrefix(
            ref BotOwner ___botOwner_0,
            ref GClass264 ___gclass264_0,
            ref float ___float_0,
            ref float ___float_4,
            ref float ___float_1,
            ref float ___float_5,
            ref bool ___bool_0,
            ref bool ___bool_1,
            ref bool ___bool_2
        )
        {
            BotLootData botContainerData = LootCache.GetLootData(___botOwner_0.Id);

            // If there is no active container or if there is a corpse, execute the original method
            if (!botContainerData?.ActiveContainer || ___gclass264_0 != null)
            {
                return true;
            }

            LootableContainer container = botContainerData.ActiveContainer;
            if (
                IsCloseEnough(
                    ref ___float_0,
                    ref ___float_4,
                    ref ___bool_0,
                    ref ___bool_1,
                    ref ___botOwner_0,
                    out float dist
                )
            )
            {
                // If the bot has not just looted something, loot the current container since we are now close enough
                if (!___bool_1)
                {
                    botContainerData.LootFinder.LootContainer(container);
                    ___float_5 = TimeToLoot + Time.time;
                    ___bool_1 = true;
                }

                // Crouch and look to container
                ___botOwner_0.SetPose(0f);
                ___botOwner_0.Steering.LookToPoint(container.transform.position);

                return false;
            }

            // Initiate move to container. Will return false if the bot is not able to navigate using a NavMesh
            bool canMove = botContainerData.LootFinder.TryMoveToLoot(ref ___float_1);

            // If there is not a valid path to the container, ignore the container forever
            if (!canMove)
            {
                ___bool_2 = false;
                ___bool_1 = false;
            }
            return false;
        }

        // Original function GClass326.method_1
        private static bool IsCloseEnough(
            ref float closeEnoughTimer, // float_0
            ref float containerDist, // float_4
            ref bool isCloseEnough, // bool_0
            ref bool hasLooted, // bool_1
            ref BotOwner botOwner, // botOwner_0
            out float dist
        )
        {
            BotLootData botLootData = LootCache.GetLootData(botOwner.Id);
            if (closeEnoughTimer < Time.time)
            {
                closeEnoughTimer = Time.time + 2f;
                isCloseEnough = botLootData.LootFinder.IsCloseEnough(out dist);

                // If the bot is not looting anything, check to see if the bot is stuck on a door and open it
                if (!hasLooted)
                {
                    botLootData.LootFinder.CheckIfStuck(dist);
                    return isCloseEnough;
                }
            }

            dist = containerDist;
            return isCloseEnough;
        }
    }

    public class HasNearbyContainerPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass326).GetMethod(
                "method_3",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static void PatchPrefix(
            ref GClass326 __instance,
            ref BotOwner ___botOwner_0,
            ref float ___float_2,
            ref GClass264 ___gclass264_0,
            ref bool ___bool_2
        )
        {
            BotLootData botContainerData = LootCache.GetLootData(___botOwner_0.Id);

            // Only apply container detection if there is no active corpse
            if (___float_2 < Time.time && ___gclass264_0 == null)
            {
                // If we have an active container mark ShallLoot as true
                if (botContainerData?.ActiveContainer)
                {
                    LootingBots.LootLog.LogWarning(
                        $"Bot {___botOwner_0.Id} existing container: {botContainerData.ActiveContainer.name}"
                    );
                    // Set ShallLoot to true
                    ___bool_2 = true;
                    return;
                }
            }
        }
    }
}
