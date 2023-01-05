using Microsoft.AspNetCore.OData.Formatter.Value;
using Microsoft.AspNetCore.OData.Query;
using Newtonsoft.Json.Linq;
using System.Net;

namespace maskx.OData
{
    public class RequestInfo
    {
        public RequestInfo()
        {
            this.Message = string.Empty;
            this.StatusCode = HttpStatusCode.NotFound;
            this.Result = true; 
        }
        /// <summary>
        /// Get,Create,Update,Replace,Delete,Replace,InvokeFunction
        /// </summary>
        public MethodType Method { get; internal set; }
        /// <summary>
        /// Name of Table, View or SP
        /// </summary>
        public string Target { get; internal set; }
        JObject _Parameters;
        /// <summary>
        /// the parameter of function
        /// </summary>
        public JObject Parameters
        {
            get
            {
                if (_Parameters == null)
                    _Parameters = new JObject();
                return _Parameters;
            }
            internal set
            {
                _Parameters = value;
            }
        }
        public ODataQueryOptions QueryOptions { get; internal set; }
        public IEdmEntityObject Entity { get; internal set; }
        /// <summary>
        /// continue execute function when true
        /// when false,break 
        /// </summary>
        public bool Result { get; set; }
        /// <summary>
        /// the message when Result is false
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// the HttpStatusCode when Result is false
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }
    }
}
