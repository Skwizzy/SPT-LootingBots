# SPT-LootingBots

This mod aims to add a bit more life to the bots by enhancing some of the base EFT looting behaviors, allowing bots to pick up loot in the current raid. 

### Base game behavior:
  - Scavs start a raid on patrol, when they finish a combat engagement they will return to patrol mode after the amount of seconds specified in the `Mind.TIME_TO_FORGOR_ABOUT_ENEMY_SEC` bot config property
  - Scavs by default have their `Patrol.CAN_LOOK_TO_DEADBODIES` property set to `true` which allows them to check dead bodies during patrols
  - When scavs are on patrol, they have a chance to inspect a nearby corpse and only loot their primary weapon
  - When scavs are on patrol, sometimes they stop in front of a lootable container and pretend to loot it
  
### Modded behavior:
  - Base game corpse looting behavior added to all bot types (option for pmc)
  - When a bot goes to loot the primary weapon of a corpse they now attempt to loot everything from the corpse, equipping things in empty slots and swapping out gear for better gear.
  - If a bot cannot equip a piece of gear, they will attempt to place it in their bag/rig

**Gear Swap Critria** 
- Bot will always swap to gear that has higher armor rating (helmets, armor vests, armored rigs)
- Backpack will be swapped if backpack being looted has more slots
- When looting larger rigs, bots will swap if currently equipped rig is of equal or lower armor class
- When throwing old backpacks/tactical rigs, bots try to take all the loot from the container thrown
- When looting weapons, bots will compare the item's value in roubles to the value of the weapons currently equipped. 
  - Looted weapons with higer value will replace an equipped weapon with the lowest value
  - Bots prefer to use the higest value weapon as their primary (if they have ammo)

### Mod Settings (F12)
- `Enable Debug` - Enables logging in the plugin, does not require restart
- `PMCs can loot` - Enables config changes to allow looting behavior
- `Distance to see body` - Distance in meters to body until it can be "seen" by a bot
- `Distance to forget body` - Distance in meters from a body until its "forgotten" by a bot
- `Looting time (warning)` - Time in seconds the bot will stand over a corpse. Changing lower than 8 seconds may result in issues
    
## Planned features:
- [x] Looting of every item on corpses
- [x] Equipment swapping
- [x] Bot preference to use looted weapons that are higher in market value
- [x] When swapping rigs/bags, transfer items from old item into new item
- [x] Add corpse looting to pmc bots
- [ ] Stronger checks for when to equip a new primary weapon
- [ ] Weapon attachement stripping
- [ ] Enhance base logic for adding corpses to loot pool
- [ ] Apply same looting logic to patrol patterns where scavs stop in front of lootable containers
- [ ] Loose loot detection
- [ ] Container nesting
- [ ] Customizable params in mod settings


## Unknowns:
- Sometimes bots will not loot corpses even after a large amount of time has passed. Need to investigate the base EFT logic and see if I can improve this check
- Do bots despawn after a certain amount of time has passed? Could be problematic if bots with loot randomly disappear mid raid
    
