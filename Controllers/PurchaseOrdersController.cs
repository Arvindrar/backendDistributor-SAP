using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backendDistributor.Models;
// You might need a DTO for your list view if it's different
// using backendDistributor.Models.DTOs;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Globalization;

namespace backendDistributor.Controllers
{
    // --- DTOs that EXACTLY match the JSON payload structure ---
    // These are used for receiving data (in POST/PUT) and can be used for sending it back.
    public class PurchaseOrderPayloadDto
    {
        public string? PoNumber { get; set; } // Note: Can be vendor ref number on create
        public string? VendorCode { get; set; }
        public string? VendorName { get; set; }
        public string? PoDate { get; set; } // Format: "yyyyMMdd"
        public string? Address { get; set; }
        public string? Remark { get; set; }
        public string? NetTotal { get; set; }
        public List<PurchaseOrderItemPayloadDto>? PostingPurchaseOrderDetails { get; set; }

        // Additional fields from the form that the backend might need
        public DateTime? DeliveryDate { get; set; }
        public string? VendorRefNumber { get; set; }
    }

    public class PurchaseOrderItemPayloadDto
    {
        public int SlNo { get; set; }
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

    // This DTO represents the entire multipart/form-data request
    public class PurchaseOrderFormDto
    {
        public string? Payload { get; set; }
        public List<IFormFile>? UploadedFiles { get; set; }
        public string? FilesToDeleteJson { get; set; }
    }


    [ApiController]
    [Route("api/[controller]")]
    public class PurchaseOrdersController : ControllerBase
    {
        private readonly CustomerDbContext _context;
        private readonly IWebHostEnvironment _env;

