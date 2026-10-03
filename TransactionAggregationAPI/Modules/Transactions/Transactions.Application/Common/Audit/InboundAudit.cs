using System.Diagnostics;

namespace Modules.Transactions.Application.Common.Audit
{
    public static class InboundAudit
    {
        public static string? CurrentTraceId =>
            Activity.Current is { } activity ? activity.TraceId.ToString() : null;
    }
}