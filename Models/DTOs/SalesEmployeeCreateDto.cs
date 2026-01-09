// PASTE THIS ENTIRE CODE INTO: Dtos/SalesEmployeeCreateDto.cs

using System.ComponentModel.DataAnnotations;

namespace backendDistributor.Dtos
{
    // This DTO defines the exact shape of the JSON the frontend sends to our API.
    // It uses standard C# property names.
    public class SalesEmployeeCreateDto
    {
        [Required(ErrorMessage = "Employee Code is required.")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Employee Name is required.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Contact Number is required.")]
        public string? ContactNumber { get; set; }

        [EmailAddress(ErrorMessage = "Invalid Email Address format.")]
        public string? Email { get; set; }

        public string? Remarks { get; set; }
    }
}