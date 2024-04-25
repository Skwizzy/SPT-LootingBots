[![Latest release downloads](https://img.shields.io/github/downloads/skwizzy/SPT-LootingBots/latest/total?label=dowloads%40latest)](https://github.com/Skwizzy/SPT-LootingBots/releases/tag/v1.3.1-aki-3.8.0)
[![Beta release downloads](https://img.shields.io/github/downloads/Skwizzy/SPT-LootingBots/v1.3.2-aki-3.8.0-beta/total)](https://github.com/Skwizzy/SPT-LootingBots/releases/tag/v1.3.2-aki-3.8.0-beta)


# SPT-LootingBots

This mod aims to add a bit more life to the bots by enhancing some of the base EFT looting behaviors, allowing bots to pick up loot in the current raid. 

## Dependencies
**SPT-BigBrain**: https://github.com/DrakiaXYZ/SPT-BigBrain/releases

## Behavior

### Base game behavior:
  - Scavs start a raid on patrol, when they finish a combat engagement they will return to patrol mode after the amount of seconds specified in the `Mind.TIME_TO_FORGOR_ABOUT_ENEMY_SEC` bot config property
  - When scavs are on patrol, they have a chance to inspect a nearby corpse and only loot their primary weapon
  - When scavs are on patrol, sometimes they stop in front of a lootable container and pretend to loot it
  - PMCs and Scavs in SPT spawn with some potentially valuable loot already in their inventory
  
### Modded behavior:
  - New bot brain layer added for looting that replaces the base game logic responsible for "looting"
  - New looting layer will activate every 10 seconds (by default) during a patrol causing the bots to scan for a nearby lootable item, container, or corpse (based on a configurable distance)
  - Once a lootable object has been found, bots will attempt to navigate to the object and commence looting
  - Bots will attempt to loot everything from a corpse and a container
  - Bots will examine each item for about 1 second before looting it (simulates discovering items when searcing containers/corpses)
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
- `Enable corpse line of sight check` - When scanning for loot, corpses will be ignored if they are not visible by the bot
- `Detect corpse distance` - Distance (in meters) a bot is able to detect a corpse
- `Enable container looting` - Enables container looting for the selected bot types
- `Enable container line of sight check` - When scanning for loot, containers will be ignored if they are not visible by the bot
- `Detect container distance` - Distance (in meters) a bot is able to detect a container
- `Enable loose item looting` - Enables loose item looting for the selected bot types
- `Enable item line of sight check` - When scanning for loot, loose items will be ignored if they are not visible by the bot
- `Detect item distance` - Distance (in meters) a bot is able to detect an item
- `Log Levels` - Enable different levels of log messages to show in the logs
- `Debug: Show navigation points` - Renders shperes where bots are trying to navigate when container looting. (Red): Container position. (Green): Calculated bot destination. (Blue): NavMesh corrected destination (where the bot will move).

**Loot Finder (Timing)**
- `Delay after spawn` - Amount of seconds a bot will wait to start their first loot scan after spawning into raid.
- `Delay after taking item (ms)` - Amount of milliseconds a bot will wait after taking an item into their inventory before attempting to loot another item. Simulates the amount of time it takes for a player to look through loot decide to take something.
- `Enable examine time` - Adds a delay before looting an item to simulate the time it takes for a bot to \"uncover (examine)\" an item when searching containers, items and corpses. The delay is calculated using the ExamineTime of an object and the AttentionExamineTime of the bot.
- `Loot scan interval` - The amount of seconds the bot will wait until triggering another loot scan

**Loot Settings**
- `Use flea market prices` - Bots will query more accurate ragfair prices to do item value checks. Will make a query to get ragfair prices when the client is first started. May affect initial client start times.
- `Calculate value from attachments` - Calculate weapon value by looking up each attachement. More accurate than just looking at the base weapon template but a slightly more expensive check.
- `Allow weapon attachment stripping` - Allows bots to take the attachments off of a weapon if they are not able to pick the weapon up into their inventory
- `PMC: Min loot value threshold` - PMC bots will only loot items that exceed the specified value in roubles. When set to 0, bots will ignore the minimum value threshold
- `PMC: Max loot value threshold` - PMC bots will NOT loot items that exceed the specified value in roubles. When set to 0, bots will ignore the maximum value threshold
- `PMC: Allowed gear to equip` - The equipment a PMC bot is able to equip during raid
- `PMC: Allowed gear in bags` - The equipment a PMC bot is able to place in their backpack/rig
- `Scav: Min loot value threshold` - All non-PMC bots will only loot items that exceed the specified value in roubles. When set to 0, bots will ignore the minimum value threshold
- `Scav: Max loot value threshold` - All non-PMC bots will NOT loot items that exceed the specified value in roubles. When set to 0, bots will ignore the maximum value threshold
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
    
