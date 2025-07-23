namespace backendDistributor.DTOs
{
    public class ARInvoiceItemDto
    {
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }
        public string? Quantity { get; set; }
        public string? UOM { get; set; }
        public string? Price { get; set; }
        public string? WarehouseLocation { get; set; }
        public string? TaxCode { get; set; }
        public string? TaxPrice { get; set; }
        public string? Total { get; set; }
    }
}