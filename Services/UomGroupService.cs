// Create a new file: Services/UomGroupService.cs
using backendDistributor.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using backendDistributor.Models.DTOs;

namespace backendDistributor.Services
{
    public class UomGroupService
    {
        private readonly CustomerDbContext _context;
        private readonly SapService _sapService;
        private readonly ILogger<UomGroupService> _logger;
        private readonly string _dataSource;

        public UomGroupService(CustomerDbContext context, SapService sapService, IConfiguration configuration, ILogger<UomGroupService> logger)
        {
            _context = context;
            _sapService = sapService;
            _logger = logger;
            // This is the switch that reads from appsettings.json
            _dataSource = configuration.GetValue<string>("DataSource") ?? "SQL";
        }

        // --- GET all UOM Groups ---
        // In Services/UomGroupService.cs
        public async Task<IEnumerable<UOMGroup>> GetAllAsync()
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("Fetching UOM Group data from SAP.");

                var sapUomGroups = await _sapService.GetUomGroupsAsync();

                var uomGroups = sapUomGroups.Select(sapGroup => new UOMGroup
                {
                    Id = sapGroup.AbsEntry,
                    Code = sapGroup.Code ?? string.Empty,
                    Name = sapGroup.Name ?? "Unnamed",
                    Description = sapGroup.Name ?? string.Empty
                }).ToList();

                return uomGroups;
            }
            else if (_dataSource.ToUpper() == "SQL")
            {
                _logger.LogInformation("Fetching UOM Group data from SQL.");
                return await _context.UOMGroups.OrderBy(ug => ug.Name).ToListAsync();
            }
            else
            {
                var errorMessage = $"The configured DataSource '{_dataSource}' is not supported. Please use 'SAP' or 'SQL'.";
                _logger.LogError(errorMessage);
                throw new NotSupportedException(errorMessage);
            }
        }

        public async Task<UOMGroup> AddAsync(UOMGroup group)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                // Step 1: Create the Base Unit of Measure (UoM) first.
                _logger.LogInformation("Step 1: Creating the base Unit of Measure: {UoMCode}", group.Code);
                var baseUomPayload = new { Name = group.Code, Code = group.Code }; // Use the Code property
                var createdUom = await _sapService.CreateUnitOfMeasureAsync(baseUomPayload);
                int newUomId = createdUom.AbsEntry;
                _logger.LogInformation("Step 1 Successful. Created Base UoM with ID: {UoMId}", newUomId);

                // Step 2: Now, create the UOM Group
                _logger.LogInformation("Step 2: Creating the UOM Group '{GroupName}' with Base UoM ID {UoMId}", group.Name, newUomId);
                var uomGroupPayload = new SapUomGroupCreateDto
                {
                    Code = group.Code,
                    Name = group.Name, // Use the Name property for the description/name
                    BaseUoM = newUomId,
                    UoMGroupDefinitionCollection = new UomGroupDefinitionDto[]
                    {
                new UomGroupDefinitionDto
                {
                    AlternateUoM = newUomId,
                    AlternateQuantity = 1.0,
                    BaseQuantity = 1.0
                }
                    }
                };

                var sapResponseJson = await _sapService.CreateUomGroupAsync(uomGroupPayload);
                using var jsonDoc = JsonDocument.Parse(sapResponseJson);
                group.Id = jsonDoc.RootElement.GetProperty("AbsEntry").GetInt32();
                return group; // This path returns a value.
            }
            else if (_dataSource.ToUpper() == "SQL")
            {
                _logger.LogInformation("Creating a new UOM Group in SQL.");
                _context.UOMGroups.Add(group);
                await _context.SaveChangesAsync();
                return group; // This path also returns a value.
            }
            else
            {
                // This 'else' block ensures all other paths are handled, fixing the error.
                var errorMessage = $"The configured DataSource '{_dataSource}' is not supported. Please use 'SAP' or 'SQL'.";
                _logger.LogError(errorMessage);
                throw new NotSupportedException(errorMessage);
            }
        }

        public async Task DeleteAsync(int id)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("Deleting UOM Group with ID {Id} from SAP.", id);
                await _sapService.DeleteUomGroupAsync(id);
            }
            else // SQL
            {
                _logger.LogInformation("Deleting UOM Group with ID {Id} from SQL.", id);
                var group = await _context.UOMGroups.FindAsync(id);
                if (group != null)
                {
                    _context.UOMGroups.Remove(group);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    throw new KeyNotFoundException($"UOM Group with ID {id} not found in SQL database.");
                }
            }
        }
    }
}