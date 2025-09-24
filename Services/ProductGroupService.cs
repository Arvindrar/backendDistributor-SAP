// FILE: backendDistributor/Services/ProductGroupService.cs

using backendDistributor.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace backendDistributor.Services
{
    public class ProductGroupService
    {
        private readonly CustomerDbContext _context;
        private readonly SapService _sapService;
        private readonly ILogger<ProductGroupService> _logger;
        private readonly string _dataSource;

        public ProductGroupService(CustomerDbContext context, SapService sapService, IConfiguration configuration, ILogger<ProductGroupService> logger)
        {
            _context = context;
            _sapService = sapService;
            _logger = logger;
            _dataSource = configuration.GetValue<string>("DataSource") ?? "SQL";
        }

        public async Task<IEnumerable<ProductGroup>> GetAllAsync()
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("Fetching Product Group data from SAP.");
                var sapResponseJson = await _sapService.GetProductGroupsAsync();
                using var jsonDoc = JsonDocument.Parse(sapResponseJson);

                if (!jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
                {
                    return new List<ProductGroup>();
                }

                // Map the SAP response (Number, GroupName) to our local ProductGroup model (Id, Name)
                return valueElement.EnumerateArray().Select(g => new ProductGroup
                {
                    Id = g.GetProperty("Number").GetInt32(),
                    Name = g.GetProperty("GroupName").GetString() ?? "Unnamed"
                }).ToList();
            }

            // SQL path
            _logger.LogInformation("Fetching Product Group data from SQL.");
            return await _context.ProductGroups.OrderBy(pg => pg.Name).ToListAsync();
        }

        public async Task<ProductGroup> AddAsync(ProductGroup group)
        {
            if (string.IsNullOrWhiteSpace(group.Name))
            {
                throw new ArgumentException("Product group name cannot be empty.");
            }
            group.Name = group.Name.Trim();

            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("Creating new Product Group in SAP with name: {Name}", group.Name);
                var sapResponseJson = await _sapService.CreateProductGroupAsync(group);
                using var jsonDoc = JsonDocument.Parse(sapResponseJson);
                group.Id = jsonDoc.RootElement.GetProperty("Number").GetInt32();
                return group;
            }

            // SQL path
            _logger.LogInformation("Creating new Product Group in SQL with name: {Name}", group.Name);
            if (await _context.ProductGroups.AnyAsync(pg => pg.Name.ToLower() == group.Name.ToLower()))
            {
                throw new InvalidOperationException($"A product group with the name '{group.Name}' already exists.");
            }
            _context.ProductGroups.Add(group);
            await _context.SaveChangesAsync();
            return group;
        }

        public async Task DeleteAsync(int id)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("Deleting Product Group with ID {Id} from SAP.", id);
                await _sapService.DeleteProductGroupAsync(id);
            }
            else // SQL Path
            {
                _logger.LogInformation("Deleting Product Group with ID {Id} from SQL.", id);
                var group = await _context.ProductGroups.FindAsync(id);
                if (group != null)
                {
                    _context.ProductGroups.Remove(group);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    throw new KeyNotFoundException($"Product Group with ID {id} not found in SQL database.");
                }
            }
        }
    }
}