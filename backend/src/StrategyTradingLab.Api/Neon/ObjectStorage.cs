using Amazon.Runtime;
using Amazon.S3;

namespace StrategyTradingLab.Api.Neon;

/// <summary>Neon Object Storage: S3-compatible, one bucket per neon.ts declaration, branch-scoped.</summary>
public class ObjectStorageOptions
{
    /// <summary>The bucket declared in neon.ts. Override with ObjectStorage:Bucket.</summary>
    public string Bucket { get; set; } = "assets";
}

public static class ObjectStorage
{
    static readonly string[] Variables =
        ["AWS_ENDPOINT_URL_S3", "AWS_REGION", "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY"];

    public static bool IsConfigured(IConfiguration configuration) =>
        Variables.All(name => !string.IsNullOrWhiteSpace(configuration[name]));

    /// <summary>An S3 client for the branch endpoint in the AWS_* variables.</summary>
    public static IAmazonS3 CreateClient(IConfiguration configuration)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = configuration["AWS_ENDPOINT_URL_S3"],
            AuthenticationRegion = configuration["AWS_REGION"],
            // Neon only supports path-style addressing.
            ForcePathStyle = true,
            // Only send and check checksums S3 requires; S3-compatible stores may reject the extra ones.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        var credentials = new BasicAWSCredentials(
            configuration["AWS_ACCESS_KEY_ID"], configuration["AWS_SECRET_ACCESS_KEY"]);
        return new AmazonS3Client(credentials, config);
    }
}
