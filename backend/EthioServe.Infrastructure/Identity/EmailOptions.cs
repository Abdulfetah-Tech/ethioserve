namespace EthioServe.Infrastructure.Identity;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "None";

    public string Region { get; set; } = "eu-west-2";

    public string SenderAddress { get; set; } = string.Empty;
}
