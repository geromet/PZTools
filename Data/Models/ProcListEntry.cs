﻿using System.Text;
using DataInput.Models.Interfaces;

namespace DataInput.Models
{
    public class ProcListEntry : ICommon
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
        
    }
}