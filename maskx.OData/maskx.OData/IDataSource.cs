using Microsoft.OData.Edm;
using Newtonsoft.Json.Linq;
using System;
using System.Web.OData;
using System.Web.OData.Query;

namespace maskx.OData
{
    public interface IDataSource
    {
        string Name { get; }
        EdmModel Model { get; }

        EdmEntityObjectCollection Get(ODataQueryOptions queryOptions);
        int GetCount(ODataQueryOptions queryOptions);
        EdmEntityObject Get(string key, ODataQueryOptions queryOptions);
        string Create(IEdmEntityObject entity);
        int Delete(string key, IEdmType elementType);
        int Merge(string key, IEdmEntityObject entity);
        int Replace(string key, IEdmEntityObject entity);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        /// <param name="parameterValues"></param>
        /// <param name="queryOptions"></param>
        /// <returns></returns>
        IEdmObject InvokeFunction(IEdmFunction action, JObject parameterValues, ODataQueryOptions queryOptions = null);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="func"></param>
        /// <param name="parameterValues"></param>
        /// <param name="queryOptions"></param>
        /// <returns></returns>
        int GetFuncResultCount(IEdmFunction func, JObject parameterValues, ODataQueryOptions queryOptions);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        /// <param name="parameterValues"></param>
        /// <returns></returns>
        IEdmObject DoAction(IEdmAction action, JObject parameterValues);

        Action<RequestInfo> BeforeExcute { get; set; }
        Action<RequestInfo> AfrerExcute { get; set; }
    }
}
