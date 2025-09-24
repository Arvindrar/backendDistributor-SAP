// Create a new file: Services/TaxService.cs
using backendDistributor.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace backendDistributor.Services
{
    public class TaxService
    {
        private readonly CustomerDbContext _context;
        private readonly SapService _sapService;
        private readonly ILogger<TaxService> _logger;
        private readonly string _dataSource;

        public TaxService(CustomerDbContext context, SapService sapService, IConfiguration configuration, ILogger<TaxService> logger)
        {
            _context = context;
            _sapService = sapService;
            _logger = logger;
            _dataSource = configuration.GetValue<string>("DataSource") ?? "SQL";
        }

        // GET all taxes
        public async Task<IEnumerable<TaxDeclaration>> GetAllAsync()
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("Fetching tax data from SAP.");
                var sapJsonResult = await _sapService.GetVatGroupsAsync();
                using var jsonDoc = JsonDocument.Parse(sapJsonResult);

                if (!jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
                {
                    return Enumerable.Empty<TaxDeclaration>();
                }

                var taxes = new List<TaxDeclaration>();
                foreach (var e in valueElement.EnumerateArray())
                {
                    // Filter for only Sales Tax ("Output Tax")

                    if (e.GetProperty("Inactive").GetString() == "tYES") continue;

                    decimal totalRate = 0;
                    if (e.TryGetProperty("VatGroups_Lines", out var lines) && lines.ValueKind == JsonValueKind.Array)
                    {
                        var firstLine = lines.EnumerateArray().FirstOrDefault();
                        if (firstLine.TryGetProperty("Rate", out var rateEl))
                        {
                            totalRate = rateEl.GetDecimal();
                        }
                    }

                    var taxCode = e.GetProperty("Code").GetString() ?? "";

                    taxes.Add(new TaxDeclaration
                    {
                        Id = taxCode.GetHashCode(), // Use a temporary hash code for the ID
                        TaxCode = taxCode,
                        TaxDescription = e.GetProperty("Name").GetString(),
                        TotalPercentage = totalRate,
                        IsActive = e.GetProperty("Inactive").GetString() == "tNO",
                       
                        CGST = !taxCode.Contains("IGST", StringComparison.OrdinalIgnoreCase) ? totalRate / 2 : null,
                        SGST = !taxCode.Contains("IGST", StringComparison.OrdinalIgnoreCase) ? totalRate / 2 : null,
                        ValidFrom = DateTime.Now, // SAP doesn't provide this on the main object
                        ValidTo = DateTime.Now
                    });
                }
                return taxes;
            }
            else // SQL
            {
                _logger.LogInformation("Fetching tax data from SQL.");
                return await _context.TaxDeclarations.OrderBy(t => t.TaxCode).ToListAsync();
            }
        }

        // POST a new tax
        public async Task<TaxDeclaration> AddAsync(TaxDeclaration tax)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                await _sapService.CreateVatGroupAsync(tax);
                // Since SAP confirms creation, we return the object we sent
                return tax;
            }
            else // SQL
            {
                _context.TaxDeclarations.Add(tax);
                await _context.SaveChangesAsync();
                return tax;
            }
        }

        public async Task UpdateAsync(int id, TaxDeclaration tax)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                // SAP B1's standard DI API and Service Layer often do not support
                // updating the primary key (Code) of a VatGroup. 
                // They typically only allow updating other properties.
                // We will assume for now we are just updating properties other than the TaxCode.
                // A full implementation would require a PATCH request.

                _logger.LogWarning("SAP VatGroup update is a complex operation, this is a placeholder implementation.");
                await _sapService.UpdateVatGroupAsync(tax.TaxCode, tax); // We need to create this method in SapService
            }
            else // SQL
            {
                var existingTax = await _context.TaxDeclarations.FindAsync(id);
                if (existingTax == null) throw new KeyNotFoundException($"Tax with ID {id} not found.");

                // Use a library like Automapper for this in a real project
                // For now, we'll do it manually:
                _context.Entry(existingTax).CurrentValues.SetValues(tax);

                await _context.SaveChangesAsync();
            }
        }

        // DELETE a tax
        public async Task DeleteByCodeAsync(string taxCode)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                // This is much simpler. We just pass the code directly to SapService.
                await _sapService.DeleteVatGroupAsync(taxCode);
            }
            else // SQL
            {
                var tax = await _context.TaxDeclarations.FirstOrDefaultAsync(t => t.TaxCode == taxCode);
                if (tax == null) throw new KeyNotFoundException($"Tax with code {taxCode} not found.");
                _context.TaxDeclarations.Remove(tax);
                await _context.SaveChangesAsync();
            }
        }
    }
}