

namespace TransactionAggregation.Domain.Common
{
    public abstract class BaseEntity
    {
        public DateTime CreatedAt { get;  set; }
        public DateTime? UpdatedAt { get; set; }

        private readonly List<BaseDomainEvent> _domainEvents = new();
        public IReadOnlyCollection<BaseDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        protected BaseEntity()
        {
            CreatedAt = DateTime.UtcNow;
        }

        public void AddDomainEvent(BaseDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void RemoveDomainEvent(BaseDomainEvent domainEvent)
        {
            _domainEvents.Remove(domainEvent);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }
    }
}
