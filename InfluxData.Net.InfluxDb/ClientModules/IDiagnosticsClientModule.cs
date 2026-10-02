using System.Threading;
using System.Threading.Tasks;
using InfluxData.Net.InfluxDb.Models.Responses;

namespace InfluxData.Net.InfluxDb.ClientModules
{
    public interface IDiagnosticsClientModule
    {
        /// <summary>
        /// Pings the InfluxDb server.
        /// </summary>
        /// <returns>Response of the ping execution (success, dbVersion, response time).</returns>
        Task<Pong> PingAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets node statistics.
        /// </summary>
        /// <returns></returns>
        Task<Stats> GetStatsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets node diagnostics. This returns information such as build information, uptime, 
        /// hostname, server configuration, memory usage, and Go runtime diagnostics.
        /// </summary>
        /// <returns></returns>
        Task<Diagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken = default);
    }
}