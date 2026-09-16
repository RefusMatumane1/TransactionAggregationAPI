using System.Reflection;
using Serilog.Core;
using Serilog.Events;
using TransactionAggregation.Application.Common.Attributes;

namespace TransactionAggregationAPI.Logging
{
    public sealed class SensitiveDataDestructuringPolicy : IDestructuringPolicy
    {
        private const string RedactedValue = "***REDACTED***";

        public bool TryDestructure(
            object value,
            ILogEventPropertyValueFactory propertyValueFactory,
            out LogEventPropertyValue? result)
        {
            var type = value.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .ToList();

            if (!properties.Any(p => p.GetCustomAttribute<SensitiveAttribute>() != null))
            {
                result = null;
                return false;
            }

            var logProperties = properties.Select(p =>
            {
                var isSensitive = p.GetCustomAttribute<SensitiveAttribute>() != null;
                var propertyValue = isSensitive
                    ? new ScalarValue(RedactedValue)
                    : propertyValueFactory.CreatePropertyValue(p.GetValue(value), destructureObjects: true);

                return new LogEventProperty(p.Name, propertyValue);
            });

            result = new StructureValue(logProperties, type.Name);
            return true;
        }
    }
}