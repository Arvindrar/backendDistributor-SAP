// Services/SalesEmployeeService.cs
using backendDistributor.Models;
using backendDistributor.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

// Define an alias for the SQL model if its name conflicts
using SqlSalesEmployee = backendDistributor.Models.SalesEmployee;

public class SalesEmployeeService
{
    private readonly CustomerDbContext _context;
    private readonly SapService _sapService;
    private readonly ILogger<SalesEmployeeService> _logger;
    private readonly string _dataSource;

    public SalesEmployeeService(CustomerDbContext context, SapService sapService, IConfiguration configuration, ILogger<SalesEmployeeService> logger)
    {
        _context = context;
        _sapService = sapService;
        _logger = logger;
        _dataSource = configuration.GetValue<string>("DataSource") ?? "SQL"; // Default to SQL
    }

    // --- GET ALL SALES EMPLOYEES ---
    // File: Services/SalesEmployeeService.cs

    public async Task<IEnumerable<SalesEmployee>> GetAllAsync()
    {
        if (_dataSource.ToUpper() == "SAP")
        {
            _logger.LogInformation("--> SalesEmployeeService is using SAP data for GET.");
            var sapJsonResult = await _sapService.GetSalesEmployeesAsync();
            using var jsonDoc = JsonDocument.Parse(sapJsonResult);

            // Check if the 'value' property exists and is an array
            if (!jsonDoc.RootElement.TryGetProperty("value", out var sapEmployeesElement) || sapEmployeesElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("SAP response did not contain a 'value' array or was invalid.");
                return Enumerable.Empty<SalesEmployee>(); // Return an empty list
            }

            var employees = new List<SalesEmployee>();

            // --- THIS IS THE CORRECTED MAPPING LOGIC ---
            foreach (var e in sapEmployeesElement.EnumerateArray())
            {
                // Create a new employee and populate it with data from the JSON element 'e'
                var employee = new SalesEmployee
                {
                    // Use helper functions to safely get properties.
                    // NOTE: These property names ('SalesEmployeeCode', 'SalesEmployeeName')
                    // are educated guesses. You may need to adjust them to match your exact SAP JSON.
                    Id = e.TryGetProperty("SalesEmployeeCode", out var idElement) ? idElement.GetInt32() : 0,
                    Code = TryGetPropertyAsString(e, "SalesEmployeeCode"),
                    Name = TryGetPropertyAsString(e, "SalesEmployeeName"),
                    ContactNumber = TryGetPropertyAsString(e, "Mobile"), // SAP often uses 'Mobile' or 'Telephone'
                    Email = TryGetPropertyAsString(e, "Email"),
                    Remarks = TryGetPropertyAsString(e, "Remarks"),
                    IsActive = TryGetPropertyAsBool(e, "Active") // SAP often uses a 'tYES'/'tNO' string for bools
                };

                // Only add employees that have a valid ID and Name
                if (employee.Id > 0 && !string.IsNullOrWhiteSpace(employee.Name))
                {
                    employees.Add(employee);
                }
            }

            return employees.OrderBy(e => e.Name);
        }
        else
        {
            _logger.LogInformation("--> SalesEmployeeService is using SQL data for GET.");
            return await _context.SalesEmployees.OrderBy(e => e.Name).ToListAsync();
        }
    }

    // --- ADD THESE HELPER FUNCTIONS INSIDE YOUR SalesEmployeeService CLASS ---
    // These make the code safer and cleaner.

