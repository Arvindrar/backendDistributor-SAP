// FILE: DTOs/PurchaseOrderListDto.cs
// Updated to use the new naming convention for consistency in API responses.
using System;

namespace backendDistributor.DTOs
{
    public class PurchaseOrderListDto
    {
        public Guid Id { get; set; }
        public string? PoNumber { get; set; } // Was PurchaseOrderNo
        public string? VendorCode { get; set; }
        public string? VendorName { get; set; }
        public DateTime PoDate { get; set; } // Was PODate
        public string? Remark { get; set; } // Was PurchaseRemarks
        public decimal OrderTotal { get; set; }
    }
}