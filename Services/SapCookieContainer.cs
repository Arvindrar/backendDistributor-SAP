using System.Net;

namespace backendDistributor.Services
{
    /// <summary>
    /// A Singleton service to hold the SAP B1 Service Layer session cookies (B1SESSION, ROUTEID)
    /// This allows the session to be shared across multiple HTTP requests from the client.
    /// </summary>
    public class SapCookieContainer
    {
        public CookieContainer? Container { get; set; }
    }
}