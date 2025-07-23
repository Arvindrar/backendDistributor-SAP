using System.Drawing.Printing;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace backendDistributor.Services // Make sure this namespace matches your project
{
    public class SapService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SapService> _logger;
        private static CookieContainer _cookieContainer = new CookieContainer();
        private static string? _b1SessionId = null;
        private static DateTime _sessionExpiry;

        public SapService(IConfiguration configuration, ILogger<SapService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // WARNING: This bypasses SSL certificate validation.
            // OK for development with a self-signed cert, but in production, install the cert properly.
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            _httpClient = new HttpClient(handler);
            _httpClient.BaseAddress = new Uri(_configuration["SapServiceLayer:BaseUrl"]);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private async Task LoginAsync()
        {
            // If we have a session and it's not expired, do nothing.
            if (!string.IsNullOrEmpty(_b1SessionId) && DateTime.UtcNow < _sessionExpiry)
            {
                return;
            }

            _logger.LogInformation("SAP session expired or not found. Attempting to log in.");

            var loginPayload = new
            {
                CompanyDB = _configuration["SapServiceLayer:CompanyDB"],
                UserName = _configuration["SapServiceLayer:UserName"],
                Password = _configuration["SapServiceLayer:Password"]
            };

            var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("Login", content);
                response.EnsureSuccessStatusCode();

                var loginResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
                _b1SessionId = loginResponse.GetProperty("SessionId").GetString();
                var timeout = loginResponse.GetProperty("SessionTimeout").GetInt32();
                _sessionExpiry = DateTime.UtcNow.AddMinutes(timeout - 2); // Renew 2 mins before expiry

                _logger.LogInformation("Successfully logged into SAP Service Layer. Session is valid for {Timeout} minutes.", timeout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to login to SAP Service Layer.");
                throw; // Re-throw the exception so the caller knows it failed
            }
        }

        // --- METHODS TO INTERACT WITH SAP ---

        // In Services/SapService.cs

        public async Task<string> GetCustomersAsync(string? group, string? searchTerm, int pageNumber, int pageSize)
        {
            await LoginAsync();

            var filterClauses = new List<string>();
            filterClauses.Add("CardType eq 'cCustomer'");

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                filterClauses.Add($"(contains(tolower(CardName), '{term}') or contains(tolower(CardCode), '{term}'))");
            }

            var filterQuery = string.Join(" and ", filterClauses);

            // Calculate the $skip value for SAP OData
            int skip = (pageNumber - 1) * pageSize;

            // We want to get the total count and the data in one go if possible.
            // We add $count=true to the query.
            var fields = "CardCode,CardName,CurrentAccountBalance,SalesPersonCode,BPAddresses,Notes";
            var requestUrl = $"BusinessPartners?$filter={filterQuery}&$select={fields}&$skip={skip}&$top={pageSize}&$count=true";

            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetCustomerByIdAsync(string cardCode)
        {
            await LoginAsync();
            // The CardCode needs to be in single quotes in the URL
            var requestUrl = $"BusinessPartners('{cardCode}')";

            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> CreateCustomerAsync(JsonElement customerData)
        {
            await LoginAsync();

            var content = new StringContent(customerData.ToString(), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("BusinessPartners", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create SAP Business Partner. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }

            return await response.Content.ReadAsStringAsync();
        }

        public async Task UpdateCustomerAsync(string cardCode, JsonElement customerData)
        {
            await LoginAsync();

            // SAP uses PATCH for updates, not PUT
            var requestUrl = $"BusinessPartners('{cardCode}')";
            var content = new StringContent(customerData.ToString(), Encoding.UTF8, "application/json");

            var response = await _httpClient.PatchAsync(requestUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update SAP Business Partner. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }

            // A successful PATCH returns 204 No Content, so there is no body to return.
        }

        public async Task DeleteCustomerAsync(string cardCode)
        {
            await LoginAsync();

            var requestUrl = $"BusinessPartners('{cardCode}')";
            var response = await _httpClient.DeleteAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete SAP Business Partner. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
            // A successful DELETE returns 204 No Content.
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}