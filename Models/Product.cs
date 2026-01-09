// PASTE THIS CORRECTED CODE INTO: Models/Product.cs

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization; // <<< ADD THIS USING DIRECTIVE

namespace backendDistributor.Models
{
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonIgnore] // We don't get this from SAP, it's for the SQL DB
        public int Id { get; set; }

        [JsonPropertyName("ItemCode")]
        public string SKU { get; set; } = string.Empty;

        [JsonPropertyName("ItemName")]
        public string Name { get; set; } = string.Empty;

        public string Group { get; set; } = string.Empty; // This is set manually after fetching

        public string? UOMGroup { get; set; }

        [JsonPropertyName("InventoryUOM")]
        public string UOM { get; set; } = string.Empty;

        [JsonPropertyName("U_HS_Code")]
        public string? HSN { get; set; }

        public decimal? RetailPrice { get; set; } // Set manually from ItemPrices
        public decimal? WholesalePrice { get; set; } // Set manually from ItemPrices

        [JsonPropertyName("Picture")]
        public string? ImageFileName { get; set; }

        // This helper property will catch the nested ItemPrices array from SAP
        [JsonPropertyName("ItemPrices")]
        public List<ItemPrice> ItemPrices { get; set; } = new List<ItemPrice>();
    }

    // Helper class to deserialize the nested ItemPrices
    public class ItemPrice
    {
        public int PriceList { get; set; }
        public decimal Price { get; set; }
    }
}