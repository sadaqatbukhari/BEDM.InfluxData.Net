using System.Threading;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using InfluxData.Net.Common.Infrastructure;

namespace InfluxData.Net.Kapacitor.RequestClients
{
    public interface IKapacitorRequestClient
    {
        Task<IInfluxDataApiResponse> GetAsync(string path, CancellationToken cancellationToken = default);

        Task<IInfluxDataApiResponse> GetAsync(string path, string taskId, CancellationToken cancellationToken = default);

        Task<IInfluxDataApiResponse> GetAsync(string path, IDictionary<string, string> requestParams, CancellationToken cancellationToken = default);

        Task<IInfluxDataApiResponse> PostAsync(string path, IDictionary<string, string> requestParams = null, string content = null, CancellationToken cancellationToken = default);

        Task<IInfluxDataApiResponse> DeleteAsync(string path, string taskId, CancellationToken cancellationToken = default);

        Task<IInfluxDataApiResponse> DeleteAsync(string path, IDictionary<string, string> requestParams = null, CancellationToken cancellationToken = default);

        Task<IInfluxDataApiResponse> PatchAsync(string path, string taskId, string content = null, CancellationToken cancellationToken = default);

        Task<IInfluxDataApiResponse> RequestAsync(
            HttpMethod method,
            string path,
            IDictionary<string, string> requestParams = null,
            HttpContent content = null,
            bool includeAuthToQuery = true,
            bool headerIsBody = false, CancellationToken cancellationToken = default);
    }
}