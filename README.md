[![Latest release downloads](https://img.shields.io/github/downloads/skwizzy/SPT-LootingBots/latest/total?label=dowloads%40latest)](https://github.com/Skwizzy/SPT-LootingBots/releases/tag/v1.1.2-aki-3.6.0)
[![Beta release downloads](https://img.shields.io/github/downloads/Skwizzy/SPT-LootingBots/v1.1.3-aki-3.5.8-beta/total)](https://github.com/Skwizzy/SPT-LootingBots/releases/tag/v1.1.3-aki-3.6.0-beta)


# SPT-LootingBots

This mod aims to add a bit more life to the bots by enhancing some of the base EFT looting behaviors, allowing bots to pick up loot in the current raid. 

## Dependencies
**SPT-BigBrain ^0.2.0**: https://github.com/DrakiaXYZ/SPT-BigBrain/releases/tag/0.2.0

## Behavior

### Base game behavior:
  - Scavs start a raid on patrol, when they finish a combat engagement they will return to patrol mode after the amount of seconds specified in the `Mind.TIME_TO_FORGOR_ABOUT_ENEMY_SEC` bot config property
  - When scavs are on patrol, they have a chance to inspect a nearby corpse and only loot their primary weapon
  - When scavs are on patrol, sometimes they stop in front of a lootable container and pretend to loot it
  - PMCs and Scavs in SPT spawn with some potentially valuable loot already in their inventory
  
### Modded behavior:
  - New bot brain layer added for looting that replaces the base game logic responsible for "looting"
  - New looting layer will activate every 6 seconds during a patrol causing the bots to scan for a nearby lootable item, container, or corpse (based on a configurable distance)
  - Once a lootable object has been found, bots will attempt to navigate to the object and commence looting
  - Bots will attempt to loot everything from a corpse and a container
  - If a bot cannot equip a piece of gear, they will attempt to place it in their inventory 
  - Not all loot is navigable, relies heavily on the availablity of a nearby NavMesh that bots can use to navigate
    - If loot is behind a door, bots will open the door if unlocked 
    - If a bot is stuck in place or if the bot spends too much time moving, the loot will be ignored
  - Once looting has finished, bots will wait a certain amount of time before the next loot scan period occurs (configurable in the settings)
  - PMCs by default will no longer spawn with loot in their inventories. (can be changed in the settings for NoDiscardLimit)

**Gear Swap Critria** 
- Bot will always swap to gear that has higher armor rating (helmets, armor vests, armored rigs)
- Backpack will be swapped if backpack being looted has more slots
- When looting larger rigs, bots will swap if currently equipped rig is of equal or lower armor class
- When throwing old backpacks/tactical rigs, bots try to take all the loot from the container thrown
- When looting weapons, bots will compare the item's Handbook(default) or Flea market price in rubles to the value of the weapons currently equipped. 
  - Looted weapons with higer value will replace an equipped weapon with the lowest value
  - Bots prefer to use the higest value weapon as their primary (if they have ammo)

## Mod Settings (F12)
**Loot Finder**
- `Enable corpse looting` - Enables corpse looting for the selected bot types
- `Detect corpse distance` - Distance (in meters) a bot is able to detect a corpse
- `Enable container looting` - Enables container looting for the selected bot types
- `Detect container distance` - Distance (in meters) a bot is able to detect a container
- `Enable loose item looting` - Enables loose item looting for the selected bot types
- `Detect item distance` - Distance (in meters) a bot is able to detect an item
- `Log Levels` - Enable different levels of log messages to show in the logs
- `Debug: Show navigation points` - Renders shperes where bots are trying to navigate when container looting. (Red): Container position. (Green): Calculated bot destination. (Blue): NavMesh corrected destination (where the bot will move).

**Loot Finder (Timing)**
- `Delay after spawn` - Amount of seconds a bot will wait to start their first loot scan after spawning into raid.
- `Transaction delay (ms)` - Amount of milliseconds a bot will wait after a looting transaction has occured before attempting another transaction. Simulates the amount of time it takes for a player to look through loot and equip things.
- `Delay between looting` - The amount of seconds the bot will wait after looting an container/item/corpse before trying to find the next nearest item/container/corpse

**Loot Settings**
- `Use flea market prices` - Bots will query more accurate ragfair prices to do item value checks. Will make a query to get ragfair prices when the client is first started. May affect initial client start times.
- `Calculate value from attachments` - Calculate weapon value by looking up each attachement. More accurate than just looking at the base weapon template but a slightly more expensive check.
- `Allow weapon attachment stripping` - Allows bots to take the attachments off of a weapon if they are not able to pick the weapon up into their inventory
- `PMC: Loot value threshold` - PMC bots will only loot items that exceed the specified value in roubles
- `PMC: Allowed gear to equip` - The equipment a PMC bot is able to equip during raid
- `PMC: Allowed gear in bags` - The equipment a PMC bot is able to place in their backpack/rig
- `Scav: Loot value threshold` - All non-PMC bots will only loot items that exceed the specified value in roubles.
- `Scav: Allowed gear to equip` - The equipment a non-PMC bot is able to equip during raid
- `Scav: Allowed gear in bags` - The equipment a non-PMC bot is able to place in their backpack/rig
- `Log Levels` - Enable different levels of log messages to show in the logs

## Server Mod Settings (NoDiscardLimit/config/config.json)
- `pmcSpawnWithLoot` - When set to `true`, PMCs will spawn with loot in their bags/pockets (default SPT behavior)
- `scavSpawnWithLoot` - When set to `true`, Scavs will spawn with loot in the bags/pockets (default SPT behavior)

  Default config: 
  ```
  {
      "pmcSpawnWithLoot": false,
      "scavSpawnWithLoot": true
  }

## Conflicts

This mod will conflict with any server mod that sets the `globals.config.DiscardLimitsEnabled` to true. PMC bots will throw exceptions when attempting to discard gear with DiscardLimits set. This needs to be false for the mod to function properly with pmc looting.

## Planned features:
- [x] Looting of every item on corpses
- [x] Equipment swapping
- [x] Bot preference to use looted weapons that are higher in market value
- [x] When swapping rigs/bags, transfer items from old item into new item
- [x] Add corpse looting to pmc bots
- [ ] Stronger checks for when to equip a new primary weapon
- [x] Weapon attachement stripping
- [x] Enhance base logic for adding corpses to loot pool
- [x] Apply same looting logic to patrol patterns where scavs stop in front of lootable containers
- [x] Loose loot detection
- [ ] Container nesting
- [x] Customizable params in mod settings

## Package Contents
- `BepInEx/plugins/skwizzy.LootingBots.dll` - Client plugin responsible for all the new corpse looting logic
- `user/mods/Skwizzy-NoDiscardLimit` - Provide the option to clear out the loot that PMC/Scav bots start with in their backpacks. This does not include meds, ammo, grenades ect. These options can be found in the `NoDiscardLimit/config/config.json`.

## Install instructions
Simply extract the contents of the .zip file into your SPT directory.
    
