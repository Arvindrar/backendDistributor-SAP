// File: Models/Customer.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;

namespace backendDistributor.Models
{
    public class Customer
    {
        [Key] // This is the primary key for the SQL database ONLY.
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string CardCode { get; set; } = string.Empty; // This is the primary key for SAP.

        [Required]
        [MaxLength(100)]
        public string CardName { get; set; } = string.Empty;
        public string CardType { get; set; } = "cCustomer";

        public int GroupCode { get; set; }
        public int? SalesPersonCode { get; set; }
        public int? ShippingType { get; set; }
        public string? Notes { get; set; }
        public string? EmailAddress { get; set; }
        public string? Phone1 { get; set; }
        public int? Territory { get; set; } // This will hold the Route ID
        public string? FederalTaxID { get; set; } // For GSTIN

        // This tells EF Core to expect a collection of related addresses.
        public virtual ICollection<BPAddress> BPAddresses { get; set; } = new List<BPAddress>();
    }
}