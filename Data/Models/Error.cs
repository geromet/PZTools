using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataInput.Models
{
    public class Error
    {
        public int? Code { get; set; }
        public string? Description { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public int? Line { get; set; }
        
    }
}
