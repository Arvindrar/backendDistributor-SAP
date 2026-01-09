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
            var sapJsonResult = await _sapService.GetSalesEmployeesAsync();
            using var jsonDoc = JsonDocument.Parse(sapJsonResult);

            if (jsonDoc.RootElement.TryGetProperty("value", out var sapEmployeesElement))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var employees = sapEmployeesElement.Deserialize<List<SalesEmployee>>(options);
                _logger.LogInformation("SAP EMPLOYEES COUNT: {Count}", employees.Count);

                foreach (var emp in employees)
                {
                    _logger.LogInformation(
                        "EMP => Id: {Id}, Name: {Name}, ActiveSap: {Active}",
                        emp.Id,
                        emp.Name,
                        emp.ActiveSap
                    );
                }

                if (employees != null)
                {
                    foreach (var emp in employees)
                    {
                        emp.Code = emp.Id.ToString();
                        emp.IsActive = emp.ActiveSap?.Equals("tYES", StringComparison.OrdinalIgnoreCase) ?? false;

                    }
                    return employees;
                }
            }
            return Enumerable.Empty<SalesEmployee>();
        }
        else
        {
            return await _context.SalesEmployees.OrderBy(e => e.Name).ToListAsync();
        }
    }

    public async Task<SalesEmployee> AddAsync(SalesEmployee employee)
    {
        if (_dataSource.ToUpper() == "SAP")
        {
            var createdEmployeeJson = await _sapService.CreateSalesEmployeeAsync(employee);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var newEmployee = JsonSerializer.Deserialize<SalesEmployee>(createdEmployeeJson, options);

            if (newEmployee == null)
            {
                throw new Exception("Failed to deserialize the SAP response after creating an employee.");
            }
            newEmployee.Code = newEmployee.Id.ToString();
            return newEmployee;
        }
        else
        {
            // ... (SQL logic is fine)
            return employee; // Placeholder
        }
    }

    private string? TryGetPropertyAsString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null)
        {
            // --- THIS IS THE FIX ---
            // Check the type of the JSON property.
            if (property.ValueKind == JsonValueKind.Number)
            {
                // If it's a number, convert it to a string.
                return property.GetInt32().ToString();
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                // If it's already a string, return it directly.
                return property.GetString();
            }
            // For any other type, just use its string representation.
            return property.ToString();
            // --- END OF FIX ---
        }
        return null;
    }

    // This helper function was already correct
    private bool TryGetPropertyAsBool(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.True) return true;
            if (property.ValueKind == JsonValueKind.False) return false;
            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString()?.ToUpper() == "TYES";
            }
        }
        return false;
    }
    // ... (your other methods like GetAllAsync are here) ...

    // --- GET ONE SALES EMPLOYEE BY ID ---
    // File: Services/SalesEmployeeService.cs

    // File: Services/SalesEmployeeService.cs

    // ... (your other using statements and class definition) ...

    // File: Services/SalesEmployeeService.cs

    public async Task<SalesEmployee?> GetByIdAsync(int id)
    {
        // THE FIX: Use the `_dataSource` field, not `_configuration`.
        if (_dataSource.ToUpper() == "SAP")
        {
            _logger.LogInformation("--> SalesEmployeeService is using SAP data for GET by ID.");
            var jsonNode = await _sapService.GetCombinedSalesEmployeeDetailsAsync(id);
            if (jsonNode == null) return null;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<SalesEmployee>(jsonNode.ToString(), options);
        }
        else
        {
            _logger.LogInformation("--> SalesEmployeeService is using SQL data for GET by ID.");
            return await _context.SalesEmployees.FindAsync(id);
        }
    }

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
        // The `if` statement now correctly references the `_dataSource` field
        // that was initialized in the constructor.
        if (_dataSource.ToUpper() == "SAP")
        {
            await _sapService.DeleteSalesEmployeeAsync(id);
        }
        else
        {
            var employee = await _context.SalesEmployees.FindAsync(id);
            if (employee == null)
            {
                throw new KeyNotFoundException($"Employee with ID {id} not found.");
            }
            _context.SalesEmployees.Remove(employee);
            await _context.SaveChangesAsync();
        }
    }
}