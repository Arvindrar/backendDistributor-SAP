// Models/SalesEmployee.cs

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace backendDistributor.Models
{
    public class SalesEmployee
    {
        [Key]
        // This maps the incoming JSON property from SAP to our "Id" property
        [JsonPropertyName("SalesEmployeeCode")]
        public int Id { get; set; }

        // This property is set manually in the service, so no mapping is needed here
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Sales Employee Name is required.")]
        [JsonPropertyName("SalesEmployeeName")]
        public string Name { get; set; } = string.Empty;
        // --- END OF FIX ---

        [JsonPropertyName("Mobile")]
        public string? ContactNumber { get; set; }

        [JsonPropertyName("Email")]
        public string? Email { get; set; }

        [JsonPropertyName("Remarks")]
        public string? Remarks { get; set; }

        // The frontend and database will use this standard boolean property.
        // We ignore it during deserialization from SAP because SAP sends a string.
        [JsonIgnore]
        public bool IsActive { get; set; } = true;

        // --- THIS IS THE FIX ---
        // This helper property is added to catch the string value ("tYES" or "tNO")
        // from the "Active" property in the SAP JSON response.
        [JsonPropertyName("Active")]
        public string? ActiveSap { get; set; }
        // --- END OF FIX ---
    }
}