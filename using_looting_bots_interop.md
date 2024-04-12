# Using the Interop capabilities

Thanks to @dwesterwick, LootingBots now has support for external commands without requiring LootingBots as a dependency for mods.

### To Use
- Add the [LootingBotsInterop.cs](https://github.com/Skwizzy/SPT-LootingBots/blob/3.7.1/LootingBots/LootingBots.cs) file from `LootingBots/LootingBotsInterop.cs` into your project 
- Use the Init() method to intialize the interop utils before using any other methods from the interop file


## [LootingBotsInterop](https://github.com/Skwizzy/SPT-LootingBots/blob/3.7.1/LootingBots/LootingBotsInterop.cs) Methods

### Init

Initializes the LootingBotsInterop class and instantiates hooks to the methods specified in [External.cs](https://github.com/Skwizzy/SPT-LootingBots/blob/3.7.1/LootingBots/External.cs). 

Returns `true` if the LootingBots plugin was detected 

```c#
public static bool Init()
```
**Example**
```c#
class MyComponent {
    bool canUseLootingBotsInterop = false;

    public MyComponent() {
        // Initialize the LootingBotsInterop and save the results to a boolean to be used in other methods of the class
        canUseLootingBotsInterop = LootingBots.LootingBotsInterop.Init();
    }
}
```

___
### TryForceBotToScanLoot(BotOwner, float)
Force a bot to search for loot immediately if LootingBots is loaded. A bot will scan for loot the next time it is available (not currently looting). 

Returns `true` if the command was issued successfully

```c#
public static bool TryForceBotToLootNow(BotOwner botOwner)
```

**Params**
```c#
BotOwner botOwner // BotOwner instance of the bot we want to manipulate
```

**Example**
```c#
// Before using, we should always make sure interop has been intialized (see Init())
if (!canUseLootingBotsInterop) {
    return false;
}

// Force the bot to scan for loot the next time it is able to
if (LootingBots.LootingBotsInterop.TryForceBotToScanLoot(botOwner))
{
    // Custom code after command success
}
```
___
### TryPreventBotFromLooting(BotOwner, float)
Stops a bot from looting and prevents a bot from searching for loot for the amount of time specified by `duration`. 

Returns `true` if command was issued successfully.

```c#
public static bool TryPreventBotFromLooting(BotOwner botOwner, float duration)
```

**Params**
```c#
BotOwner botOwner // BotOwner instance of the bot we want to manipulate
float duration // Amount of seconds that must elapse before the bot will be allowed to loot again
```

**Example**
```c#
// Before using, we should always make sure interop has been intialized (see Init())
if (!canUseLootingBotsInterop) {
    return false;
}

// Stop any looting and prevent the bot from looting for the next 30 seconds
if (LootingBots.LootingBotsInterop.TryPreventBotFromLooting(botOwner, 30f))
{
    // Custom code after command success
}
```
___
### CheckIfInventoryFull
Returns `true` if a bot's inventory has been flagged as full by the LootingBrain (less than 2 available grid slots)

```c#
public static bool CheckIfInventoryFull(BotOwner botOwner)
```

**Params**
```c#
BotOwner botOwner // BotOwner instance of the bot to check
```

**Example**
```c#
// Before using, we should always make sure interop has been intialized (see Init())
if (!canUseLootingBotsInterop) {
    return false;
}


if (LootingBots.LootingBotsInterop.CheckIfInventoryFull(botOwner)) {
    // Custom Logic to execute if inventory is full
}
```
___
### GetNetLootValue
Returns a `float` representing the net loot value of all the items looted by a bot during the raid

```c#
public static float GetNetLootValue(BotOwner botOwner)
```

**Params**
```c#
BotOwner botOwner // BotOwner instance of the bot to check
```

**Example**
```c#
// Before using, we should always make sure interop has been intialized (see Init())
if (!canUseLootingBotsInterop) {
    return false;
}

float totalValueLooted = LootingBots.LootingBotsInterop.GetNetLootValue(botOwner);
if (totalValueLooted > 10000f) {
    // Some special logic if bot has looted over a certain value
}
```

___
### GetItemPrice
Uses the LootingBots ItemAppraiser to find the value of the item as a `float`. Depeding on the F12 settings, this will either use the handbook or the flea market

```c#
public static float GetItemPrice(LootItem item)
```

**Params**
```c#
LootItem item // LootItem instance of the item that we want to appraise
```

**Example**
```c#
// Before using, we should always make sure interop has been intialized (see Init())
if (!canUseLootingBotsInterop) {
    return false;
}

float itemPrice = LootingBots.LootingBotsInterop.GetItemPrice(botOwner);
if (itemPrice > 10000f) {
    // Some special logic if an item exceeds a specific value
}
```
___
### IsLootingBotsLoaded
Return `true` if LootingBots plugin has been detected in the client. Used internally by the `Init()` method

```c#
public static bool IsLootingBotsLoaded()
```
**Example**
```c#
if (LootingBots.LootingBotsInterop.IsLootingBotsLoaded()) {
    // Custom Logic to execute if LootingBots was detected
}
```
