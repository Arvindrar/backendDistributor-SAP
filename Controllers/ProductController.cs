using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backendDistributor.Models;
using backendDistributor.Services;
using backendDistributor.Dtos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace backendDistributor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly ProductService _productService;
        private readonly CustomerDbContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<ProductController> _logger;

        public ProductController(
            ProductService productService,
            CustomerDbContext context,
            IWebHostEnvironment hostingEnvironment,
            ILogger<ProductController> logger)
        {
            _productService = productService;
            _context = context;
            _hostingEnvironment = hostingEnvironment;
            _logger = logger;
        }

        // This POST method uses the switchable service
        [HttpPost]
        public async Task<ActionResult<object>> PostProduct([FromForm] ProductCreateDto productDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdProduct = await _productService.CreateProductAsync(productDto);
                // Return a 201 Created status. The body will contain the SQL entity or the SAP JSON object.
                return CreatedAtAction(nameof(GetProduct), new { id = 0 }, createdProduct);
            }
            catch (InvalidOperationException ex) // Catches "already exists" from SQL
            {
                return Conflict(new { message = ex.Message });
            }
            catch (HttpRequestException ex) // Catches errors from SAP
            {
                var errorResponse = new
                {
                    message = "An error occurred while communicating with the remote data source.",
                    details = ex.Message,
                };
                return StatusCode((int)(ex.StatusCode ?? HttpStatusCode.InternalServerError), errorResponse);
            }
            catch (Exception ex) // Catches all other errors
            {
                _logger.LogError(ex, "An unexpected error occurred while creating a product.");
                return StatusCode(500, new { message = "An internal server error occurred.", details = ex.Message });
            }
        }

        // --- Your existing SQL-only methods ---

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetProducts(
    [FromQuery] string? group,
    [FromQuery] string? searchTerm)
        {
            try
            {
                // This single line now handles fetching from both SQL and SAP!
                var products = await _productService.GetAllAsync(group, searchTerm);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get products.");
                return StatusCode(500, "An internal server error occurred while fetching products.");
            }
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            if (_context.Products == null)
            {
                return Problem("Entity set 'CustomerDbContext.Products' is null.", statusCode: 500);
            }
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound($"Product with ID {id} not found.");
            }
            return product;
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            // This is your original SQL delete logic.
            // ...
            return NoContent();
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> PutProduct(int id, [FromForm] ProductCreateDto productDto)
        {
            // This is your original SQL update logic.
            // ...
            return NoContent();
        }
    }
}