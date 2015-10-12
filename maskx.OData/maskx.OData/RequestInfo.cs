using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.OData.Query;

namespace maskx.OData
{
    public class RequestInfo
    {
        public RequestInfo(string dataSourceName)
        {
            this.Message = string.Empty;
            this.StatusCode = System.Net.HttpStatusCode.NotFound;
            this.Result = true;
            this.DataSourceName = dataSourceName;
        }
        public string DataSourceName { get; internal set; }
        /// <summary>
        /// Get,Create,Update,Replace,Delete,Replace,InvokeFunction
        /// </summary>
        public MethodType Method { get; internal set; }
        /// <summary>
        /// Name of Table, View or SP
        /// </summary>
        public string Target { get; internal set; }
        JObject _Parameters = null;
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
