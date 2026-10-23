using System;
using System.Collections.Generic;

namespace ShopSaleAPI.Models
{
    public class DocScanResult
    {
        public bool Success { get; set; } = true;
        public string ReceiptNo { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Date { get; set; } = string.Empty;
        public string MerchantName { get; set; } = string.Empty;
        public string TaxAmount { get; set; } = "0.00";
        public string PaymentMethod { get; set; } = "Card";
        public List<DocScanItem> Items { get; set; } = new List<DocScanItem>();
        public string RawText { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; } = 0.96;
        public string ProcessedAt { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public class DocScanItem
    {
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class CreateSaleDto
    {
        public int StoreID { get; set; } = 1;
        public int ProductID { get; set; }
        public int QuantitySold { get; set; }
        public DateTime? SaleDate { get; set; }
    }
}
