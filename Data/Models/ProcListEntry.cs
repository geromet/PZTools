using System.Text;

namespace DataInput.Models
{
    public class ProcListEntry
    {
        public string? Name { get; set; }
        public int? WeightChance { get; set; }
        public int? Min { get; set; }
        public int? Max { get; set; }
        public string? ForceForTiles { get; set; }
        public string? ForceForRooms { get; set; }
        public string? ForceForZones { get; set; }
        public string? ForceForItems { get; set; }
        public Distribution? ProceduralDistribution { get; set; }
        
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Name);
            if (WeightChance.HasValue) sb.AppendLine("WeightChance : " + WeightChance);
            if (Min.HasValue) sb.AppendLine("Min: " + Min);
            if (Max.HasValue) sb.AppendLine("Max : " + Max);
            if (ForceForTiles != null) sb.AppendLine("ForceForTiles : " + ForceForTiles);
            if (ForceForRooms != null) sb.AppendLine("ForceForRooms : " + ForceForRooms);
            if (ForceForZones != null) sb.AppendLine("ForceForZones : " + ForceForZones);
            if (ForceForItems != null) sb.AppendLine("ForceForItems : " + ForceForItems);
            if (ProceduralDistribution != null)
                sb.AppendLine("ProceduralDistribution : " + ProceduralDistribution.ToFullString());


            return sb.ToString();
        }
    }
}