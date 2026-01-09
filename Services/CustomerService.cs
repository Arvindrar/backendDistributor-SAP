// File: Services/CustomerService.cs
using backendDistributor.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace backendDistributor.Services
{
    public class CustomerService
    {
        private readonly CustomerDbContext _context;
        private readonly SapService _sapService;
        private readonly ILogger<CustomerService> _logger;
        private readonly string _dataSource;

        public CustomerService(CustomerDbContext context, SapService sapService, IConfiguration configuration, ILogger<CustomerService> logger)
        {
            _context = context;
            _sapService = sapService;
            _logger = logger;
            _dataSource = configuration.GetValue<string>("DataSource") ?? "SQL";
        }

        public async Task<string> GetAllAsync(string? group, string? searchTerm, int pageNumber, int pageSize)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("--> CustomerService is fetching customers from SAP.");
                return await _sapService.GetCustomersAsync(group, searchTerm, pageNumber, pageSize);
            }
            else
            {
                _logger.LogInformation("--> CustomerService is fetching customers from SQL.");
                var query = _context.Customers.Include(c => c.BPAddresses).AsQueryable();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(c => c.CardName.Contains(searchTerm) || c.CardCode.Contains(searchTerm));
                }

                // Note: SQL pagination and filtering would be more complex to fully replicate SAP.
                // This is a basic implementation.
                var totalRecords = await query.CountAsync();
                var customers = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                var result = new
                {
                   
                    value = customers
                };

                return JsonSerializer.Serialize(result);

            }
        }

        public async Task<JsonElement?> GetByCardCodeAsync(string cardCode)
        {
            // THE FIX: Use the `_dataSource` field that exists in this class.
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("--> CustomerService is fetching single customer from SAP by CardCode.");
                var customerJson = await _sapService.GetCustomerByIdAsync(cardCode);

                if (string.IsNullOrEmpty(customerJson)) return null;

                // Return as JsonElement to match the AddAsync return type
                return JsonDocument.Parse(customerJson).RootElement;
            }
            else
            {
                _logger.LogInformation("--> CustomerService is fetching single customer from SQL by CardCode.");
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CardCode == cardCode);
                if (customer == null) return null;

                var customerJson = JsonSerializer.Serialize(customer);
                return JsonDocument.Parse(customerJson).RootElement;
            }
        }

        public async Task<JsonElement> AddAsync(JsonElement customerData)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("--> CustomerService is creating customer in SAP.");
                var resultJson = await _sapService.CreateCustomerAsync(customerData);
                return JsonDocument.Parse(resultJson).RootElement;
            }
            else
            {
                _logger.LogInformation("--> CustomerService is creating customer in SQL.");

                // Deserialize the incoming JSON to our EF models
                var customer = JsonSerializer.Deserialize<Customer>(customerData.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (customer == null)
                {
                    throw new ArgumentException("Invalid customer data provided.");
                }

                // Add to context and save
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                // Return the created customer, serialized back to JsonElement
                var createdJson = JsonSerializer.Serialize(customer);
                return JsonDocument.Parse(createdJson).RootElement;
            }
        }
    }
}