using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataInput.Models.Interfaces
{
    public interface IItemParent : ICommon
    {
        public List<Item>? ItemChances { get; set; }
        public List<Item>? JunkChances { get; set; }
        public int? ItemRolls { get; set; }
        public int? JunkRolls { get; set; }
        public bool? DontSpawnAmmo { get; set; }
    }
}
