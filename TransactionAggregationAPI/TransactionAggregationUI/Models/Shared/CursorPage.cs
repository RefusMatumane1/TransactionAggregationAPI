namespace TransactionAggregationUI.Models.Shared
{
    public class CursorPage<T>
    {
        public List<T> Items { get; set; } = [];
        public int PageSize { get; set; }
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }
        public int? TotalCount { get; set; }

        public bool TotalCountCapped { get; set; }
    }
}