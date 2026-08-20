using InfluxData.Net.InfluxDb.ClientModules;

namespace InfluxData.Net.InfluxDb
{
    /// <summary>
    /// Extends the original client contract with InfluxDB 1.8+ v2-compatible APIs.
    /// </summary>
    public interface IInfluxDbClientV2 : IInfluxDbClient
    {
        /// <summary>
        /// Time-range and predicate deletion API. Supported by InfluxDB 1.8 and newer.
        /// </summary>
        IDeleteClientModule Delete { get; }
    }
}
