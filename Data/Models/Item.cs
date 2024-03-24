﻿namespace DataInput.Models
{
    public class Item
    {
        public double? Chance { get; set; }
        public string? Name { get; set; }
        public override string ToString()
        {
            if (Chance.HasValue)
            {
                return Name + "   " + Chance;
            }
            else
            {
                return Name;
            }
        }
    }
}