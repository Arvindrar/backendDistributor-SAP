using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace backendDistributor.Dtos
{
    public class ProductCreateDto
    {
        [Required]
        public string SKU { get; set; } = string.Empty;

        [Required]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        public string ProductGroup { get; set; } = string.Empty;

        [Required]
        public string UOM { get; set; } = string.Empty;

        public string? UOMGroup { get; set; }

        public string? HSN { get; set; }

        public string? RetailPrice { get; set; }

        public string? WholesalePrice { get; set; }

        public IFormFile? ProductImage { get; set; }
    }
}