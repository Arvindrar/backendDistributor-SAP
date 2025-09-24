// File: Models/BPAddress.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace backendDistributor.Models
{
    public class BPAddress
    {
        [Key]
        public int Id { get; set; } // Primary key for the SQL table

        [Required]
        public string BPCode { get; set; } // Foreign key link to the Customer

        [Required]
        public string AddressType { get; set; } = "bo_BillTo"; // e.g., "bo_BillTo" or "bo_ShipTo"

        public string? AddressName { get; set; }
        public string? Street { get; set; }
        public string? Block { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string Country { get; set; } = "IN"; // Default to IN
        public string? GstType { get; set; }
        public string? GSTIN { get; set; }

        // Navigation property
        [JsonIgnore] // Prevent circular references during serialization
        public Customer? Customer { get; set; }
    }
}