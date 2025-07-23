using System;
using System.Collections.Generic;

namespace backendDistributor.Models
{
    public class ARInvoice
    {
        public Guid Id { get; set; }
        public string? ARInvoiceNo { get; set; }
        public string? SalesOrderNo { get; set; } // Reference to the original Sales Order
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string? CustomerRefNumber { get; set; }
        public string? BillToAddress { get; set; }
        public string? InvoiceRemarks { get; set; }

        public ARInvoice()
        {
            ARInvoiceItems = new HashSet<ARInvoiceItem>();
            Attachments = new HashSet<ARInvoiceAttachment>();
        }

        // Relationships
        public ICollection<ARInvoiceItem> ARInvoiceItems { get; set; }
        public ICollection<ARInvoiceAttachment> Attachments { get; set; }
    }
}