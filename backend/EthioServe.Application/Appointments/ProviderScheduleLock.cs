namespace EthioServe.Application.Appointments;

public static class ProviderScheduleLock
{
    public static string Key(Guid providerId) => $"provider-schedule:{providerId:N}";
}
