using backendDistributor.DTOs;
using backendDistributor.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace backendDistributor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ARInvoicesController : ControllerBase
    {
        private readonly CustomerDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ARInvoicesController(CustomerDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: api/ARInvoices
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ARInvoiceListDto>>> GetARInvoices(
            [FromQuery] string? arInvoiceNo,
            [FromQuery] string? customerName,
            [FromQuery] string? salesOrderNo)
        {
            var query = _context.ARInvoices.AsQueryable();

            if (!string.IsNullOrEmpty(arInvoiceNo))
            {
                query = query.Where(i => i.ARInvoiceNo.Contains(arInvoiceNo));
            }
            if (!string.IsNullOrEmpty(customerName))
            {
                query = query.Where(i => i.CustomerName.Contains(customerName));
            }
            if (!string.IsNullOrEmpty(salesOrderNo))
            {
                query = query.Where(i => i.SalesOrderNo.Contains(salesOrderNo));
            }

            var invoices = await query
                .Include(i => i.ARInvoiceItems)
                .Select(inv => new ARInvoiceListDto
                {
                    Id = inv.Id,
                    ARInvoiceNo = inv.ARInvoiceNo,
                    SalesOrderNo = inv.SalesOrderNo,
                    CustomerName = inv.CustomerName,
                    CustomerCode = inv.CustomerCode,
                    InvoiceDate = inv.InvoiceDate,
                    InvoiceRemarks = inv.InvoiceRemarks,
                    InvoiceTotal = inv.ARInvoiceItems.Sum(item => item.Total)
                })
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

            return Ok(invoices);
        }

        // GET: api/ARInvoices/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult> GetARInvoiceById(Guid id)
        {
            var invoice = await _context.ARInvoices
                .Include(i => i.ARInvoiceItems)
                .Include(i => i.Attachments)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
            {
                return NotFound(new { message = $"A/R Invoice with ID {id} not found." });
            }

            return Ok(invoice);
        }

        // POST: api/ARInvoices
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> CreateARInvoice([FromForm] ARInvoiceCreateDto dto)
        {
            if (Request.Form.TryGetValue("InvoiceItemsJson", out var itemsJsonString) && !string.IsNullOrEmpty(itemsJsonString))
            {
                try
                {
                    dto.InvoiceItems = JsonSerializer.Deserialize<List<ARInvoiceItemDto>>(itemsJsonString.ToString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException ex)
                {
                    return BadRequest(new { message = "Invalid format for InvoiceItemsJson.", details = ex.Message });
                }
            }

            if (dto.InvoiceItems == null || !dto.InvoiceItems.Any())
            {
                return BadRequest(new { message = "At least one invoice item is required." });
            }

            var tracker = await _context.ARInvoiceNumberTrackers.FindAsync(3) ?? new ARInvoiceNumberTracker { Id = 3, LastUsedNumber = 1000000 };
            if (_context.Entry(tracker).State == EntityState.Detached) _context.ARInvoiceNumberTrackers.Add(tracker);

            tracker.LastUsedNumber++;
            string newInvoiceNumber = $"INV-{tracker.LastUsedNumber}";

            var invoice = new ARInvoice
            {
                ARInvoiceNo = newInvoiceNumber,
                SalesOrderNo = dto.SalesOrderNo,
                CustomerCode = dto.CustomerCode,
                CustomerName = dto.CustomerName,
                InvoiceDate = dto.InvoiceDate,
                DueDate = dto.DueDate,
                CustomerRefNumber = dto.CustomerRefNumber,
                BillToAddress = dto.BillToAddress,
                InvoiceRemarks = dto.InvoiceRemarks,

                ARInvoiceItems = dto.InvoiceItems.Select(i => new ARInvoiceItem
                {
                    // --- FIX START: Use TryParse for safety ---
                    ProductCode = i.ProductCode,
                    ProductName = i.ProductName,
                    Quantity = decimal.TryParse(i.Quantity, out var qty) ? qty : 0,
                    UOM = i.UOM,
                    Price = decimal.TryParse(i.Price, out var price) ? price : 0,
                    WarehouseLocation = i.WarehouseLocation,
                    TaxCode = i.TaxCode,
                    TaxPrice = decimal.TryParse(i.TaxPrice, out var tax) ? tax : (decimal?)null,
                    Total = decimal.TryParse(i.Total, out var total) ? total : 0
                    // --- FIX END ---
                }).ToList()
            };

            if (dto.UploadedFiles != null)
            {
                string uploadBasePath = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                string uploadFolder = Path.Combine(uploadBasePath, "uploads", "arinvoices");
                Directory.CreateDirectory(uploadFolder);

                foreach (var file in dto.UploadedFiles)
                {
                    var clientFileName = Path.GetFileName(file.FileName);
                    var uniqueFileName = $"{Guid.NewGuid()}_{clientFileName}";
                    var physicalPath = Path.Combine(uploadFolder, uniqueFileName);
                    var relativePath = Path.Combine("uploads", "arinvoices", uniqueFileName).Replace(Path.DirectorySeparatorChar, '/');

                    await using var stream = new FileStream(physicalPath, FileMode.Create);
                    await file.CopyToAsync(stream);

                    invoice.Attachments.Add(new ARInvoiceAttachment
                    {
                        FileName = clientFileName,
                        FilePath = relativePath,
                    });
                }
            }

            _context.ARInvoices.Add(invoice);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"A/R Invoice {newInvoiceNumber} created successfully!", id = invoice.Id, arInvoiceNo = newInvoiceNumber });
        }


        // PUT: api/ARInvoices/{id}
        [HttpPut("{id:guid}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateARInvoice(Guid id, [FromForm] ARInvoiceCreateDto dto)
        {
            var existingInvoice = await _context.ARInvoices
                .Include(i => i.ARInvoiceItems)
                .Include(i => i.Attachments)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (existingInvoice == null)
            {
                return NotFound(new { message = $"A/R Invoice with ID {id} not found." });
            }

            // --- 1. Update Scalar Properties from DTO ---
            existingInvoice.SalesOrderNo = dto.SalesOrderNo;
            existingInvoice.CustomerCode = dto.CustomerCode;
            existingInvoice.CustomerName = dto.CustomerName;
            existingInvoice.InvoiceDate = dto.InvoiceDate;
            existingInvoice.DueDate = dto.DueDate;
            existingInvoice.CustomerRefNumber = dto.CustomerRefNumber;
            existingInvoice.BillToAddress = dto.BillToAddress;
            existingInvoice.InvoiceRemarks = dto.InvoiceRemarks;

            // --- 2. Update Items (A safe "remove and replace" implementation) ---
            if (!string.IsNullOrEmpty(dto.InvoiceItemsJson))
            {
                var newItemsDto = JsonSerializer.Deserialize<List<ARInvoiceItemDto>>(dto.InvoiceItemsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                _context.ARInvoiceItems.RemoveRange(existingInvoice.ARInvoiceItems);

                if (newItemsDto != null)
                {
                    // --- FIX START: Use TryParse for safety ---
                    existingInvoice.ARInvoiceItems = newItemsDto.Select(i => new ARInvoiceItem
                    {
                        ProductCode = i.ProductCode,
                        ProductName = i.ProductName,
                        Quantity = decimal.TryParse(i.Quantity, out var qty) ? qty : 0,
                        UOM = i.UOM,
                        Price = decimal.TryParse(i.Price, out var price) ? price : 0,
                        WarehouseLocation = i.WarehouseLocation,
                        TaxCode = i.TaxCode,
                        TaxPrice = decimal.TryParse(i.TaxPrice, out var tax) ? tax : (decimal?)null,
                        Total = decimal.TryParse(i.Total, out var total) ? total : 0
                    }).ToList();
                    // --- FIX END ---
                }
            }

            // --- 3. Handle Attachment Deletions ---
            if (!string.IsNullOrEmpty(dto.FilesToDeleteJson))
            {
                var fileIdsToDelete = JsonSerializer.Deserialize<List<Guid>>(dto.FilesToDeleteJson);
                var attachmentsToDelete = existingInvoice.Attachments.Where(a => fileIdsToDelete.Contains(a.Id)).ToList();

                if (attachmentsToDelete.Any())
                {
                    string fileStorageBasePath = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                    foreach (var att in attachmentsToDelete)
                    {
                        var physicalPath = Path.Combine(fileStorageBasePath, att.FilePath.TrimStart('/'));
                        if (System.IO.File.Exists(physicalPath))
                        {
                            try { System.IO.File.Delete(physicalPath); }
                            catch (IOException ex) { Console.Error.WriteLine($"Error deleting file {physicalPath}: {ex.Message}"); }
                        }
                    }
                    _context.ARInvoiceAttachments.RemoveRange(attachmentsToDelete);
                }
            }

            // --- 4. Handle New Attachment Uploads ---
            if (dto.UploadedFiles != null && dto.UploadedFiles.Any())
            {
                string uploadBasePath = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                string uploadFolder = Path.Combine(uploadBasePath, "uploads", "arinvoices");
                Directory.CreateDirectory(uploadFolder);

                foreach (var file in dto.UploadedFiles)
                {
                    var clientFileName = Path.GetFileName(file.FileName);
                    var uniqueFileName = $"{Guid.NewGuid()}_{clientFileName}";
                    var physicalPath = Path.Combine(uploadFolder, uniqueFileName);
                    var relativePath = Path.Combine("uploads", "arinvoices", uniqueFileName).Replace(Path.DirectorySeparatorChar, '/');

                    await using var stream = new FileStream(physicalPath, FileMode.Create);
                    await file.CopyToAsync(stream);

                    existingInvoice.Attachments.Add(new ARInvoiceAttachment
                    {
                        FileName = clientFileName,
                        FilePath = relativePath,
                    });
                }
            }

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "A/R Invoice updated successfully!" });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "The record was modified by another user. Please refresh and try again." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred during the update: " + ex.InnerException?.Message ?? ex.Message });
            }
        }


        // DELETE: api/ARInvoices/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteARInvoice(Guid id)
        {
            var existingInvoice = await _context.ARInvoices
                .Include(i => i.Attachments)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (existingInvoice == null)
            {
                return NotFound();
            }

            // Delete physical files
            if (existingInvoice.Attachments.Any())
            {
                string fileStorageBasePath = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                foreach (var att in existingInvoice.Attachments)
                {
                    var physicalPath = Path.Combine(fileStorageBasePath, att.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(physicalPath))
                    {
                        try { System.IO.File.Delete(physicalPath); }
                        catch (IOException ex) { Console.Error.WriteLine($"Error deleting file {physicalPath}: {ex.Message}"); }
                    }
                }
            }

            _context.ARInvoices.Remove(existingInvoice);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}