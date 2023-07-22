using System;
using System.Linq;

using DrakiaXYZ.BigBrain.Brains;

using EFT;

using LootingBots.Patch.Components;
using LootingBots.Patch.Util;

using UnityEngine;
using UnityEngine.AI;

namespace LootingBots.Brain.Logics
{
    internal class LootingLogic : CustomLogic
    {
        private readonly LootingBrain _lootingBrain;
        private readonly BotLog _log;
        private float _closeEnoughTimer = 0f;
        private float _moveTimer = 0f;
        private int _stuckCount = 0;
        private int _navigationAttempts = 0;
        private Vector3 _destination = Vector3.zero;

        public LootingLogic(BotOwner botOwner)
            : base(botOwner)
        {
            _log = new BotLog(LootingBots.LootLog, botOwner);
            _lootingBrain = botOwner.GetPlayer.gameObject.GetComponent<LootingBrain>();
        }

        public override void Update()
        {
            // Kick off looting logic
            if (ShouldUpdate())
            {
                TryLoot();
            }
        }

        public override void Stop()
        {
            _destination = Vector3.zero;
            _lootingBrain.DistanceToLoot = -1f;
            _stuckCount = 0;
            _navigationAttempts = 0;
            base.Stop();
        }

        // Run looting logic only when the bot is not looting and when the bot has an active item to loot
        public bool ShouldUpdate()
        {
            return !_lootingBrain.LootTaskRunning
                && _lootingBrain.HasActiveLootable
                && BotOwner.BotState == EBotState.Active;
        }

        private void TryLoot()
        {
            try
            {
                // Check if the bot is close enough to the destination to commence looting
                if (_closeEnoughTimer < Time.time)
                {
                    _closeEnoughTimer = Time.time + 2f;

                    // If the bot has not just looted something, loot the current item since we are now close enough
                    if (!_lootingBrain.LootTaskRunning && IsCloseEnough())
                    {
                        // Crouch and look to item
                        BotOwner.SetPose(0f);
                        BotOwner.Steering.LookToPoint(_lootingBrain.LootObjectPosition);
                        _lootingBrain.StartLooting();
                        return;
                    }
                }

                // Try to move the bot to the destination
                if (_moveTimer < Time.time && !_lootingBrain.LootTaskRunning)
                {
                    _moveTimer = Time.time + 4f;

                    // Initiate move to loot. Will return false if the bot is not able to navigate using a NavMesh
                    bool canMove = TryMoveToLoot();

                    // If there is not a valid path to the loot, ignore the loot forever
                    if (!canMove)
                    {
                        _lootingBrain.HandleNonNavigableLoot();
                        _stuckCount = 0;
                    }
                }
            }
            catch (Exception e)
            {
                _log.LogError(e);
            }
        }

        /*
            Check to see if the destination point and the loot object do not have a wall between them by casting a Ray between the two points.
            Walls should be on the LowPolyCollider LayerMask, so we can assume if we see one of these then we cannot properly loot
        */
        public bool HasLOS()
        {
            Vector3 rayDirection = _lootingBrain.LootObjectPosition - _destination;

            if (Physics.Raycast(_destination, rayDirection, out RaycastHit hit))
            {
                if (hit.collider.gameObject.layer == LootUtils.LowPolyMask)
                {
                    _log.LogError(
                        $"NO LOS: LowPolyCollider hit {hit.collider.gameObject.layer} {hit.collider.gameObject.name}"
                    );
                    return false;
                }
            }

            return true;
        }

