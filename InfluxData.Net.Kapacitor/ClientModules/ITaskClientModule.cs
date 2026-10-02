using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using InfluxData.Net.Common.Infrastructure;
using InfluxData.Net.Kapacitor.Models;
using InfluxData.Net.Kapacitor.Models.Responses;

namespace InfluxData.Net.Kapacitor.ClientModules
{
    public interface ITaskClientModule
    {
        Task<KapacitorTask> GetTaskAsync(string taskId, CancellationToken cancellationToken = default);

        Task<IEnumerable<KapacitorTask>> GetTasksAsync(CancellationToken cancellationToken = default);

        Task<IInfluxDataApiResponse> DefineTaskAsync(DefineTaskParams taskParams, CancellationToken cancellationToken = default);

        Task<IInfluxDataApiResponse> DefineTaskAsync(DefineTemplatedTaskParams taskParams, CancellationToken cancellationToken = default);

        Task<IInfluxDataApiResponse> DeleteTaskAsync(string taskId, CancellationToken cancellationToken = default);

        Task<IInfluxDataApiResponse> EnableTaskAsync(string taskId, CancellationToken cancellationToken = default);

        Task<IInfluxDataApiResponse> DisableTaskAsync(string taskId, CancellationToken cancellationToken = default);
    }
}