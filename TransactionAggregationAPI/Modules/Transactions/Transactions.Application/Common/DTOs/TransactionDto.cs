using Modules.Transactions.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Modules.Transactions.Application.Common.DTOs
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
        string SourceSystem,
        Guid? AccountId = null);
}