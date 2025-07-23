using System;

namespace backendDistributor.DTOs
{
    public class ARInvoiceListDto
    {
        public Guid Id { get; set; }
        public string? ARInvoiceNo { get; set; }
        public string? SalesOrderNo { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal InvoiceTotal { get; set; }
        public string? InvoiceRemarks { get; set; }
    }
}