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
    [Route("api/Products")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly ProductService _productService;
        private readonly ILogger<ProductController> _logger;

        // --- THIS IS THE FIX ---
        // The constructor now only injects the dependencies it actually uses.
        // CustomerDbContext and IWebHostEnvironment have been removed because
        // they are already injected into the ProductService.
        public ProductController(
            ProductService productService,
            ILogger<ProductController> logger)
        {
            _productService = productService;
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

                // THE FIX:
                // 1. Get the SKU from the newly created product object.
                //    We use reflection here because the return type is 'object'.
                var sku = createdProduct.GetType().GetProperty("SKU")?.GetValue(createdProduct)?.ToString();

                // 2. Reference the correct Get method: 'GetProductBySku'.
                // 3. Pass the 'sku' as the route parameter.
                return CreatedAtAction(nameof(GetProductBySku), new { sku = sku }, createdProduct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating a product.");
                return BadRequest(new { message = ex.Message });
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
                var products = await _productService.GetAllAsync(group, searchTerm);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get products.");
                return StatusCode(500, "An internal server error occurred while fetching products.");
            }
        }

        [HttpGet("{sku}")]
        public async Task<ActionResult<Product>> GetProductBySku(string sku)
        {
            try
            {
                var product = await _productService.GetBySkuAsync(sku);
                if (product == null)
                {
                    return NotFound($"Product with SKU '{sku}' not found.");
                }
                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product by SKU {SKU}", sku);
                return StatusCode(500, "An internal server error occurred.");
            }
        }
        //[HttpGet("{id:int}")]
        //public async Task<ActionResult<Product>> GetProduct(int id)
        //{
        //    if (_context.Products == null)
        //    {
        //        return Problem("Entity set 'CustomerDbContext.Products' is null.", statusCode: 500);
        //    }
        //    var product = await _context.Products.FindAsync(id);
        //    if (product == null)
        //    {
        //        return NotFound($"Product with ID {id} not found.");
        //    }
        //    return product;
        //}


    }
}