using Microsoft.AspNetCore.OData.Formatter.Value;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData.Edm;
using Newtonsoft.Json.Linq;
using System;

namespace maskx.OData
{
    public interface IDataSource
    {
        Configuration Configuration { get; set; }
        string Name { get; }
        EdmModel Model { get; }

        EdmEntityObjectCollection Get(ODataQueryOptions queryOptions);
        long GetCount(ODataQueryOptions queryOptions);
        EdmEntityObject Get(string key, ODataQueryOptions queryOptions);
        /// <summary>
        /// insert one row to table
        /// </summary>
        /// <param name="entity">the data of the row</param>
        /// <returns>the identity of the new recorder</returns>
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
        IEdmObject InvokeFunction(ODataQueryOptions queryOptions);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="func"></param>
        /// <param name="parameterValues"></param>
        /// <param name="queryOptions"></param>
        /// <returns></returns>
        int GetFuncResultCount(ODataQueryOptions queryOptions);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        /// <param name="parameterValues"></param>
        /// <returns></returns>
        IEdmObject DoAction(IEdmAction action, JObject parameterValues);

        Action<RequestInfo> BeforeExcute { get; set; }
        Func<RequestInfo, object, object> AfrerExcute { get; set; }
    }
}