        /**
        * Makes the bot look towards the target destination and begin moving towards it. Navigation will be cancelled if the bot has not moved in more than 2 navigation calls, if the destination cannot be snapped to a mesh,
        * or if the NavPathStatus is anything other than Completed
        */
        public bool TryMoveToLoot()
        {
            bool canMove = true;
            try
            {
                // Stand and move to lootable
                BotOwner.SetPose(1f);
                BotOwner.SetTargetMoveSpeed(1f);
                BotOwner.Steering.LookToMovingDirection();

                //Increment navigation attempt counter
                _navigationAttempts++;

                string lootableName =
                    _lootingBrain.ActiveContainer?.ItemOwner.Items.ToArray()[0].Name.Localized()
                    ?? _lootingBrain.ActiveItem?.Name.Localized()
                    ?? _lootingBrain.ActiveCorpse.GetPlayer?.name.Localized();

                // If the bot has not been stuck for more than 2 navigation checks, attempt to navigate to the lootable otherwise ignore the container forever
                bool isBotStuck = _stuckCount > 1;
                bool isNavigationLimit = _navigationAttempts > 30;
                if (!isBotStuck && !isNavigationLimit)
                {
                    Vector3 center = _lootingBrain.LootObjectCenter;

                    // Try to snap the desired destination point to the nearest NavMesh to ensure the bot can draw a navigable path to the point
                    Vector3 pointNearbyContainer = NavMesh.SamplePosition(
                        center,
                        out NavMeshHit navMeshAlignedPoint,
                        1f,
                        NavMesh.AllAreas
                    )
                        ? navMeshAlignedPoint.position
                        : Vector3.zero;

                    // Since SamplePosition always snaps to the closest point on the NavMesh, sometimes this point is a little too close to the loot and causes the bot to shake violently while looting.
                    // Add a small amount of padding by pushing the point away from the nearbyPoint
                    Vector3 padding = center - pointNearbyContainer;
                    padding.y = 0;
                    padding.Normalize();

                    // Make sure the point is still snapped to the NavMesh after its been pushed
                    _destination = pointNearbyContainer = NavMesh.SamplePosition(
                        center - padding,
                        out navMeshAlignedPoint,
                        1f,
                        navMeshAlignedPoint.mask
                    )
                        ? navMeshAlignedPoint.position
                        : pointNearbyContainer;

                    // Debug for bot loot navigation
                    if (LootingBots.DebugLootNavigation.Value)
                    {
                        GameObjectHelper.DrawSphere(center, 0.5f, Color.red);
                        GameObjectHelper.DrawSphere(center - padding, 0.5f, Color.green);
                        if (pointNearbyContainer != Vector3.zero)
                        {
                            GameObjectHelper.DrawSphere(pointNearbyContainer, 0.5f, Color.blue);
                        }
                    }

                    // If we were able to snap the loot position to a NavMesh, attempt to navigate
                    if (pointNearbyContainer != Vector3.zero && HasLOS())
                    {
                        NavMeshPathStatus pathStatus = BotOwner.GoToPoint(
                            pointNearbyContainer,
                            true,
                            1f,
                            false,
                            false,
                            true
                        );

                        // Log every 5 movement attempts to reduce noise
                        if (_navigationAttempts % 5 == 1)
                        {
                            _log.LogDebug(
                                $"[Attempt: {_navigationAttempts}] Moving to {lootableName} status: {pathStatus}"
                            );
                        }

                        if (pathStatus != NavMeshPathStatus.PathComplete)
                        {
                            _log.LogWarning($"No valid path to: {lootableName}. Ignoring");
                            canMove = false;
                        }
                    }
                    else
                    {
                        _log.LogWarning(
                            $"Unable to snap loot position to NavMesh. Ignoring {lootableName}"
                        );
                        canMove = false;
                    }
                }
                else
                {
                    if (isBotStuck)
                    {
                        _log.LogError($"Has been stuck trying to reach: {lootableName}. Ignoring");
                    }
                    else
                    {
                        _log.LogError(
                            $"Has exceeded the navigation limit (30) trying to reach: {lootableName}. Ignoring"
                        );
                    }
                    canMove = false;
                }
            }
            catch (Exception e)
            {
                _log.LogError(e);
            }

            return canMove;
        }

        /**
        * Check to see if the bot is close enough to the destination so that they can stop moving and start looting
        */
        private bool IsCloseEnough()
        {
            if (_destination == Vector3.zero)
            {
                return false;
            }

            // Calculate distance from bot to destination
            float dist;
            Vector3 vector = BotOwner.Position - _destination;
            float y = vector.y;
            vector.y = 0f;
            dist = vector.sqrMagnitude;

            bool isCloseEnough = dist < 0.85f && Math.Abs(y) < 0.5f;

            // Check to see if the bot is stuck
            if (!IsBotStuck(dist))
            {
                // Bot has moved, reset stuckCount and update cached distance to container
                _stuckCount = 0;
                _lootingBrain.DistanceToLoot = dist;
            }

            return isCloseEnough;
        }

        // Checks if the bot is stuck moving and increments the stuck counter.
        private bool IsBotStuck(float dist)
        {
            // Calculate change in distance and assume any change less than .25f means the bot hasnt moved.
            float changeInDist = Math.Abs(_lootingBrain.DistanceToLoot - dist);
            bool isStuck = changeInDist < 0.25f;

            if (isStuck)
            {
                _log.LogDebug(
                    $"[Stuck: {_stuckCount}] Disance moved since check: {changeInDist}. Dist from loot: {dist}"
                );

                // Bot is stuck, update stuck count
                _stuckCount++;
            }

            return isStuck;
        }
    }
}
