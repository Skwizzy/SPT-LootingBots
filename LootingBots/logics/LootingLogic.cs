using System;
using System.Linq;
using System.Threading.Tasks;

using DrakiaXYZ.BigBrain.Brains;

using EFT;
using EFT.Interactive;

using LootingBots.Patch.Components;
using LootingBots.Patch.Util;

using UnityEngine;
using UnityEngine.AI;

namespace LootingBots.Brain.Logics
{
    internal class LootingLogic : CustomLogic
    {
        private readonly LootFinder _lootFinder;
        private readonly Log _log;
        private float _closeEnoughTimer = 0f;
        private bool _isLooting = false;
        private float _moveTimer = 0f;
        private int _stuckCount = 0;
        private int _navigationAttempts = 0;
        private Vector3 _destination;
        private float _distanceToDestination = 0f;

        public LootingLogic(BotOwner botOwner)
            : base(botOwner)
        {
            _log = LootingBots.LootLog;
            _lootFinder = botOwner.GetPlayer.gameObject.GetComponent<LootFinder>();
        }

        public override void Update()
        {
            // Kick off looting logic
            if (ShouldUpdate())
            {
                TryLoot();
            }
        }

        // Run looting logic only when the bot is not looting and when the bot has an active item to loot
        public bool ShouldUpdate()
        {
            return !_isLooting && _lootFinder.HasActiveLootable();
        }

        public override void Start()
        {
            _distanceToDestination = 0;
            _stuckCount = 0;
            _navigationAttempts = 0;
            base.Start();
        }

        public override void Stop()
        {
            StopLooting();
            base.Stop();
        }

        private void StopLooting()
        {
            _isLooting = false;
            _lootFinder.Cleanup();
        }

        private async void TryLoot()
        {
            try
            {
                // Check if the bot is close enough to the destination to commence looting
                if (_closeEnoughTimer < Time.time)
                {
                    _closeEnoughTimer = Time.time + 2f;
                    bool isCloseEnough = IsCloseEnough();

                    // If the bot has not just looted something, loot the current item since we are now close enough
                    if (!_isLooting && isCloseEnough)
                    {
                        await ExecuteLooting();
                        return;
                    }
                }

                // Try to move the bot to the destination
                if (_moveTimer < Time.time && !_isLooting)
                {
                    _moveTimer = Time.time + 4f;

                    // Initiate move to loot. Will return false if the bot is not able to navigate using a NavMesh
                    bool canMove = TryMoveToLoot();

                    // If there is not a valid path to the loot, ignore the loot forever
                    if (!canMove)
                    {
                        _lootFinder.HandleNonNavigableLoot();
                        _stuckCount = 0;
                    }
                }
            }
            catch (Exception e)
            {
                _log.LogError($"Bot {BotOwner.Id}: {e}");
            }
        }

        private async Task ExecuteLooting()
        {
            LootItem item = _lootFinder.ActiveItem;
            LootableContainer container = _lootFinder.ActiveContainer;
            BotOwner corpse = _lootFinder.ActiveCorpse;

            bool isLootingContainer = container != null;
            bool isLootingCorpse = corpse != null;
            Vector3 lootPosition =
                container?.transform?.position
                ?? corpse?.GetPlayer?.Transform?.position
                ?? item.transform.position;
            // Crouch and look to item
            BotOwner.SetPose(0f);
            BotOwner.Steering.LookToPoint(lootPosition);

            _isLooting = true;
            if (isLootingContainer)
            {
                await _lootFinder.LootContainer(container);
            }
            else if (isLootingCorpse)
            {
                await _lootFinder.LootCorpse();
            }
            else
            {
                await _lootFinder.LootItem();
            }

            // Once task has completed, reset the looting state and the lootFinder
            StopLooting();
        }

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
                    _lootFinder.ActiveContainer?.ItemOwner.Items.ToArray()[0].Name.Localized()
                    ?? _lootFinder.ActiveItem?.Name.Localized()
                    ?? _lootFinder.ActiveCorpse.GetPlayer.name.Localized();

                // If the bot has not been stuck for more than 2 navigation checks, attempt to navigate to the lootable otherwise ignore the container forever
                bool isBotStuck = _stuckCount > 1;
                bool isNavigationLimit = _navigationAttempts > 30;
                if (!isBotStuck && !isNavigationLimit)
                {
                    Vector3 center = _lootFinder.LootObjectCenter;

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
                    if (pointNearbyContainer != Vector3.zero)
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
                                $"(Attempt: {_navigationAttempts}) Bot {BotOwner.Id} moving to {lootableName} status: {pathStatus}"
                            );
                        }

                        if (pathStatus != NavMeshPathStatus.PathComplete)
                        {
                            _log.LogWarning(
                                $"Bot {BotOwner.Id} has no valid path to: {lootableName}. Ignoring"
                            );
                            canMove = false;
                        }
                    }
                    else
                    {
                        _log.LogWarning(
                            $"Bot {BotOwner.Id} unable to snap loot position to NavMesh. Ignoring {lootableName}"
                        );
                        canMove = false;
                    }
                }
                else
                {
                    if (isBotStuck)
                    {
                        _log.LogError(
                            $"Bot {BotOwner.Id} Has been stuck trying to reach: {lootableName}. Ignoring"
                        );
                    }
                    else
                    {
                        _log.LogError(
                            $"Bot {BotOwner.Id} Has exceeded the navigation limit (30) trying to reach: {lootableName}. Ignoring"
                        );
                    }
                    canMove = false;
                }
            }
            catch (Exception e)
            {
                _log.LogError($"Bot {BotOwner.Id}: {e}");
                _log.LogError(e.Message);
                _log.LogError(e.StackTrace);
            }

            return canMove;
        }

        private bool IsCloseEnough()
        {
            // Calculate distance from bot to destination
            float dist;
            Vector3 vector = BotOwner.Position - _destination;
            float y = vector.y;
            vector.y = 0f;
            dist = vector.sqrMagnitude;

            bool isCloseEnough = dist < 0.85f && Math.Abs(y) < 0.5f;

            // If the bot is not looting anything, check to see if the bot is stuck
            if (!_isLooting && !IsBotStuck(dist))
            {
                // Bot has moved, reset stuckCount and update cached distance to container
                _distanceToDestination = dist;
            }

            return isCloseEnough;
        }

        // Checks if the bot is stuck moving and increments the stuck counter.
        public bool IsBotStuck(float dist)
        {
            // Calculate change in distance and assume any change less than .25f means the bot hasnt moved.
            float changeInDist = Math.Abs(_distanceToDestination - dist);
            bool isStuck = changeInDist < 0.25f;

            if (isStuck)
            {
                _log.LogDebug(
                    $"(Stuck: {_stuckCount}) Bot {BotOwner.Id} has not moved {changeInDist}. Dist from loot: {dist}"
                );

                // Bot is stuck, update stuck count
                _stuckCount++;
            }

            return isStuck;
        }
    }
}
