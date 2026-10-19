namespace SharedKernel.Common.Auditing
{
    /// <summary>Records that the signed-in user viewed one resource. Endpoints hand it the id found in a route value.</summary>
    public interface IViewAudit
    {
        /// <summary>The route value that carries the viewed resource's id.</summary>
        string RouteKey { get; }

        Task RecordAsync(Guid resourceId, string view, CancellationToken cancellationToken);
    }
}
