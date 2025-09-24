// Services/CustomerGroupService.cs

using backendDistributor.Models;
using backendDistributor.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public class CustomerGroupService
{
    private readonly CustomerDbContext _context;
    private readonly SapService _sapService;
    private readonly ILogger<CustomerGroupService> _logger;
    private readonly string _dataSource;

    public CustomerGroupService(CustomerDbContext context, SapService sapService, IConfiguration configuration, ILogger<CustomerGroupService> logger)
    {
        _context = context;
        _sapService = sapService;
        _logger = logger;
        _dataSource = configuration.GetValue<string>("DataSource");
    }

    // --- GET (READ) ---
    public async Task<IEnumerable<CustomerGroup>> GetAllGroupsAsync()
    {
        if (_dataSource?.ToUpper() == "SAP")
        {
            _logger.LogInformation("--> CustomerGroupService is using SAP data for GET.");
            // ... (The SAP GET logic remains the same) ...
            var sapJsonResult = await _sapService.GetBusinessPartnerGroupsAsync();
            using var jsonDoc = JsonDocument.Parse(sapJsonResult);
            if (!jsonDoc.RootElement.TryGetProperty("value", out var sapGroupElements)) { return new List<CustomerGroup>(); }
            var customerGroups = new List<CustomerGroup>();
            foreach (var element in sapGroupElements.EnumerateArray())
            {
                if (element.TryGetProperty("Type", out var typeElement) && typeElement.GetString() == "bbpgt_CustomerGroup")
                {
                    customerGroups.Add(new CustomerGroup
                    {
                        Id = element.GetProperty("Code").GetInt32(),
                        Name = element.GetProperty("Name").GetString() ?? "Unnamed Group"
                    });
                }
            }
            return customerGroups.OrderBy(g => g.Name);
        }
        else
        {
            _logger.LogInformation("--> CustomerGroupService is using SQL data for GET.");
            return await _context.CustomerGroups.OrderBy(cg => cg.Name).ToListAsync();
        }
    }

    // --- POST (CREATE) ---
    // --- POST (CREATE) ---
    public async Task<CustomerGroup> AddGroupAsync(CustomerGroup group)
    {
        if (_dataSource?.ToUpper() == "SAP")
        {
            _logger.LogInformation("--> CustomerGroupService is using SAP data for POST.");

            // === REPLACE THE OLD CODE WITH THIS NEW CODE ===
            var sapResponseJson = await _sapService.CreateBusinessPartnerGroupAsync(group);

            // Parse the response from SAP to get the new ID
            using var jsonDoc = JsonDocument.Parse(sapResponseJson);
            var newSapGroup = jsonDoc.RootElement;

            // Update the group object with the new ID created by SAP
            group.Id = newSapGroup.GetProperty("Code").GetInt32();

            return group;
            // ===============================================
        }
        else
        {
            _logger.LogInformation("--> CustomerGroupService is using SQL data for POST.");
            // (The SQL logic remains the same)
            if (await _context.CustomerGroups.AnyAsync(cg => cg.Name.ToLower() == group.Name.ToLower()))
            {
                throw new InvalidOperationException($"A customer group with the name '{group.Name}' already exists.");
            }
            _context.CustomerGroups.Add(group);
            await _context.SaveChangesAsync();
            return group;
        }
    }

    // --- PUT (UPDATE) ---
    public async Task UpdateGroupAsync(int id, CustomerGroup group)
    {
        if (id != group.Id)
        {
            throw new ArgumentException("ID in URL does not match ID in request body.");
        }

        if (_dataSource?.ToUpper() == "SAP")
        {
            _logger.LogWarning("--> SAP 'Update Customer Group' is NOT IMPLEMENTED. Action blocked.");
            // In a real scenario, you would call _sapService.UpdateBusinessPartnerGroupAsync(id, group);
            throw new NotSupportedException("Updating Customer Groups directly in SAP is not supported by this application.");
        }
        else
        {
            _logger.LogInformation("--> CustomerGroupService is using SQL data for PUT.");
            // Check if the new name conflicts with an existing group
            if (await _context.CustomerGroups.AnyAsync(cg => cg.Name.ToLower() == group.Name.ToLower() && cg.Id != id))
            {
                throw new InvalidOperationException($"Another customer group with the name '{group.Name}' already exists.");
            }
            _context.Entry(group).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }
    }


    // --- DELETE ---
    // --- DELETE ---
    public async Task DeleteGroupAsync(int id)
    {
        if (_dataSource?.ToUpper() == "SAP")
        {
            _logger.LogInformation("--> CustomerGroupService is using SAP data for DELETE.");

            // === REPLACE THE OLD CODE WITH THIS NEW CODE ===
            await _sapService.DeleteBusinessPartnerGroupAsync(id);
            // ===============================================
        }
        else
        {
            _logger.LogInformation("--> CustomerGroupService is using SQL data for DELETE.");
            // (The SQL logic remains the same)
            var group = await _context.CustomerGroups.FindAsync(id);
            if (group == null)
            {
                throw new KeyNotFoundException($"Customer group with ID {id} not found.");
            }
            _context.CustomerGroups.Remove(group);
            await _context.SaveChangesAsync();
        }
    }
}