namespace backendDistributor.Models
{
    public class ARInvoiceNumberTracker
    {
        // Use a different ID to avoid conflict with other trackers
        public int Id { get; set; } = 3; // Primary key, hardcoded to 3
        public int LastUsedNumber { get; set; }
    }
}