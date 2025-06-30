using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LootingBotsServerMod.Models
{
    public record ConfigModel
    {
        public bool PmcSpawnWithLoot { get; set; } = true;
        public bool ScavSpawnWithLoot { get; set; } = true;
    }
}
