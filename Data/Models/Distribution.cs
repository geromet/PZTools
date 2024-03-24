using System.Text;

namespace DataInput.Models
{
    public class Distribution
    {
        public string? Name { get; set; }
        public string? DistributionType { get; set; }
        public bool? IsShop { get; set; }
        public bool? DontSpawnAmmo { get; set; }
        public int? MaxMap { get; set; }
        public int? StashChance { get; set; }
        public int? FillRand { get; set; }
        public int? ItemRolls { get; set; }
        public int? JunkRolls { get; set; }
        public List<Item>? ItemChances { get; set; } = new();
        public List<Item>? JunkChances { get; set; } = new();
        public List<Container>? Containers { get; set; } = new();

        public string ToFullString()
        {
            StringBuilder sb = new StringBuilder();
            if (IsShop.HasValue) if (IsShop.Value) sb.AppendLine("IsShop");
            if (DontSpawnAmmo.HasValue) if (DontSpawnAmmo.Value) sb.AppendLine("DontSpawnAmmo");
            if (MaxMap.HasValue) sb.AppendLine("MaxMap : " + MaxMap.Value.ToString());
            if (StashChance.HasValue) sb.AppendLine("StashChance : " + StashChance.Value.ToString());
            if (FillRand.HasValue) sb.AppendLine("FillRand : " + FillRand.Value.ToString());
            if (ItemRolls.HasValue) sb.AppendLine("ItemRolls : " + ItemRolls.Value.ToString());
            if (JunkRolls.HasValue) sb.AppendLine("JunkRolls : " + JunkRolls.Value.ToString());
            /*if (ItemChances.Any())
            {
                sb.AppendLine("Items : ");
                foreach (Item item in ItemChances)
                {
                    sb.AppendLine(item.ToString());
                }
            }*/
            if (JunkChances.Any())
            {
                sb.AppendLine("Junk : ");
                foreach (Item item in JunkChances)
                {
                    sb.AppendLine(item.ToString());
                }
            }
            return sb.ToString();

        }

    }
}