    private string? TryGetPropertyAsString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null)
        {
            return property.ToString();
        }
        return null;
    }

    private bool TryGetPropertyAsBool(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.True) return true;
            if (property.ValueKind == JsonValueKind.False) return false;
            // Handle SAP's common string booleans
            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString()?.ToUpper() == "TYES";
            }
        }
        return false;
    }
    // --- GET ONE SALES EMPLOYEE BY ID ---
    // File: Services/SalesEmployeeService.cs

    // ... (your other methods like GetAllAsync are here) ...

    // --- GET ONE SALES EMPLOYEE BY ID ---
    // File: Services/SalesEmployeeService.cs

    // File: Services/SalesEmployeeService.cs

    // ... (your other using statements and class definition) ...

    // File: Services/SalesEmployeeService.cs

    public async Task<SalesEmployee?> GetByIdAsync(int id)
    {
        if (_dataSource.ToUpper() != "SAP")
        {
            return await _context.SalesEmployees.FindAsync(id);
        }

        // --- SIMPLIFIED LOGIC ---
        // We only call the simple GetSalesEmployeeByIdAsync method now.
        var sapJsonResult = await _sapService.GetSalesEmployeeByIdAsync(id);
        if (string.IsNullOrEmpty(sapJsonResult)) return null;

        using var jsonDoc = JsonDocument.Parse(sapJsonResult);
        var root = jsonDoc.RootElement;

        // Map ONLY the fields that exist.
        return new SalesEmployee
        {
            Id = (int?)root.GetProperty("SalesEmployeeCode").GetInt32() ?? 0,
            Code = root.GetProperty("SalesEmployeeCode").ToString(),
            Name = root.GetProperty("SalesEmployeeName").GetString(),
            ContactNumber = root.GetProperty("Mobile").GetString(),
            Email = root.GetProperty("Email").GetString(),
            Remarks = root.GetProperty("Remarks").GetString(),
            IsActive = (root.GetProperty("Active").GetString() ?? "tNO") == "tYES"
        };
    }

    public async Task<SalesEmployee> AddAsync(SalesEmployee employee)
    {
        if (_dataSource.ToUpper() == "SAP")
        {
            var createdEmployeeJson = await _sapService.CreateSalesEmployeeAsync(employee);
            using var jsonDoc = JsonDocument.Parse(createdEmployeeJson);
            employee.Id = jsonDoc.RootElement.GetProperty("SalesEmployeeCode").GetInt32();
            employee.Code = employee.Id.ToString(); // Also set the code from SAP
            return employee;
        }
        else
        {
            // --- THIS IS THE SQL LOGIC TO CHANGE ---

            // 1. Set the initial code to a temporary placeholder.
            //    This is because the Id is not known until after the first save.
            employee.Code = "PENDING";

            // 2. Add the employee to the context and save to generate the Id.
            _context.SalesEmployees.Add(employee);
            await _context.SaveChangesAsync();

            // 3. NOW the employee object has its new Id from the database.
            //    We set the Code to be the same as the Id.
            employee.Code = employee.Id.ToString();

            // 4. Save the changes again to update the Code in the database.
            await _context.SaveChangesAsync();

            // 5. Return the fully updated employee object.
            return employee;
        }
    }

    // --- UPDATE A SALES EMPLOYEE ---
    // In Services/SalesEmployeeService.cs

    // Services/SalesEmployeeService.cs
    // File: Services/SalesEmployeeService.cs

    public async Task UpdateAsync(int id, SalesEmployee employee)
    {
        // This 'if' statement correctly routes the logic based on your appsettings.json
        if (_dataSource.ToUpper() == "SAP")
        {
            await _sapService.UpdateSalesEmployeeAsync(id, employee);
        }
        else // This 'else' block contains the SQL logic
        {
            // --- THIS IS THE CORRECTED SQL UPDATE LOGIC ---

            // 1. Find the existing employee in the database using its ID.
            var existingEmployee = await _context.SalesEmployees.FindAsync(id);

            // 2. If the employee doesn't exist, we can't update it. Throw an error.
            if (existingEmployee == null)
            {
                throw new KeyNotFoundException($"Employee with ID {id} not found.");
            }

            // 3. Manually copy the values from the incoming 'employee' object
            //    to the 'existingEmployee' object that we just fetched from the database.
            //    This is crucial because 'existingEmployee' is being tracked by EF Core.
            existingEmployee.Name = employee.Name;
            existingEmployee.ContactNumber = employee.ContactNumber;
            existingEmployee.Email = employee.Email;
            existingEmployee.Remarks = employee.Remarks;
            existingEmployee.IsActive = employee.IsActive;
            // We do not update the 'Code' or 'Id' as they should be immutable.

            // 4. Save the changes. EF Core knows that 'existingEmployee' has been modified
            //    and will generate the correct SQL UPDATE statement.
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Successfully updated employee with ID {id} in the SQL database.");
        }
    }

    public async Task DeleteAsync(int id)
    {
        if (_dataSource.ToUpper() == "SAP")
        {
            await _sapService.DeleteSalesEmployeeAsync(id);
        }
        else
        {
            var employee = await _context.SalesEmployees.FindAsync(id);
            if (employee == null) throw new KeyNotFoundException($"Employee with ID {id} not found.");
            _context.SalesEmployees.Remove(employee);
            await _context.SaveChangesAsync();
        }
    }
}