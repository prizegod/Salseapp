using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ShopSaleAPI.Models
{
    public class DailySale
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SaleID { get; set; }

        [Required]
        [ForeignKey(nameof(Store))]
        public int StoreID { get; set; }

        [Required]
        [ForeignKey(nameof(Product))]
        public int ProductID { get; set; }

        [Required]
        public DateTime SaleDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int QuantitySold { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalAmount { get; set; }

        // Navigation properties
        public virtual Store? Store { get; set; }
        public virtual Product? Product { get; set; }
    }
}
