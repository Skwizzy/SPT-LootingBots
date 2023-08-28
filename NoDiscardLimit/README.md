## Description ##
A server side mod that serves two main purposes for LootingBots. 

1. Marks all items with DiscardLimits as InsuranceDisabled. It then disables the DiscardLimit settings for the server via the EnableDiscardLimits option in Server/database/globals.json. SPT PMC bots by default spawn with loot already in their backpacks, this loot is not marked Found In Raid and thus is subject to BSG's RMT protection logic. With discard limits enabled, when a bot drops their backback to swap to a new one any loot with discard limits in their bag will be deleted immediately when the bag is dropped. To avoid this we set the EnableDiscardLimits to false, and also make sure to flag all items with a DiscardLimit >= 0 as InsuranceDisabled to prevent items suchs as keys and cases to be insured.

2. Provide the option to clear out the loot that PMC/Scav bots start with in their backpacks. This does not include meds, ammo, grenades ect. These options can be found in the `NoDiscardLimit/config/config.json`.
    - `bot_types`: List of bot types to remove loot from. Includes "bear", and "usec" as defaults
        - `"assault"` - Scavs
        - `"bear"` - PMC Bear
        - `"usec"` - PMC Usec
    - `empty_bag` - When set to `true`, empty the bags of all bot_types
    - `empty_pockets` - When set to `true`, empty the pockets of all bot_types

        Default config: 
        ```
        {
            "bot_types": [
                "assault",
                "bear",
                "usec"
            ],
            "empty_bag": true,
            "empty_pockets": true
        }
        ```