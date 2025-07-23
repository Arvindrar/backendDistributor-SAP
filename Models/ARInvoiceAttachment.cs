using System;

namespace backendDistributor.Models
{
    public class ARInvoiceAttachment
    {
        public Guid Id { get; set; }
        public Guid ARInvoiceId { get; set; } // Foreign Key
        public string? FileName { get; set; }
        public string? FilePath { get; set; }

        public ARInvoice? ARInvoice { get; set; } // Navigation property
    }
}