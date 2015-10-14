using System;
using System.Collections.Generic;
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
        public async override Task<IList<ODataBatchResponseItem>> ExecuteRequestMessagesAsync(IEnumerable<ODataBatchRequestItem> requests, CancellationToken cancellationToken)
        {
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
       
    }
}
