// FILE: DTOs/PurchaseOrderPostDto.cs
// This DTO perfectly matches the structure of the JSON sent from the React frontend.
namespace backendDistributor.DTOs
{
    public class PurchaseOrderPostDto
    {
        public string? PoNumber { get; set; } // Matches "poNumber"
        public string? VendorCode { get; set; }
        public string? VendorName { get; set; }
        public DateTime PoDate { get; set; }
        public DateTime? DeliveryDate { get; set; } // Added this field from your form
        public string? Address { get; set; } // Matches "address"
        public string? Remark { get; set; } // Matches "remark"
        public List<PostingPurchaseOrderDetailDto>? PostingPurchaseOrderDetails { get; set; }
    }

    public class PostingPurchaseOrderDetailDto
    {
        public int SINo { get; set; } // Matches "sINo"
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }
        public decimal Qty { get; set; } // Matches "qty"
        public string? UomCode { get; set; } // Matches "uomCode"
        public decimal Price { get; set; }
        public string? LocationCode { get; set; } // Matches "locationCode"
        public string? TaxCode { get; set; }
        public decimal? TotalTax { get; set; } // Matches "totalTax"
        public decimal NetTotal { get; set; } // Matches "netTotal"
    }
}