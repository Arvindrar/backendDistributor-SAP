using backendDistributor.Models;
using backendDistributor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders; // For StaticFiles
using Microsoft.Extensions.Options;
using System.IO;                         // For Path
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// ...
// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    // This keeps your existing setting to prevent errors with object cycles
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;

    // THIS IS THE NEW LINE: It tells .NET to output camelCase JSON
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});
//...


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<SapServiceLayerSettings>(
    builder.Configuration.GetSection("SapServiceLayer")
);

builder.Services.AddSingleton(new SapCookieContainer { Container = new CookieContainer() });
// 2. Register a custom HttpClientHandler that bypasses certificate validation.

// 3. Add a "named" HttpClient that USES the custom handler we just defined.
builder.Services.AddHttpClient("SapClient", (sp, client) =>
{
// Configure the client's base address and timeout from settings
var settings = sp.GetRequiredService<IOptions<SapServiceLayerSettings>>().Value;
client.BaseAddress = new Uri(settings.BaseUrl);
client.Timeout = TimeSpan.FromMinutes(5);
})
.ConfigurePrimaryHttpMessageHandler(sp =>
{
// Get the singleton cookie container from the service provider
var cookieContainerService = sp.GetRequiredService<SapCookieContainer>();

    return new HttpClientHandler
    {
        CookieContainer = cookieContainerService.Container,
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // Ignore SSL errors
    };
});

builder.Services.AddScoped<SapService>();
builder.Services.AddScoped<CustomerService>();

builder.Services.AddScoped<CustomerGroupService>();
builder.Services.AddScoped<VendorService>();
builder.Services.AddScoped<VendorGroupService>();
builder.Services.AddScoped<RouteService>();
builder.Services.AddScoped<SalesEmployeeService>();
builder.Services.AddScoped<ShippingTypeService>();
builder.Services.AddScoped<TaxService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<ProductGroupService>();
builder.Services.AddScoped<UomService>();
builder.Services.AddScoped<UomGroupService>();
builder.Services.AddScoped<WarehouseService>();
builder.Services.AddScoped<SalesOrderService>();


builder.Services.AddDbContext<CustomerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CustomerDbContext"))
        .LogTo(Console.WriteLine, LogLevel.Information) // 🔥 Show SQL
           .EnableSensitiveDataLogging()
           .EnableDetailedErrors()
           );

// Define the CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.AllowAnyOrigin() // Allows ANY origin
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build(); // Build the application

// Configure the HTTP request pipeline.
// The order of middleware here is important.

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    //app.UseDeveloperExceptionPage(); // More detailed errors in dev
}
else
{
    app.UseExceptionHandler("/Home/Error"); // Configure a proper error handler for production
    app.UseHsts();
}

app.UseHttpsRedirection(); // Redirect HTTP to HTTPS

// E.g., wwwroot/images/products/foo.jpg will be accessible at /images/products/foo.jpg
app.UseStaticFiles();

app.UseRouting(); // Explicitly adding UseRouting (often implicitly added, but good for clarity)

// *** ADD CORS MIDDLEWARE HERE ***
// It should generally be after UseRouting (implicitly added) and UseHttpsRedirection,
// and before UseAuthentication, UseAuthorization, and endpoint mapping.
app.UseCors("AllowReactApp");

app.UseAuthentication(); // If you add authentication later
app.UseAuthorization();  // Authorization middleware

app.MapControllers();    // Maps controller actions to routes

app.Run();

