namespace TransactionAggregationAPI.DTOs
{
    public sealed record CreateTransactionRequest(
        decimal Amount,
        string Currency,
        DateTime TransactionDate,
        string Description,
        string SourceSystem)
    {
        public void Deconstruct(out decimal amount, out string currency, out DateTime transactionDate,
            out string description, out string sourceSystem)
        {
            amount = Amount;
            currency = Currency;
            transactionDate = TransactionDate;
            description = Description;
            sourceSystem = SourceSystem;
        }
    }
}
