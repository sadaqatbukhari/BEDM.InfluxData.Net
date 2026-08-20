using InfluxDB.Client;

namespace InfluxData.Net.InfluxDb
{
    /// <summary>
    /// Adds modern InfluxDB 1.8+ functionality without changing the established
    /// InfluxDB 1.x client contracts.
    /// </summary>
    public interface IInfluxDbClientModern : IInfluxDbClientV2
    {
        /// <summary>
        /// Gets the official InfluxDB.Client Flux query API.
        /// InfluxDB 1.x requires Flux to be enabled and uses
        /// database/retention-policy as the bucket name in the Flux query.
        /// </summary>
        IQueryApi Flux { get; }
    }
}
