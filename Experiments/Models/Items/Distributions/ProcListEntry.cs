using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Items.Distributions
{
    public class ProcListEntry
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? Min { get; set; }
        public int? Max { get; set; }
        public int? WheightChance { get; set; }
        public string? ForceForTiles { get; set; }
        public string? ForceForRooms { get; set; }
        public string? ForceForZones { get; set; }
        public string? ForceForItems { get; set; }
        public Container? Container { get; set; }
        public int? ContainerId { get; set; }
    }
}
