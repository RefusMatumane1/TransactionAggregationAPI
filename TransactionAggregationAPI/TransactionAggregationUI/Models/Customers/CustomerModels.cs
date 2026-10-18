namespace TransactionAggregationUI.Models.Customers
{
    public class CustomerSummaryModel
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int AccountCount { get; set; }

        public List<string> Institutions { get; set; } = [];
    }

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