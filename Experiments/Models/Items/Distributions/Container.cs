using System.ComponentModel.DataAnnotations;
namespace Data.Models.Items.Distributions
{
    public class Container
    {
        [Key]
        public int Id { get; set; }
        public Distribution Distribution { get; set; }
        public int DistributionId { get; set; }
        public string? Name { get; set; }
        public bool? Procedural { get; set; }
        public bool? DontSpawnAmmo { get; set; }
        public bool? FillRand { get; set; }
        public int? ItemRolls { get; set; }
        public int? JunkRolls { get; set; }
        public List<Item>? ItemChances { get; set; } = new();
        public List<ProcListEntry>? ProcListEntries { get; set; } = new();



    }
}
