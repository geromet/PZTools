using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Items.Distributions
{
    [Table("Distributions")]
    public class Distribution
    {
        [Key]
        public int Id { get; set; }
        public int? ProcListEntryId { get; set; }
        public string? Name { get; set; }
        public enum DistributionType
        {
            Room,
            Bag,
            Profession,
            Cache,
            Procedural
        }
        public bool? IsShop { get; set; }
        public List<Container>? Containers { get; set; } = new();
        public bool? DontSpawnAmmo { get; set; }
        public int? MaxMap { get; set; }
        public int? StashChance { get; set; }
        public int? FillRand { get; set; }
        public long? ItemRolls { get; set; }
        public int? JunkRolls { get; set; }
        public List<Item>? ItemChances { get; set; } = new();
        public List<ProcListEntry>? ProcListEntries { get; set; }

    }
}
