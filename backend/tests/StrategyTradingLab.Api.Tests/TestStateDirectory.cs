using System.Runtime.CompilerServices;

namespace StrategyTradingLab.Api.Tests;

static class TestStateDirectory
{
    /// <summary>
    /// Every test host gets its own state directory, so none reads the per-user database.json of the local API
    /// (a switch to production made on the local admin page would send the tests there). A fixture may set its own.
    /// </summary>
    [ModuleInitializer]
    internal static void Isolate() =>
        Environment.SetEnvironmentVariable("STATE_DIRECTORY", Directory.CreateTempSubdirectory("stl-tests-").FullName);
}
