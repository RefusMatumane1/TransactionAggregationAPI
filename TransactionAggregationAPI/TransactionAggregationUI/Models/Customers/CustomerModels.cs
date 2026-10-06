namespace TransactionAggregationUI.Models.Customers
{
    // GET /api/v1/customers: one row per customer the caller can see.
    public class CustomerSummaryModel
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int AccountCount { get; set; }

        // Bank codes of the linked accounts the caller may read.
        public List<string> Institutions { get; set; } = [];
    }

    // GET /api/v1/customers/{id}: the customer and the linked accounts the caller may read.
    public class CustomerModel
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<LinkedAccountModel> Accounts { get; set; } = [];
    }

    public class LinkedAccountModel
    {
        public string Institution { get; set; } = string.Empty;
        public string ExternalAccountId { get; set; } = string.Empty;
        public DateTime LinkedAt { get; set; }
    }
}