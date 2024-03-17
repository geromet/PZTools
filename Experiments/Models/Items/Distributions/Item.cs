
using System.ComponentModel.DataAnnotations;
namespace Data.Models.Items.Distributions
{
    public class Item
    {
        [Key]
        public int Id { get; set; }
        public int? ContainerId { get; set; }
        public int? DistributionId { get; set; }
        public Container? Container { get; set; }
        public Distribution? Distribution { get; set; }
        public double? Chance { get; set; }
        public string? Name { get; set; }
    }
}
