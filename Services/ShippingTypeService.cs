// Services/ShippingTypeService.cs
using backendDistributor.Models;
using backendDistributor.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

public class ShippingTypeService
{
    private readonly CustomerDbContext _context;
    private readonly SapService _sapService;
    private readonly ILogger<ShippingTypeService> _logger;
    private readonly string _dataSource;

    public ShippingTypeService(CustomerDbContext context, SapService sapService, IConfiguration configuration, ILogger<ShippingTypeService> logger)
    {
        _context = context;
        _sapService = sapService;
        _logger = logger;
        _dataSource = configuration.GetValue<string>("DataSource");
    }

    // --- GET ALL SHIPPING TYPES ---
    public async Task<IEnumerable<ShippingType>> GetAllAsync()
    {
        if (_dataSource?.ToUpper() == "SAP")
        {
            _logger.LogInformation("--> ShippingTypeService is using SAP data for GET.");
            var sapJsonResult = await _sapService.GetShippingTypesAsync();

            using var jsonDoc = JsonDocument.Parse(sapJsonResult);
            if (!jsonDoc.RootElement.TryGetProperty("value", out var sapElements))
            {
                return new List<ShippingType>();
            }

            // Map the SAP response (Code, Name) to our standard ShippingType model (Id, Name)
            return sapElements.EnumerateArray()
                .Select(st => new ShippingType
                {
                    Id = st.GetProperty("Code").GetInt32(),
                    Name = st.GetProperty("Name").GetString() ?? ""
                })
                .OrderBy(st => st.Name)
                .ToList();
        }
        else
        {
            _logger.LogInformation("--> ShippingTypeService is using SQL data for GET.");
            return await _context.ShippingTypes.OrderBy(st => st.Name).ToListAsync();
        }
    }

    // --- ADD A SHIPPING TYPE ---
    public async Task DeleteAsync(int id)
    {
        if (_dataSource?.ToUpper() == "SAP")
        {
            _logger.LogInformation("--> ShippingTypeService is using SAP data for DELETE.");
            await _sapService.DeleteShippingTypeAsync(id);
        }
        else
        {
            _logger.LogInformation("--> ShippingTypeService is using SQL data for DELETE.");
            var sqlShippingType = await _context.ShippingTypes.FindAsync(id);

            if (sqlShippingType == null)
            {
                // This exception will be caught by the controller and turned into a 404 Not Found.
                throw new KeyNotFoundException($"Shipping Type with ID {id} not found in SQL database.");
            }

            _context.ShippingTypes.Remove(sqlShippingType);
            await _context.SaveChangesAsync();
        }
    }
    public async Task<ShippingType> AddAsync(ShippingType shippingType)
    {
        if (_dataSource?.ToUpper() == "SAP")
        {
            _logger.LogInformation("--> ShippingTypeService is using SAP data for POST.");
            // The frontend sends a simple object, which we can wrap in a JsonElement
            var payload = new { name = shippingType.Name };
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(payload));
            var data = doc.RootElement.Clone();

            var createdSapJson = await _sapService.CreateShippingTypeAsync(data);

            using var jsonDoc = JsonDocument.Parse(createdSapJson);
            // Map the response from SAP back to our model
            return new ShippingType
            {
                Id = jsonDoc.RootElement.GetProperty("Code").GetInt32(),
                Name = jsonDoc.RootElement.GetProperty("Name").GetString() ?? ""
            };
        }
        else
        {
            _logger.LogInformation("--> ShippingTypeService is using SQL data for POST.");
            // Check for duplicates in SQL
            var existing = await _context.ShippingTypes.FirstOrDefaultAsync(st => st.Name.ToLower() == shippingType.Name.ToLower());
            if (existing != null)
            {
                throw new InvalidOperationException("A Shipping Type with this name already exists in the SQL database.");
            }

            _context.ShippingTypes.Add(shippingType);
            await _context.SaveChangesAsync();
            return shippingType; // The ID is now populated by the database
        }
    }
}