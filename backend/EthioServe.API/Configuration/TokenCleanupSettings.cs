using System.ComponentModel.DataAnnotations;

namespace EthioServe.API.Configuration;

public sealed class TokenCleanupSettings
{
    public const string SectionName = "Security";

    [Range(5, 1440)]
    public int TokenCleanupIntervalMinutes { get; set; } = 360;
}
