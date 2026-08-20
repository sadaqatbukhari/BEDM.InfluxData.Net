using System;
using System.Threading.Tasks;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.Common.Infrastructure;
using InfluxData.Net.InfluxDb.Models;

namespace InfluxData.Net.InfluxDb.ClientModules
{
    internal sealed class UnsupportedDeleteClientModule : IDeleteClientModule
    {
        private readonly InfluxDbVersion _version;

        public UnsupportedDeleteClientModule(InfluxDbVersion version)
        {
            _version = version;
        }

        public Task<IInfluxDataApiResponse> DeleteAsync(DeleteRequest request, string authorizationToken = null)
        {
            throw new NotSupportedException(
                String.Format("The v2-compatible delete API requires InfluxDB 1.8 or newer; configured version is {0}.", _version));
        }
    }
}
