using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace backendDistributor.DTOs
{
    public class ARInvoiceCreateDto
    {
        public string? SalesOrderNo { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string? CustomerRefNumber { get; set; }
        public string? BillToAddress { get; set; }
        public string? InvoiceRemarks { get; set; }

        // Data from multipart/form-data
        public string? InvoiceItemsJson { get; set; }
        public List<IFormFile>? UploadedFiles { get; set; }
        public string? FilesToDeleteJson { get; set; }

        // Deserialized list
        public List<ARInvoiceItemDto>? InvoiceItems { get; set; }
    }
}