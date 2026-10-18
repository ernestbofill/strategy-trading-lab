using Npgsql;
using StrategyTradingLab.Api.Neon;

namespace StrategyTradingLab.Api.Tests;

public class NeonEnvironmentTests
{
    [Fact]
    public void Turns_a_pooled_database_url_into_a_connection_string()
    {
        var connectionString = NeonEnvironment.ToNpgsqlConnectionString(
            "postgresql://app_user:p%40ss@ep-cool-1-pooler.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.Equal("ep-cool-1-pooler.eu-central-1.aws.neon.tech", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("neondb", builder.Database);
        Assert.Equal("app_user", builder.Username);
        Assert.Equal("p@ss", builder.Password);
        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.Equal(ChannelBinding.Require, builder.ChannelBinding);
        Assert.True(builder.NoResetOnClose);
    }

    [Fact]
    public void Keeps_session_reset_on_a_direct_connection()
    {
        var builder = new NpgsqlConnectionStringBuilder(NeonEnvironment.ToNpgsqlConnectionString(
            "postgresql://u:p@ep-cool-1.eu-central-1.aws.neon.tech/neondb?sslmode=require"));

        Assert.False(builder.NoResetOnClose);
    }
}
