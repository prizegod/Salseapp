using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ShopSaleAPI.Models
{
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Price { get; set; }

        public int Stock { get; set; }

        // React App 'stockQuantity' அனுப்பினால் அதை 'Stock'-க்கு Map செய்ய:
        [NotMapped]
        [JsonPropertyName("stockQuantity")]
        public int StockQuantity
        {
            get => Stock;
            set => Stock = value;
        }

        // Circular Reference தவறுகளைத் தவிர்க்க Navigation Property-க்கு JsonIgnore
        [JsonIgnore]
        public virtual ICollection<DailySale>? DailySales { get; set; }
    }
}