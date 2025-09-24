// File: Models/DTOs/SapUomGroupCreateDto.cs

namespace backendDistributor.Models.DTOs
{
    public class SapUomGroupCreateDto
    {
        // Initialize string properties to satisfy the compiler
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public int BaseUoM { get; set; }

        // Initialize the array to an empty array
        public UomGroupDefinitionDto[] UoMGroupDefinitionCollection { get; set; } = Array.Empty<UomGroupDefinitionDto>();
    }

    public class UomGroupDefinitionDto
    {
        public int AlternateUoM { get; set; }
        public double AlternateQuantity { get; set; }
        public double BaseQuantity { get; set; }
    }
}