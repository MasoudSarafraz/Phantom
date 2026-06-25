using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Phantom.Core.StronglyTypedIds;
using System.Reflection;

namespace Phantom.Data.Extensions;

public static class StronglyTypedIdConverterExtensions
{
    public static ModelBuilder UseStronglyTypedIdConversions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType is null) continue;

                if (!IsStronglyTypedId(property.ClrType, out var valueType) || valueType is null) continue;

                var converter = CreateConverter(property.ClrType, valueType);
                if (converter is not null)
                    property.SetValueConverter(converter);
            }
        }

        return modelBuilder;
    }

    private static bool IsStronglyTypedId(Type type, out Type? valueType)
    {
        valueType = null;
        if (type is null || type.IsAbstract) return false;

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>))
            {
                valueType = iface.GetGenericArguments()[0];
                return true;
            }
        }
        return false;
    }

    private static ValueConverter? CreateConverter(Type stronglyTypedIdType, Type valueType)
    {
        var converterType = typeof(StronglyTypedIdValueConverter<,>)
            .MakeGenericType(stronglyTypedIdType, valueType);
        return (ValueConverter?)Activator.CreateInstance(converterType);
    }
}

internal sealed class StronglyTypedIdValueConverter<TId, TValue> : ValueConverter<TId, TValue>
    where TId : IStronglyTypedId<TValue>
    where TValue : notnull
{
    public StronglyTypedIdValueConverter()
        : base(
            id => id.Value,
            value => FromValue(value))
    {
    }

    public static TId FromValue(TValue value)
    {
        if (typeof(TId) == typeof(GuidId) && typeof(TValue) == typeof(Guid))
            return (TId)(object)new GuidId((Guid)(object)value);

        if (typeof(TId) == typeof(IntId) && typeof(TValue) == typeof(int))
            return (TId)(object)new IntId((int)(object)value);

        if (typeof(TId) == typeof(LongId) && typeof(TValue) == typeof(long))
            return (TId)(object)new LongId((long)(object)value);

        if (typeof(TId) == typeof(StringId) && typeof(TValue) == typeof(string))
            return (TId)(object)new StringId((string)(object)value);

        var ctor = typeof(TId).GetConstructor(new[] { typeof(TValue) });
        if (ctor is null)
            throw new InvalidOperationException(
                $"Type {typeof(TId).Name} must have a public constructor accepting {typeof(TValue).Name}.");

        return (TId)ctor.Invoke(new object[] { value! });
    }
}
