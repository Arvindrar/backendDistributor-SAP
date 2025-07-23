using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backendDistributor.Models;
using backendDistributor.Models.DTOs;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Globalization;

namespace backendDistributor.Controllers
{
    // --- DTO for receiving the payload during POST/PUT ---
    // This class is now updated to match the smaller payload from React.
    public class SalesOrderSubmitDto
    {
        // Properties from the JSON payload
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public string? SODate { get; set; } // Format: "yyyyMMdd"
        public string? Address { get; set; }
        public string? Remark { get; set; }
        public string? NetTotal { get; set; }
        public List<SalesOrderItemSubmitDto>? PostingSalesOrderDetails { get; set; }

        // --- REMOVED THE FOLLOWING PROPERTIES ---
        // public DateTime? DeliveryDate { get; set; }
        // public string? CustomerRefNumber { get; set; }
        // public string? SalesEmployee { get; set; }
    }

    public class SalesOrderItemSubmitDto
    {
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }
        public string? Qty { get; set; }
        public string? UomCode { get; set; }
        public string? Price { get; set; }
        public string? LocationCode { get; set; }
        public string? TaxCode { get; set; }
        public string? TotalTax { get; set; }
        public string? NetTotal { get; set; }
    }

    // DTO for the entire multipart form
    public class SalesOrderFormDto
    {
        public string? Payload { get; set; }
        public List<IFormFile>? UploadedFiles { get; set; }
        public string? FilesToDeleteJson { get; set; }
    }


    [ApiController]
    [Route("api/[controller]")]
    public class SalesOrdersController : ControllerBase
    {
        private readonly CustomerDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SalesOrdersController(CustomerDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // --- POST (Create) Method - Updated Mapping Logic ---
        [HttpPost]
        public async Task<ActionResult> Create([FromForm] SalesOrderFormDto form)
        {
            if (string.IsNullOrEmpty(form.Payload)) return BadRequest(new { message = "Payload is missing." });

            SalesOrderSubmitDto? payload;
            try { payload = JsonSerializer.Deserialize<SalesOrderSubmitDto>(form.Payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch (JsonException ex) { return BadRequest(new { message = $"Invalid JSON payload: {ex.Message}" }); }

            if (payload == null || payload.PostingSalesOrderDetails == null || !payload.PostingSalesOrderDetails.Any())
            {
                return BadRequest(new { message = "Sales order must contain at least one item." });
            }

            // --- Map from the received DTO to the database entity ---
            var salesOrder = new SalesOrder
            {
                Id = Guid.NewGuid(),
                CustomerCode = payload.CustomerCode,
                CustomerName = payload.CustomerName,
                ShipToAddress = payload.Address,
                SalesRemarks = payload.Remark,

                // --- REMOVED MAPPING FOR THE 3 FIELDS ---
                // These fields will now be null or have default values in the database,
                // as they are not being provided in the payload.
                // DeliveryDate = payload.DeliveryDate,
                // CustomerRefNumber = payload.CustomerRefNumber,
                // SalesEmployee = payload.SalesEmployee,
                Attachments = new List<SalesOrderAttachment>()
            };

            if (DateTime.TryParseExact(payload.SODate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var soDate)) { salesOrder.SODate = soDate; }
            else { return BadRequest(new { message = "Invalid SODate format. Expected 'yyyyMMdd'." }); }

            salesOrder.SalesItems = payload.PostingSalesOrderDetails.Select(itemDto =>
            {
                decimal.TryParse(itemDto.Qty, out var qty); decimal.TryParse(itemDto.Price, out var price); decimal.TryParse(itemDto.TotalTax, out var tax); decimal.TryParse(itemDto.NetTotal, out var total);
                return new SalesOrderItem { ProductCode = itemDto.ProductCode, ProductName = itemDto.ProductName, Quantity = qty, UOM = itemDto.UomCode ?? "", Price = price, WarehouseLocation = itemDto.LocationCode ?? "", TaxCode = itemDto.TaxCode, TaxPrice = tax, Total = total };
            }).ToList();

            var tracker = await _context.SalesOrderNumberTrackers.FindAsync(1) ?? new SalesOrderNumberTracker { Id = 1, LastUsedNumber = 1000000 };
            if (_context.Entry(tracker).State == EntityState.Detached) _context.SalesOrderNumberTrackers.Add(tracker);
            tracker.LastUsedNumber++;
            salesOrder.SalesOrderNo = $"SO-{tracker.LastUsedNumber}";

            if (form.UploadedFiles != null) { await ProcessAndSaveAttachments(form.UploadedFiles, salesOrder); }

            _context.SalesOrders.Add(salesOrder);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Sales order {salesOrder.SalesOrderNo} created successfully!", id = salesOrder.Id, salesOrderNo = salesOrder.SalesOrderNo });
        }

        // --- PUT (Update) Method - Updated Mapping Logic ---
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromForm] SalesOrderFormDto form)
        {
            if (string.IsNullOrEmpty(form.Payload)) return BadRequest(new { message = "Payload is missing." });

            var existingOrder = await _context.SalesOrders.Include(o => o.SalesItems).Include(o => o.Attachments).FirstOrDefaultAsync(o => o.Id == id);
            if (existingOrder == null) return NotFound(new { message = $"Sales Order with ID {id} not found." });

            SalesOrderSubmitDto? payload;
            try { payload = JsonSerializer.Deserialize<SalesOrderSubmitDto>(form.Payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch (JsonException ex) { return BadRequest(new { message = $"Invalid JSON payload: {ex.Message}" }); }

            if (payload == null) return BadRequest(new { message = "Could not parse payload." });

            // --- Map updated fields from DTO to existing Entity ---
            existingOrder.CustomerCode = payload.CustomerCode; existingOrder.CustomerName = payload.CustomerName; existingOrder.ShipToAddress = payload.Address; existingOrder.SalesRemarks = payload.Remark;

            // --- REMOVED MAPPING FOR THE 3 FIELDS ---
            // existingOrder.DeliveryDate = payload.DeliveryDate;
            // existingOrder.CustomerRefNumber = payload.CustomerRefNumber;
            // existingOrder.SalesEmployee = payload.SalesEmployee;

            if (DateTime.TryParseExact(payload.SODate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var soDate)) { existingOrder.SODate = soDate; }

            if (existingOrder.SalesItems != null) { _context.SalesOrderItems.RemoveRange(existingOrder.SalesItems); }
            if (payload.PostingSalesOrderDetails != null)
            {
                existingOrder.SalesItems = payload.PostingSalesOrderDetails.Select(itemDto =>
                {
                    decimal.TryParse(itemDto.Qty, out var qty); decimal.TryParse(itemDto.Price, out var price); decimal.TryParse(itemDto.TotalTax, out var tax); decimal.TryParse(itemDto.NetTotal, out var total);
                    return new SalesOrderItem { SalesOrderId = existingOrder.Id, ProductCode = itemDto.ProductCode, ProductName = itemDto.ProductName, Quantity = qty, UOM = itemDto.UomCode ?? "", Price = price, WarehouseLocation = itemDto.LocationCode ?? "", TaxCode = itemDto.TaxCode, TaxPrice = tax, Total = total };
                }).ToList();
            }

            if (!string.IsNullOrEmpty(form.FilesToDeleteJson)) { await DeleteExistingAttachments(form.FilesToDeleteJson, existingOrder); }
            if (form.UploadedFiles != null) { await ProcessAndSaveAttachments(form.UploadedFiles, existingOrder); }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Sales order updated successfully!" });
        }

        // --- GET, GET BY ID, DELETE and HELPER methods can remain as they were ---
        // They are not affected by this change to the POST/PUT payload.
        // [Copy the GET, GetById, DELETE, and helper methods from the previous correct answer here]

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SalesOrderListDto>>> GetSalesOrders([FromQuery] string? salesOrderNo, [FromQuery] string? customerName)
        {
            var query = _context.SalesOrders.AsQueryable();
            if (!string.IsNullOrEmpty(salesOrderNo)) { query = query.Where(o => o.SalesOrderNo != null && o.SalesOrderNo.Contains(salesOrderNo)); }
            if (!string.IsNullOrEmpty(customerName)) { query = query.Where(o => o.CustomerName != null && o.CustomerName.Contains(customerName)); }
            return Ok(await query.OrderByDescending(o => o.SODate).Select(order => new SalesOrderListDto { Id = order.Id, SalesOrderNo = order.SalesOrderNo, CustomerCode = order.CustomerCode, CustomerName = order.CustomerName, SODate = order.SODate, SalesRemarks = order.SalesRemarks, OrderTotal = (order.SalesItems != null) ? order.SalesItems.Sum(i => i.Total) : 0 }).ToListAsync());
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult> GetById(Guid id)
        {
            var order = await _context.SalesOrders.Include(o => o.SalesItems).Include(o => o.Attachments).AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();
            var response = new { customerCode = order.CustomerCode, customerName = order.CustomerName, soDate = order.SODate.ToString("yyyyMMdd"), address = order.ShipToAddress, remark = order.SalesRemarks, netTotal = (order.SalesItems?.Sum(i => i.Total) ?? 0).ToString("F2", CultureInfo.InvariantCulture), postingSalesOrderDetails = order.SalesItems?.Select((item, index) => new { slNo = index + 1, productCode = item.ProductCode, productName = item.ProductName, qty = item.Quantity.ToString("F1", CultureInfo.InvariantCulture), uomCode = item.UOM, price = item.Price.ToString("F2", CultureInfo.InvariantCulture), locationCode = item.WarehouseLocation, taxCode = item.TaxCode, totalTax = (item.TaxPrice ?? 0).ToString("F2", CultureInfo.InvariantCulture), netTotal = item.Total.ToString("F2", CultureInfo.InvariantCulture), id = item.Id }).ToList(), id = order.Id, salesOrderNo = order.SalesOrderNo, deliveryDate = order.DeliveryDate, customerRefNumber = order.CustomerRefNumber, salesEmployee = order.SalesEmployee, attachments = (order.Attachments ?? new List<SalesOrderAttachment>()).Select(a => new { a.Id, a.FileName, a.FilePath }).ToList() };
            return Ok(response);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await _context.SalesOrders.Include(o => o.Attachments).FirstOrDefaultAsync(o => o.Id == id);
            if (existing == null) return NotFound();
            if (existing.Attachments != null) { DeleteAllPhysicalFilesForOrder(existing.Attachments); }
            _context.SalesOrders.Remove(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task ProcessAndSaveAttachments(List<IFormFile> files, SalesOrder order)
        {
            string path = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "sales_attachments");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            foreach (var file in files.Where(f => f.Length > 0))
            {
                var uniqueName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(path, uniqueName);
                await using (var stream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(stream); }
                if (order.Attachments == null) order.Attachments = new List<SalesOrderAttachment>();
                order.Attachments.Add(new SalesOrderAttachment { FileName = file.FileName, FilePath = Path.Combine("uploads", "sales_attachments", uniqueName).Replace('\\', '/') });
            }
        }

        private async Task DeleteExistingAttachments(string json, SalesOrder order)
        {
            try
            {
                var ids = JsonSerializer.Deserialize<List<Guid>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (ids != null && ids.Any() && order.Attachments != null)
                {
                    var toDelete = order.Attachments.Where(a => ids.Contains(a.Id)).ToList();
                    DeleteAllPhysicalFilesForOrder(toDelete);
                    foreach (var item in toDelete) _context.SalesOrderAttachments.Remove(item);
                }
            }
            catch (JsonException ex) { Console.Error.WriteLine($"Error deserializing FilesToDeleteJson: {ex.Message}"); }
        }

        private void DeleteAllPhysicalFilesForOrder(ICollection<SalesOrderAttachment> attachments)
        {
            if (attachments == null) return;
            string basePath = _env.WebRootPath ?? "wwwroot";
            foreach (var att in attachments.Where(a => !string.IsNullOrEmpty(a.FilePath)))
            {
                var physicalPath = Path.Combine(basePath, att.FilePath!.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(physicalPath)) { try { System.IO.File.Delete(physicalPath); } catch (IOException ex) { Console.Error.WriteLine(ex.Message); } }
            }
        }
    }
}