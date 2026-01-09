// File: Services/VendorService.cs
using backendDistributor.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace backendDistributor.Services
{
    public class VendorService
    {
        private readonly CustomerDbContext _context;
        private readonly SapService _sapService;
        private readonly ILogger<VendorService> _logger;
        private readonly string _dataSource;

        public VendorService(CustomerDbContext context, SapService sapService, IConfiguration configuration, ILogger<VendorService> logger)
        {
            _context = context;
            _sapService = sapService;
            _logger = logger;
            _dataSource = configuration.GetValue<string>("DataSource") ?? "SQL";
        }

        public async Task<JsonElement?> GetByCardCodeAsync(string cardCode)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("--> VendorService is fetching single vendor from SAP by CardCode.");
                var vendorJson = await _sapService.GetCustomerByIdAsync(cardCode); // Can reuse the same SAP method

                if (string.IsNullOrEmpty(vendorJson)) return null;

                return JsonDocument.Parse(vendorJson).RootElement;
            }
            else
            {
                _logger.LogInformation("--> VendorService is fetching single vendor from SQL by CardCode.");
                // Note: SQL logic for vendors is not fully implemented in your provided code
                // This is a placeholder for how it would work.
                var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Code == cardCode);
                if (vendor == null) return null;

                var vendorJson = JsonSerializer.Serialize(vendor);
                return JsonDocument.Parse(vendorJson).RootElement;
            }
        }
        public async Task<JsonElement> AddAsync(JsonElement vendorData)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("--> VendorService is creating vendor in SAP.");
                var resultJson = await _sapService.CreateVendorAsync(vendorData);
                return JsonDocument.Parse(resultJson).RootElement;
            }
            else
            {
                _logger.LogInformation("--> VendorService is creating vendor in SQL.");
                var vendor = JsonSerializer.Deserialize<Vendor>(vendorData.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (vendor == null)
                {
                    throw new ArgumentException("Invalid vendor data provided.");
                }
                _context.Vendors.Add(vendor);
                await _context.SaveChangesAsync();
                var createdJson = JsonSerializer.Serialize(vendor);
                return JsonDocument.Parse(createdJson).RootElement;
            }
        }

        public async Task UpdateAsync(string cardCode, JsonElement vendorData)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("--> VendorService is updating vendor {cardCode} in SAP.", cardCode);
                await _sapService.UpdateVendorAsync(cardCode, vendorData);
            }
            else
            {
                // SQL update logic would go here. For now, we focus on SAP.
                _logger.LogWarning("SQL update for vendors not fully implemented yet.");
                // Example:
                // var vendor = JsonSerializer.Deserialize<Vendor>(...);
                // _context.Entry(vendor).State = EntityState.Modified;
                // await _context.SaveChangesAsync();
            }
        }

        public async Task<string> GetAllAsync(string? group, string? searchTerm, int pageNumber, int pageSize)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("--> VendorService is fetching vendors from SAP.");
                // Call the new method in SapService
                return await _sapService.GetVendorsAsync(group, searchTerm, pageNumber, pageSize);
            }
            else
            {
                _logger.LogInformation("--> VendorService is fetching vendors from SQL.");
                var query = _context.Vendors.AsQueryable();

                if (!string.IsNullOrEmpty(group))
                {
                    query = query.Where(v => v.Group == group);
                }

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(v => (v.Name != null && v.Name.Contains(searchTerm)) || (v.Code != null && v.Code.Contains(searchTerm)));
                }

                var totalRecords = await query.CountAsync();
                var vendors = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                var result = new
                {
                    // Match the OData structure the frontend expects
                    odata_count = totalRecords,
                    value = vendors
                };

                return JsonSerializer.Serialize(result);
            }
        }
    }
}