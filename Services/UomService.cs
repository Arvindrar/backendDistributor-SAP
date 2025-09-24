// FILE: backendDistributor/Services/UomService.cs
using backendDistributor.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace backendDistributor.Services
{
    public class UomService
    {
        private readonly CustomerDbContext _context;
        private readonly SapService _sapService;
        private readonly ILogger<UomService> _logger;
        private readonly string _dataSource;

        public UomService(CustomerDbContext context, SapService sapService, IConfiguration configuration, ILogger<UomService> logger)
        {
            _context = context;
            _sapService = sapService;
            _logger = logger;
            _dataSource = configuration.GetValue<string>("DataSource") ?? "SQL";
        }

        public async Task<IEnumerable<UOM>> GetAllAsync()
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("Fetching UOM data from SAP.");
                var sapUoms = await _sapService.GetUomsAsync();

                // Map the SAP response to our local UOM model
                return sapUoms.Select(sapUom => new UOM
                {
                    Id = sapUom.AbsEntry,
                    Name = sapUom.Name
                }).ToList();
            }
            // SQL path
            _logger.LogInformation("Fetching UOM data from SQL.");
            return await _context.UOMs.OrderBy(u => u.Name).ToListAsync();
        }

        public async Task<UOM> AddAsync(UOM uom)
        {
            if (string.IsNullOrWhiteSpace(uom.Name))
            {
                throw new ArgumentException("UOM name cannot be empty.");
            }
            uom.Name = uom.Name.Trim();

            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("Creating new UOM in SAP with name: {Name}", uom.Name);
                var sapResponseJson = await _sapService.CreateUomAsync(uom);
                using var jsonDoc = JsonDocument.Parse(sapResponseJson);
                uom.Id = jsonDoc.RootElement.GetProperty("AbsEntry").GetInt32();
                return uom;
            }

            // SQL path
            _logger.LogInformation("Creating new UOM in SQL with name: {Name}", uom.Name);
            bool nameExists = await _context.UOMs.AnyAsync(x => x.Name.ToLower() == uom.Name.ToLower());
            if (nameExists)
            {
                throw new InvalidOperationException($"UOM with name '{uom.Name}' already exists.");
            }
            _context.UOMs.Add(uom);
            await _context.SaveChangesAsync();
            return uom;
        }

        public async Task DeleteAsync(int id)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("Deleting UOM with ID {Id} from SAP.", id);
                await _sapService.DeleteUomAsync(id);
            }
            else // SQL Path
            {
                _logger.LogInformation("Deleting UOM with ID {Id} from SQL.", id);
                var uom = await _context.UOMs.FindAsync(id);
                if (uom != null)
                {
                    _context.UOMs.Remove(uom);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    throw new KeyNotFoundException($"UOM with ID {id} not found in SQL database.");
                }
            }
        }
    }
}