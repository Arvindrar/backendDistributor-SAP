// File: Services/VendorGroupService.cs
using backendDistributor.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace backendDistributor.Services
{
    public class VendorGroupService
    {
        private readonly SapService _sapService;
        private readonly ILogger<VendorGroupService> _logger;
        private readonly string _dataSource;
        private readonly CustomerDbContext _context;

        public VendorGroupService(SapService sapService, IConfiguration configuration, CustomerDbContext context, ILogger<VendorGroupService> logger)
        {
            _sapService = sapService;
            _logger = logger;
            _dataSource = configuration.GetValue<string>("DataSource") ?? "SQL";
            _context = context;
        }

        public async Task<string> GetAllAsync()
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("Fetching Vendor Groups from SAP.");
                var sapJson = await _sapService.GetVendorGroupsAsync();

                using (var doc = JsonDocument.Parse(sapJson))
                {
                    if (doc.RootElement.TryGetProperty("value", out var value))
                    {
                        return value.ToString() ?? "[]";
                    }
                }
                return "[]";
            }
            else
            {
                _logger.LogInformation("Fetching Vendor Groups from SQL.");
                var groups = await _context.VendorGroups.ToListAsync();
                return JsonSerializer.Serialize(groups);
            }
        }

        // --- NEW METHOD TO CREATE A VENDOR GROUP ---
        public async Task<string> AddAsync(JsonNode groupData)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("Creating Vendor Group in SAP.");
                // The frontend sends {"name":"..."}, SAP expects {"Name":"...", "Type":"..."}
                // We'll transform it here.
                string? groupName = groupData["name"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    throw new ArgumentException("Group name cannot be empty.");
                }

                // Call the existing method in SapService, but specify the correct type for vendors
                return await _sapService.CreateBusinessPartnerGroupAsync(
                    new CustomerGroup { Name = groupName }, "bbpgt_VendorGroup"
                );
            }
            else
            {
                var group = new VendorGroup { Name = groupData["name"]!.GetValue<string>() };
                _context.VendorGroups.Add(group);
                await _context.SaveChangesAsync();
                return JsonSerializer.Serialize(group);
            }
        }

        // --- NEW METHOD TO DELETE A VENDOR GROUP ---
        public async Task DeleteAsync(int groupId)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("Deleting Vendor Group {groupId} from SAP.", groupId);
                await _sapService.DeleteBusinessPartnerGroupAsync(groupId);
            }
            else
            {
                var group = await _context.VendorGroups.FindAsync(groupId);
                if (group != null)
                {
                    _context.VendorGroups.Remove(group);
                    await _context.SaveChangesAsync();
                }
            }
        }
    }
}