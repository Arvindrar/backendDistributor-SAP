// FILE: Services/SalesOrderService.cs
using backendDistributor.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Globalization;

namespace backendDistributor.Services
{
    public class SalesOrderService
    {
        private readonly CustomerDbContext _context;
        private readonly SapService _sapService;
        private readonly ILogger<SalesOrderService> _logger;
        private readonly string _dataSource;

        public SalesOrderService(CustomerDbContext context, SapService sapService, IConfiguration configuration, ILogger<SalesOrderService> logger)
        {
            _context = context;
            _sapService = sapService;
            _logger = logger;
            _dataSource = configuration.GetValue<string>("DataSource") ?? "SQL";
        }

        // --- START HIGHLIGHTED CODE: THE CREATE METHOD ---
        // This method contains the switching logic.
        public async Task<string> GetAllAsync()
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("--> SalesOrderService: Getting all Sales Orders from SAP.");
                return await _sapService.GetSalesOrdersAsync();
            }
            else // SQL Logic
            {
                _logger.LogInformation("--> SalesOrderService: Getting all Sales Orders from SQL.");

                // Fetch orders and include their line items to calculate the total
                var orders = await _context.SalesOrders
                    .Include(o => o.SalesItems)
                    .OrderByDescending(o => o.SODate)
                    .ToListAsync();

                // Create a simplified DTO (Data Transfer Object) for the list view
                var orderDtos = orders.Select(order => new
                {
                    DocEntry = order.Id, // Use the SQL Guid ID as the key
                    DocNum = order.SalesOrderNo,
                    DocDate = order.SODate,
                    CardCode = order.CustomerCode,
                    CardName = order.CustomerName,
                    Comments = order.SalesRemarks,
                    DocTotal = (order.SalesItems != null) ? order.SalesItems.Sum(i => i.Total) : 0
                });

                // Wrap the result in the { "value": [...] } format for consistency with SAP
                var result = new { value = orderDtos };
                return JsonSerializer.Serialize(result);
            }
        }

        public async Task<string> GetByIdAsync(int docEntry)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("--> SalesOrderService: Getting Sales Order {docEntry} from SAP.", docEntry);
                return await _sapService.GetSalesOrderByIdAsync(docEntry);
            }
            else // SQL Logic
            {
                _logger.LogInformation("--> SalesOrderService: Getting Sales Order {docEntry} from SQL.", docEntry);
                // In SQL mode, the ID is a Guid, not an int. We need a different method.
                // This method will not be hit by the current controller setup, but is here for completeness.
                throw new NotImplementedException("GetById for SQL requires a Guid, not an int.");
            }
        }

        public async Task<object> CreateAsync(JsonElement payload)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("--> SalesOrderService: Creating Sales Order in SAP.");
                var resultJson = await _sapService.CreateSalesOrderAsync(payload);
                return JsonDocument.Parse(resultJson).RootElement;
            }
            else // This is the new SQL logic
            {
                _logger.LogInformation("--> SalesOrderService: Creating Sales Order in SQL Database.");

                // 1. Deserialize the JSON payload from the frontend into our EF Core models.
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var salesOrder = new SalesOrder
                {
                    Id = Guid.NewGuid(),
                    CustomerCode = payload.GetProperty("CardCode").GetString(),
                    CustomerName = payload.GetProperty("CardName").GetString(), // Assuming CardName is sent
                    CustomerRefNumber = payload.GetProperty("NumAtCard").GetString(),
                    SalesRemarks = payload.GetProperty("Comments").GetString(),
                    Attachments = new List<SalesOrderAttachment>() // Initialize for safety
                };

                if (DateTime.TryParse(payload.GetProperty("DocDate").GetString(), out var soDate))
                {
                    salesOrder.SODate = soDate;
                }
                if (payload.TryGetProperty("DocDueDate", out var dueDateElement) && DateTime.TryParse(dueDateElement.GetString(), out var deliveryDate))
                {
                    salesOrder.DeliveryDate = deliveryDate;
                }

                // 2. Map the line items from the payload.
                salesOrder.SalesItems = payload.GetProperty("DocumentLines").EnumerateArray().Select(itemDto => new SalesOrderItem
                {
                    ProductCode = itemDto.GetProperty("ItemCode").GetString() ?? "",
                    ProductName = "", // Can be fetched from DB or left blank
                    Quantity = itemDto.GetProperty("Quantity").GetDecimal(),
                    UOM = "", // Can be fetched from DB or left blank
                    Price = itemDto.GetProperty("UnitPrice").GetDecimal(),
                    WarehouseLocation = itemDto.GetProperty("WarehouseCode").GetString() ?? "",
                    TaxCode = itemDto.GetProperty("VatGroup").GetString(), // Match the create payload
                    // Note: TaxPrice and Total would need to be recalculated here based on your business logic.
                    // For simplicity, we'll assume they are calculated on the fly or not stored.
                }).ToList();

                // 3. Get the next Sales Order number from the tracker table.
                var tracker = await _context.SalesOrderNumberTrackers.FindAsync(1)
                              ?? new SalesOrderNumberTracker { Id = 1, LastUsedNumber = 1000000 };

                if (_context.Entry(tracker).State == EntityState.Detached)
                {
                    _context.SalesOrderNumberTrackers.Add(tracker);
                }
                tracker.LastUsedNumber++;
                salesOrder.SalesOrderNo = tracker.LastUsedNumber.ToString();

                // 4. Add the new SalesOrder (with its items) to the database context.
                _context.SalesOrders.Add(salesOrder);

                // 5. Save all changes to the SQL database.
                await _context.SaveChangesAsync();

                // 6. Return a success object that looks similar to the SAP response for consistency.
                return new
                {
                    DocEntry = salesOrder.Id,
                    DocNum = salesOrder.SalesOrderNo,
                    message = $"Sales Order {salesOrder.SalesOrderNo} created successfully in SQL!"
                };
            }
        }

        public async Task UpdateAsync(int docEntry, JsonElement payload)
        {
            if (_dataSource.ToUpper() == "SAP")
            {
                _logger.LogInformation("--> SalesOrderService: Updating Sales Order {docEntry} in SAP.", docEntry);
                await _sapService.UpdateSalesOrderAsync(docEntry, payload);
            }
            else // SQL Logic
            {
                _logger.LogInformation("--> SalesOrderService: Updating Sales Order {docEntry} in SQL.", docEntry);
                // In SQL mode, the ID is a Guid. Update logic would need to be different.
                throw new NotImplementedException("Update for SQL requires a Guid, not an int.");
            }
        }
        // --- END HIGHLIGHTED CODE ---

        // You would add Get, Update, and Delete methods here following the same hybrid pattern.
    }
}