        public PurchaseOrdersController(CustomerDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // --- GET (List) Method ---
        [HttpGet]
        public async Task<ActionResult> GetPurchaseOrders([FromQuery] string? poNumber, [FromQuery] string? vendorName)
        {
            var query = _context.PurchaseOrders.AsQueryable();
            if (!string.IsNullOrEmpty(poNumber)) { query = query.Where(o => o.PurchaseOrderNo != null && o.PurchaseOrderNo.Contains(poNumber)); }
            if (!string.IsNullOrEmpty(vendorName)) { query = query.Where(o => o.VendorName != null && o.VendorName.Contains(vendorName)); }

            var orders = await query
                .Include(p => p.PurchaseItems)
                .OrderByDescending(p => p.PODate)
                .Select(p => new {
                    p.Id,
                    p.PurchaseOrderNo,
                    p.VendorName,
                    p.PODate,
                    p.PurchaseRemarks,
                    OrderTotal = p.PurchaseItems.Sum(i => i.Total)
                })
                .ToListAsync();

            return Ok(orders);
        }

        // --- GET (By ID) Method - REWRITTEN to return the desired JSON structure ---
        [HttpGet("{id:guid}")]
        public async Task<ActionResult> GetById(Guid id)
        {
            var order = await _context.PurchaseOrders
                .Include(p => p.PurchaseItems)
                .Include(p => p.Attachments)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (order == null) return NotFound();

            var response = new
            {
                // Root-level properties matching your target JSON
                id = order.Id,
                poNumber = order.PurchaseOrderNo,
                vendorCode = order.VendorCode,
                vendorName = order.VendorName,
                poDate = order.PODate.ToString("yyyyMMdd"),
                address = order.ShipToAddress,
                remark = order.PurchaseRemarks,
                netTotal = (order.PurchaseItems?.Sum(i => i.Total) ?? 0).ToString("F2", CultureInfo.InvariantCulture),

                // Details array with correct property names and formatting
                postingPurchaseOrderDetails = order.PurchaseItems?.Select((item, index) => new
                {
                    id = item.Id,
                    slNo = index + 1,
                    productCode = item.ProductCode,
                    productName = item.ProductName,
                    qty = item.Quantity.ToString("F1", CultureInfo.InvariantCulture),
                    uomCode = item.UOM,
                    price = item.Price.ToString("F2", CultureInfo.InvariantCulture),
                    locationCode = item.WarehouseLocation,
                    taxCode = item.TaxCode,
                    totalTax = (item.TaxPrice ?? 0).ToString("F2", CultureInfo.InvariantCulture),
                    netTotal = item.Total.ToString("F2", CultureInfo.InvariantCulture)
                }).ToList(),

                // Other form fields needed by React state
                deliveryDate = order.DeliveryDate,
                vendorRefNumber = order.VendorRefNumber,
                attachments = (order.Attachments ?? new List<PurchaseOrderAttachment>()).Select(a => new { a.Id, a.FileName, a.FilePath }).ToList()
            };

            return Ok(response);
        }

        // --- POST (Create) Method ---
        [HttpPost]
        public async Task<ActionResult> Create([FromForm] PurchaseOrderFormDto form)
        {
            if (string.IsNullOrEmpty(form.Payload)) return BadRequest(new { message = "Payload is missing." });

            PurchaseOrderPayloadDto? payload;
            try { payload = JsonSerializer.Deserialize<PurchaseOrderPayloadDto>(form.Payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch (JsonException ex) { return BadRequest(new { message = $"Invalid JSON payload: {ex.Message}" }); }

            if (payload == null || payload.PostingPurchaseOrderDetails == null || !payload.PostingPurchaseOrderDetails.Any())
            {
                return BadRequest(new { message = "Purchase order must contain at least one item." });
            }

            // --- Map from Payload DTO to Database Entity ---
            var purchaseOrder = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                VendorCode = payload.VendorCode,
                VendorName = payload.VendorName,
                ShipToAddress = payload.Address,
                PurchaseRemarks = payload.Remark,
                DeliveryDate = payload.DeliveryDate,
                VendorRefNumber = payload.VendorRefNumber,
                Attachments = new List<PurchaseOrderAttachment>()
            };

            if (DateTime.TryParseExact(payload.PoDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var poDate)) { purchaseOrder.PODate = poDate; }
            else { return BadRequest(new { message = "Invalid PoDate format. Expected 'yyyyMMdd'." }); }

            purchaseOrder.PurchaseItems = payload.PostingPurchaseOrderDetails.Select(itemDto =>
            {
                decimal.TryParse(itemDto.Qty, out var qty); decimal.TryParse(itemDto.Price, out var price); decimal.TryParse(itemDto.TotalTax, out var tax); decimal.TryParse(itemDto.NetTotal, out var total);
                return new PurchaseOrderItem { ProductCode = itemDto.ProductCode, ProductName = itemDto.ProductName, Quantity = qty, UOM = itemDto.UomCode ?? "", Price = price, WarehouseLocation = itemDto.LocationCode ?? "", TaxCode = itemDto.TaxCode, TaxPrice = tax, Total = total };
            }).ToList();

            var tracker = await _context.PurchaseOrderNumberTrackers.FindAsync(1) ?? new PurchaseOrderNumberTracker { Id = 1, LastUsedNumber = 2000000 };
            if (_context.Entry(tracker).State == EntityState.Detached) _context.PurchaseOrderNumberTrackers.Add(tracker);
            tracker.LastUsedNumber++;
            purchaseOrder.PurchaseOrderNo = $"PO-{tracker.LastUsedNumber}";

            if (form.UploadedFiles != null) { await ProcessAndSaveAttachments(form.UploadedFiles, purchaseOrder); }

            _context.PurchaseOrders.Add(purchaseOrder);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Purchase Order {purchaseOrder.PurchaseOrderNo} created successfully!", id = purchaseOrder.Id, purchaseOrderNo = purchaseOrder.PurchaseOrderNo });
        }

        // --- PUT (Update) Method ---
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromForm] PurchaseOrderFormDto form)
        {
            if (string.IsNullOrEmpty(form.Payload)) return BadRequest(new { message = "Payload is missing." });

            var existingOrder = await _context.PurchaseOrders.Include(p => p.PurchaseItems).Include(p => p.Attachments).FirstOrDefaultAsync(p => p.Id == id);
            if (existingOrder == null) return NotFound(new { message = $"Purchase Order with ID {id} not found." });

            PurchaseOrderPayloadDto? payload;
            try { payload = JsonSerializer.Deserialize<PurchaseOrderPayloadDto>(form.Payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch (JsonException ex) { return BadRequest(new { message = $"Invalid JSON payload: {ex.Message}" }); }

            if (payload == null) return BadRequest(new { message = "Could not parse payload." });

            existingOrder.VendorCode = payload.VendorCode; existingOrder.VendorName = payload.VendorName; existingOrder.ShipToAddress = payload.Address; existingOrder.PurchaseRemarks = payload.Remark; existingOrder.DeliveryDate = payload.DeliveryDate; existingOrder.VendorRefNumber = payload.VendorRefNumber;
            if (DateTime.TryParseExact(payload.PoDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var poDate)) { existingOrder.PODate = poDate; }

            if (existingOrder.PurchaseItems != null) { _context.PurchaseOrderItems.RemoveRange(existingOrder.PurchaseItems); }
            if (payload.PostingPurchaseOrderDetails != null)
            {
                existingOrder.PurchaseItems = payload.PostingPurchaseOrderDetails.Select(itemDto =>
                {
                    decimal.TryParse(itemDto.Qty, out var qty); decimal.TryParse(itemDto.Price, out var price); decimal.TryParse(itemDto.TotalTax, out var tax); decimal.TryParse(itemDto.NetTotal, out var total);
                    return new PurchaseOrderItem { PurchaseOrderId = existingOrder.Id, ProductCode = itemDto.ProductCode, ProductName = itemDto.ProductName, Quantity = qty, UOM = itemDto.UomCode ?? "", Price = price, WarehouseLocation = itemDto.LocationCode ?? "", TaxCode = itemDto.TaxCode, TaxPrice = tax, Total = total };
                }).ToList();
            }

            if (!string.IsNullOrEmpty(form.FilesToDeleteJson)) { await DeleteExistingAttachments(form.FilesToDeleteJson, existingOrder); }
            if (form.UploadedFiles != null) { await ProcessAndSaveAttachments(form.UploadedFiles, existingOrder); }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Purchase order updated successfully!" });
        }

        // --- DELETE Method and Helpers ---
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await _context.PurchaseOrders.Include(p => p.Attachments).FirstOrDefaultAsync(p => p.Id == id);
            if (existing == null) return NotFound();
            if (existing.Attachments != null) { DeleteAllPhysicalFilesForOrder(existing.Attachments); }
            _context.PurchaseOrders.Remove(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task ProcessAndSaveAttachments(List<IFormFile> files, PurchaseOrder order)
        {
            string path = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "purchase_orders");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            foreach (var file in files.Where(f => f.Length > 0))
            {
                var uniqueName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(path, uniqueName);
                await using (var stream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(stream); }
                if (order.Attachments == null) order.Attachments = new List<PurchaseOrderAttachment>();
                order.Attachments.Add(new PurchaseOrderAttachment { FileName = file.FileName, FilePath = Path.Combine("uploads", "purchase_orders", uniqueName).Replace('\\', '/') });
            }
        }

        private async Task DeleteExistingAttachments(string json, PurchaseOrder order)
        {
            try
            {
                var ids = JsonSerializer.Deserialize<List<Guid>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (ids != null && ids.Any() && order.Attachments != null)
                {
                    var toDelete = order.Attachments.Where(a => ids.Contains(a.Id)).ToList();
                    DeleteAllPhysicalFilesForOrder(toDelete);
                    foreach (var item in toDelete) _context.PurchaseOrderAttachments.Remove(item);
                }
            }
            catch (JsonException ex) { Console.Error.WriteLine($"Error deserializing FilesToDeleteJson: {ex.Message}"); }
        }

        private void DeleteAllPhysicalFilesForOrder(ICollection<PurchaseOrderAttachment> attachments)
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