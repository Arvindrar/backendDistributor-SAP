// File: Services/WarehouseService.cs
using backendDistributor.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace backendDistributor.Services
{
    public class WarehouseService
    {
        private readonly CustomerDbContext _context;
        private readonly SapService _sapService;
        private readonly ILogger<WarehouseService> _logger;
        private readonly string _dataSource;

        public WarehouseService(CustomerDbContext context, SapService sapService, IConfiguration configuration, ILogger<WarehouseService> logger)
        {
            _context = context;
            _sapService = sapService;
            _logger = logger;
            _dataSource = configuration.GetValue<string>("DataSource") ?? "SQL";
        }

        public async Task<IEnumerable<Warehouse>> GetAllAsync()
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("--> WarehouseService is using SAP data for GET.");
                var sapJsonResult = await _sapService.GetWarehousesAsync();
                using var jsonDoc = JsonDocument.Parse(sapJsonResult);

                if (!jsonDoc.RootElement.TryGetProperty("value", out var sapWarehousesElement))
                {
                    return Enumerable.Empty<Warehouse>();
                }

                // Map the SAP JSON to our Warehouse model
                return sapWarehousesElement.EnumerateArray().Select(wh => new Warehouse
                {
                    Id = 0, // SQL ID is not applicable here
                    Code = wh.TryGetProperty("WarehouseCode", out var code) ? code.GetString() : "",
                    Name = wh.TryGetProperty("WarehouseName", out var name) ? name.GetString() : "",
                    Address = wh.TryGetProperty("Street", out var street) ? street.GetString() : ""
                }).ToList();
            }
            else
            {
                _logger.LogInformation("--> WarehouseService is using SQL data for GET.");
                return await _context.Warehouses.OrderBy(w => w.Name).ToListAsync();
            }
        }

        public async Task<Warehouse> AddAsync(Warehouse warehouse)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                var createdWarehouseJson = await _sapService.CreateWarehouseAsync(warehouse);
                // We can parse the response if we need to return the full object from SAP
                return warehouse; // For simplicity, we return the object we sent.
            }
            else
            {
                _context.Warehouses.Add(warehouse);
                await _context.SaveChangesAsync();
                return warehouse;
            }
        }

        public async Task DeleteAsync(string id)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                // For SAP, the 'id' we receive from the frontend is the Warehouse Code
                await _sapService.DeleteWarehouseAsync(id);
            }
            else
            {
                // For SQL, the 'id' is a number, so we need to parse it.
                if (!int.TryParse(id, out int warehouseId))
                {
                    throw new ArgumentException("Invalid ID format for SQL data source.");
                }

                var warehouse = await _context.Warehouses.FindAsync(warehouseId);
                if (warehouse == null) throw new KeyNotFoundException($"Warehouse with ID {warehouseId} not found.");

                _context.Warehouses.Remove(warehouse);
                await _context.SaveChangesAsync();
            }
        }
    }
}