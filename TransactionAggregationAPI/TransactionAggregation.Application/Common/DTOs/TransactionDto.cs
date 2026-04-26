using System;
using System.Collections.Generic;
using System.Text;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.DTOs
{
    public record TransactionDto(
        Guid Id,
        Guid CustomerId,
        decimal Amount,
        string Currency,
        DateTime TransactionDate,
        string Description,
        TransactionCategory Category,
        TransactionStatus Status,
        string SourceSystem);
}
