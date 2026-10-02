using System.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InfluxData.Net.Common.Infrastructure;
using InfluxData.Net.InfluxData.Helpers;
using InfluxData.Net.Kapacitor.Constants;
using InfluxData.Net.Kapacitor.Models;
using InfluxData.Net.Kapacitor.Models.Responses;
using InfluxData.Net.Kapacitor.RequestClients;
using Newtonsoft.Json;

namespace InfluxData.Net.Kapacitor.ClientModules
{
    public class TaskClientModule : ClientModuleBase, ITaskClientModule
    {
        public TaskClientModule(IKapacitorRequestClient requestClient)
            : base(requestClient)
        {
        }

        public virtual async Task<KapacitorTask> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            var response = await base.RequestClient.GetAsync(RequestPaths.Tasks, Uri.EscapeDataString(taskId), cancellationToken: cancellationToken).ConfigureAwait(false);
            var task = response.ReadAs<KapacitorTask>();

            return task;
        }

        public virtual async Task<IEnumerable<KapacitorTask>> GetTasksAsync(CancellationToken cancellationToken = default)
        {
            var response = await base.RequestClient.GetAsync(RequestPaths.Tasks, cancellationToken: cancellationToken).ConfigureAwait(false);
            var tasks = response.ReadAs<KapacitorTasks>();

            return tasks.Tasks;
        }

        public virtual async Task<IInfluxDataApiResponse> DefineTaskAsync(DefineTaskParams taskParams, CancellationToken cancellationToken = default)
        {
            var contentDict = BuildDefineTaskContentDict(taskParams);
            contentDict.Add(BodyParams.Type, taskParams.TaskType.ToString().ToLower());
            contentDict.Add(BodyParams.Script, taskParams.TickScript);
            var content = JsonConvert.SerializeObject(contentDict);

            return await base.RequestClient.PostAsync(RequestPaths.Tasks, content: content, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task<IInfluxDataApiResponse> DefineTaskAsync(DefineTemplatedTaskParams taskParams, CancellationToken cancellationToken = default)
        {
            var contentDict = BuildDefineTaskContentDict(taskParams);
            contentDict.Add(BodyParams.TemplateId, taskParams.TemplateId);
            var content = JsonConvert.SerializeObject(contentDict);

            return await base.RequestClient.PostAsync(RequestPaths.Tasks, content: content, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        protected virtual Dictionary<string, object> BuildDefineTaskContentDict(BaseTaskParams taskParams)
        {
            return new Dictionary<string, object>
            {
                { BodyParams.Id, taskParams.TaskId },
                { BodyParams.Dbrps, new List<IDictionary<string, string>>
                {
                    new Dictionary<string, string>()
                    {
                        { BodyParams.Db, taskParams.DBRPsParams.DbName },
                        { BodyParams.RetentionPolicy, taskParams.DBRPsParams.RetentionPolicy }
                    }
                }},
                { BodyParams.Vars, taskParams.TaskVars }
            };
        }

        public virtual async Task<IInfluxDataApiResponse> DeleteTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            return await base.RequestClient.DeleteAsync(RequestPaths.Tasks, Uri.EscapeDataString(taskId), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task<IInfluxDataApiResponse> EnableTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            var content = JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                { BodyParams.Status, "enabled" },
            });

            return await base.RequestClient.PatchAsync(RequestPaths.Tasks, Uri.EscapeDataString(taskId), content, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task<IInfluxDataApiResponse> DisableTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            var content = JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                { BodyParams.Status, "disabled" },
            });

            return await base.RequestClient.PatchAsync(RequestPaths.Tasks, Uri.EscapeDataString(taskId), content, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}