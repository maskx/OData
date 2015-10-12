using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.OData.Batch;

namespace maskx.OData
{
    public class DynamicODataBatchHandler : DefaultODataBatchHandler
    {
        public DynamicODataBatchHandler(HttpServer httpServer)
        : base(httpServer)
        {
        }
        public override Task<HttpResponseMessage> CreateResponseMessageAsync(IEnumerable<ODataBatchResponseItem> responses, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return base.CreateResponseMessageAsync(responses, request, cancellationToken);
        }
        public async override Task<IList<ODataBatchResponseItem>> ExecuteRequestMessagesAsync(IEnumerable<ODataBatchRequestItem> requests, CancellationToken cancellationToken)
        {
            //  return base.ExecuteRequestMessagesAsync(requests, cancellationToken);
            if (requests == null) { throw new ArgumentNullException("requests"); }
            IList<ODataBatchResponseItem> responses = new List<ODataBatchResponseItem>();

            try
            {
               
                foreach (ODataBatchRequestItem request in requests)
                {
                    var changeSetResponse = await request.SendRequestAsync(Invoker, cancellationToken);
                    responses.Add(changeSetResponse);
                }

            }
            catch
            {
                foreach (ODataBatchResponseItem response in responses)
                {
                    if (response != null)
                    {
                        response.Dispose();
                    }
                }
                throw;
            }
            return responses;

        }
        public override Task<IList<ODataBatchRequestItem>> ParseBatchRequestsAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return base.ParseBatchRequestsAsync(request, cancellationToken);
        }
        public override Task<HttpResponseMessage> ProcessBatchAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return base.ProcessBatchAsync(request, cancellationToken);
        }
        public override void ValidateRequest(HttpRequestMessage request)
        {
            base.ValidateRequest(request);
        }
    }
}
