using System.Text;

namespace DataInput.Models
{
    public class Container
    {
        public string? Name { get; set; }
        public bool? FillRand { get; set; }
        public bool? Procedural { get; set; }
        public bool? DontSpawnAmmo { get; set; }
        public int? ItemRolls { get; set; }
        public int? JunkRolls { get; set; }
        public List<Item>? ItemChances { get; set; } = new();
        public List<Item>? JunkChances { get; set; } = new();
        public List<ProcListEntry>? ProcListEntries { get; set; } = new();
        public override string ToString()
        {
            return Name;
        }
        public string ToFullString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Name);
            if (DontSpawnAmmo.HasValue) sb.AppendLine("DontSpawnAmmo");
            if (FillRand.HasValue) sb.AppendLine("FillRand : " + FillRand.Value.ToString());
            if (ItemRolls.HasValue) sb.AppendLine("ItemRolls : " + ItemRolls.Value.ToString());
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
            if (ProcListEntries.Any())
            {
                sb.AppendLine("ProcList : ");
                foreach (ProcListEntry procListEntry in ProcListEntries)
                {
                    sb.AppendLine(procListEntry.ToString());
                }
            }
            return sb.ToString();
        }
    }
}