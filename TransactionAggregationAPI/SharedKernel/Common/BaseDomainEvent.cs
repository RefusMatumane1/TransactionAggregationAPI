using MediatR;

namespace SharedKernel.Common
{
    public abstract class BaseDomainEvent : INotification
    {
        public DateTime OccurredOn { get; protected set; } = DateTime.UtcNow;
    }
}
