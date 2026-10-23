using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopSaleAPI.Models
{
    public class Store
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int StoreID { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string Location { get; set; } = string.Empty;

        // Navigation property for daily sales
        public virtual ICollection<DailySale>? DailySales { get; set; }
    }
}
