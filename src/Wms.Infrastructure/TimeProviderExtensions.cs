namespace Wms.Infrastructure;

public static class TimeProviderExtensions
{
    public static DateOnly Today(this TimeProvider time) => DateOnly.FromDateTime(time.GetLocalNow().DateTime);
}
