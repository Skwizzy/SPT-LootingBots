[![Latest release downloads](https://img.shields.io/github/downloads/skwizzy/SPT-LootingBots/latest/total?label=dowloads%40latest)](https://github.com/Skwizzy/SPT-LootingBots/releases/tag/v1.0.1)
[![Beta downloads](https://img.shields.io/github/downloads/skwizzy/SPT-LootingBots/v1.0.2-beta/total)](https://github.com/Skwizzy/SPT-LootingBots/releases/tag/v1.0.2-beta)

# SPT-LootingBots

This mod aims to add a bit more life to the bots by enhancing some of the base EFT looting behaviors, allowing bots to pick up loot in the current raid. 

### Base game behavior:
  - Scavs start a raid on patrol, when they finish a combat engagement they will return to patrol mode after the amount of seconds specified in the `Mind.TIME_TO_FORGOR_ABOUT_ENEMY_SEC` bot config property
  - Scavs by default have their `Patrol.CAN_LOOK_TO_DEADBODIES` property set to `true` which allows them to check dead bodies during patrols
  - When scavs are on patrol, they have a chance to inspect a nearby corpse and only loot their primary weapon
  - When scavs are on patrol, sometimes they stop in front of a lootable container and pretend to loot it
  
### Modded behavior:
  - Base game corpse looting behavior added to all bot types (option for pmc)
  - Bots on patrol that stop in front of containers will attempt to place items in their inventory
  - When a bot goes to loot the primary weapon of a corpse they now attempt to loot everything from the corpse, equipping things in empty slots and swapping out gear for better gear.
  - If a bot cannot equip a piece of gear, they will attempt to place it in their inventory
  - Bots are now able to treat nearby containers similar to how they treat corpses and attempt to loot the items from the container
    - Not all containers are navigable, relies heavily on the availablity of a nearby NavMesh that bots can use to navigate
    - If a container is behind a door, bots will open the door if unlocked. Bots can also close doors if they are stuck on an open door.

**Gear Swap Critria** 
- Bot will always swap to gear that has higher armor rating (helmets, armor vests, armored rigs)
- Backpack will be swapped if backpack being looted has more slots
- When looting larger rigs, bots will swap if currently equipped rig is of equal or lower armor class
- When throwing old backpacks/tactical rigs, bots try to take all the loot from the container thrown
- When looting weapons, bots will compare the item's Handbook(default) or Flea market price in rubles to the value of the weapons currently equipped. 
  - Looted weapons with higer value will replace an equipped weapon with the lowest value
  - Bots prefer to use the higest value weapon as their primary (if they have ammo)

## Mod Settings (F12)
**Container Looting**
- `Enable reserve patrols` - Enables looting of containers for bots on patrols that stop in front of lootable containers (reserve patrols)
- `Enable dynamic looting` - Enable dynamic looting of containers, will detect containers within the set distance and navigate to them similar to how they would loot a corpse. More resource demanding than reserve patrol looting. 
- `Dynamic Looting: Dynamic looting: Delay between containers` - The amount of time the bot will wait after looting a container before trying to find the next nearest contianer
- `Dynamic looting: Detect container distance` - Distance (in meters) a bot is able to detect a container

**Corpse Looting**
- `Enable looting` - Enables corpse looting for the selected bot types. Takes affect during the generation of the next raid. Defaults to `all`
- `Distance to see body` - Distance in meters to body until it can be "seen" by a bot
- `Distance to forget body` - Distance in meters from a body until its "forgotten" by a bot
- `Looting time (*)` - Time in seconds the bot will stand over a corpse. *Warning - Changing lower than 8 seconds may result in issues
- `Log Levels` - Enable different levels of log messages to show in the logs

**Weapon Loot Settings**
- `Use flea market prices` - Bots will query more accurate ragfair prices to do item value checks. Will make a query to get ragfair prices when the client is first started. May affect initial client start times.
- `Calculate value from attachments` - Calculate weapon value by looking up each attachement. More accurate than just looking at the base weapon template but a slightly more expensive check. Disable if experiencing performance issues!


## Conflicts
This mod may conflict with any client mod that attempts to alter the following bot settings: (bots may not exhibit base EFT looting behavior)
```
Patrol.CAN_LOOK_TO_DEADBODIES
Mind.HOW_WORK_OVER_DEAD_BODY
Patrol.DEAD_BODY_SEE_DIST
Patrol.DEAD_BODY_LEAVE_DIST
Patrol.DEAD_BODY_LOOK_PERIOD
```

This mod will conflict with any server mod that sets the `globals.config.DiscardLimitsEnabled` to true. PMC bots will throw exceptions when attempting to discard gear with DiscardLimits set. This needs to be false for the mod to function properly with pmc looting.

## Planned features:
- [x] Looting of every item on corpses
- [x] Equipment swapping
- [x] Bot preference to use looted weapons that are higher in market value
- [x] When swapping rigs/bags, transfer items from old item into new item
- [x] Add corpse looting to pmc bots
- [ ] Stronger checks for when to equip a new primary weapon
- [ ] Weapon attachement stripping
- [ ] Enhance base logic for adding corpses to loot pool
- [x] Apply same looting logic to patrol patterns where scavs stop in front of lootable containers
- [ ] Loose loot detection
- [ ] Container nesting
- [ ] Customizable params in mod settings

## Package Contents
- `BepInEx/plugins/skwizzy.LootingBots.dll` - Client plugin responsible for all the new corpse looting logic
- `user/mods/Skwizzy-NoDiscardLimits-1.0.0` - Small server plugin that sets DiscardLimitsEnabled to false in the server/globals/config. Fixes issues with PMC bots throwing exceptions when discarding items with DiscardLimits (this is the EFT live RMT protection logic)

## Install instructions
Simply extract the contents of the .zip file into your SPT directory.
    
