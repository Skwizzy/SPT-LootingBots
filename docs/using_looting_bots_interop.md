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
### TryForceBotToLootNow(BotOwner, float)
Force a bot to search for loot immediately if LootingBots is loaded. If a bot is not looting, a loot scan will be forced until the amount of seconds specified by `duration` has passed. 

Returns `true` if the command was issued successfully

```c#
public static bool TryForceBotToLootNow(BotOwner botOwner, float duration)
```

**Params**
```c#
BotOwner botOwner // BotOwner instance of the bot we want to manipulate
float duration // Amount of seconds the command will exist in the bot's brain before expiring
```

**Example**
```c#
// Before using, we should always make sure interop has been intialized (see Init())
if (!canUseLootingBotsInterop) {
    return false;
}

// Instructs the bot to start the looting logic and run a scan. Loot scans for the next 15 seconds will not respect the normal loot scan interval and are run immediately after looting has been completed
if (LootingBots.LootingBotsInterop.TryForceBotToLootNow(botOwner, 15f))
{
    // Custom code after command success
}
```
___
### TryPreventBotFromLooting(BotOwner, float)
Prevent a bot from searching for loot for the amount of time specified by `duration`. 

Returns `true` if command was issued successfully.

```c#
public static bool TryPreventBotFromLooting(BotOwner botOwner, float duration)
```
**Example**
```c#
// Before using, we should always make sure interop has been intialized (see Init())
if (!canUseLootingBotsInterop) {
    return false;
}

// Prevent the bot from looting for the next 30 seconds
if (LootingBots.LootingBotsInterop.TryPreventBotFromLooting(botOwner, 30f))
{
    // Custom code after command success
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
