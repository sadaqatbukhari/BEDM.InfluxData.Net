using System.Threading.Tasks;
using InfluxData.Net.Common.Infrastructure;
using InfluxData.Net.InfluxDb.Models;

namespace InfluxData.Net.InfluxDb.ClientModules
{
    public interface IDeleteClientModule
    {
        /// <summary>
        /// Deletes points in an inclusive-start, exclusive-stop time range using the
        /// v2-compatible delete API available in InfluxDB 1.8 and newer.
        /// </summary>
        /// <param name="request">Delete request.</param>
        /// <param name="authorizationToken">
        /// Optional InfluxDB 2.x API token. When omitted, InfluxDB 1.x credentials are sent
        /// as Token username:password.
        /// </param>
        Task<IInfluxDataApiResponse> DeleteAsync(DeleteRequest request, string authorizationToken = null);
    }
}
