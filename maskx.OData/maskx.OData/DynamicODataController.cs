using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.OData;
using System.Web.OData.Extensions;
using System.Web.OData.Query;
using System.Web.OData.Routing;

namespace maskx.OData
{
    public class DynamicODataController : ODataController
    {
        public HttpResponseMessage Get()
        {
            string dsName = (string)Request.Properties[Constants.ODataDataSource];
            var ds = DataSourceProvider.GetDataSource(dsName);
            var options = BuildQueryOptions();
            EdmEntityObjectCollection rtv = null;
            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(dsName)
                {
                    Method = MethodType.Get,
                    QueryOptions = options,
                    Target = options.Context.Path.Segments[0].ToString()
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return Request.CreateResponse(ri.StatusCode, ri.Message);
            }
            try
            {
                rtv = ds.Get(options);
                if (options.SelectExpand != null)
                    Request.ODataProperties().SelectExpandClause = options.SelectExpand.SelectExpandClause;
                return Request.CreateResponse(HttpStatusCode.OK, rtv);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, err);
            }

        }
        public HttpResponseMessage GetSimpleFunction()
        {
            var path = Request.ODataProperties().Path;

            OperationImportSegment seg = path.Segments.FirstOrDefault() as OperationImportSegment;
            IEdmType edmType = seg.EdmType;

            IEdmType elementType = edmType.TypeKind == EdmTypeKind.Collection
                ? (edmType as IEdmCollectionType).ElementType.Definition
                : edmType;
            ODataQueryContext queryContext = new ODataQueryContext(Request.GetModel(), elementType, path);
            ODataQueryOptions queryOptions = new ODataQueryOptions(queryContext, Request);

            string dsName = (string)Request.Properties[Constants.ODataDataSource];
            var ds = DataSourceProvider.GetDataSource(dsName);
            JObject pars = new JObject();
            foreach (var p in seg.Parameters)
            {
                try
                {
                    var n = seg.GetParameterValue(p.Name);
                    pars.Add(p.Name, new JValue(n));
                }
                catch { }
            }
            var ri = new RequestInfo(dsName)
            {
                Method = MethodType.Function,
                Parameters = pars,
                Target = seg.Identifier,
                QueryOptions = queryOptions
            };
            if (ds.BeforeExcute != null)
            {
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return Request.CreateResponse(ri.StatusCode, ri.Message);
            }
            try
            {
                var b = ds.InvokeFunction(null, ri.Parameters, ri.QueryOptions);
                if (b is EdmComplexObjectCollection)
                    return Request.CreateResponse(HttpStatusCode.OK, b as EdmComplexObjectCollection);
                else
                    return Request.CreateResponse(HttpStatusCode.OK, b as EdmComplexObject);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, err);
            }

        }
        public HttpResponseMessage DoAction()
        {
            var path = Request.ODataProperties().Path;
            OperationImportSegment seg = path.Segments.FirstOrDefault() as OperationImportSegment;
            IEdmType elementType = seg.EdmType;
            JObject jobj = null;
            if (Request.Content.IsFormData())
            {
                jobj = Request.Content.ReadAsAsync<JObject>().Result;
            }
            else
            {
                string s = Request.Content.ReadAsStringAsync().Result;
                if (!string.IsNullOrEmpty(s))
                    jobj = JObject.Parse(s);
            }
            string dsName = (string)Request.Properties[Constants.ODataDataSource];
            var ds = DataSourceProvider.GetDataSource(dsName);
            var ri = new RequestInfo(dsName)
            {
                Method = MethodType.Action,
                Parameters = jobj,
                Target = seg.Identifier,
                QueryOptions = null
            };
            if (ds.BeforeExcute != null)
            {
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return Request.CreateResponse(ri.StatusCode, ri.Message);
            }
            try
            {
                var b = ds.DoAction(null, ri.Parameters);
                return Request.CreateResponse(HttpStatusCode.OK, b as EdmComplexObject);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, err);
            }
        }
        public HttpResponseMessage GetCount()
        {
            string dsName = (string)Request.Properties[Constants.ODataDataSource];
            var ds = DataSourceProvider.GetDataSource(dsName);
            var options = BuildQueryOptions();
            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(dsName)
                {
                    Method = MethodType.Count,
                    QueryOptions = options,
                    Target = options.Context.Path.Segments[0].ToString(),
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return Request.CreateResponse(ri.StatusCode, ri.Message);
            }
            try
            {
                int count = ds.GetCount(options);
                return Request.CreateResponse(HttpStatusCode.OK, count);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, err);
            }

        }
        public HttpResponseMessage GetFuncResultCount()
        {
            var path = Request.ODataProperties().Path;
            OperationImportSegment seg = path.Segments.FirstOrDefault() as OperationImportSegment;
            IEdmType edmType = seg.EdmType;
            IEdmType elementType = edmType.TypeKind == EdmTypeKind.Collection
                ? (edmType as IEdmCollectionType).ElementType.Definition
                : edmType;
            ODataQueryContext queryContext = new ODataQueryContext(Request.GetModel(), elementType, path);
            ODataQueryOptions queryOptions = new ODataQueryOptions(queryContext, Request);
            JObject pars;
            if (Request.Method == HttpMethod.Get)
            {
                pars = new JObject();
                foreach (var p in seg.Parameters)
                {
                    try
                    {
                        var n = seg.GetParameterValue(p.Name);
                        pars.Add(p.Name, new JValue(n));
                    }
                    catch { }
                }
            }
            else
            {
                pars = Request.Content.ReadAsAsync<JObject>().Result;
            }
            string dsName = (string)Request.Properties[Constants.ODataDataSource];
            var ds = DataSourceProvider.GetDataSource(dsName);
            var ri = new RequestInfo(dsName)
            {
                Method = MethodType.Count,
                Parameters = pars,
                Target = seg.Identifier,
                QueryOptions = queryOptions
            };
            if (ds.BeforeExcute != null)
            {
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return Request.CreateResponse(ri.StatusCode, ri.Message); ;
            }
            try
            {
                var count = ds.GetFuncResultCount(null, ri.Parameters, ri.QueryOptions);
                return Request.CreateResponse(HttpStatusCode.OK, count);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, err);
            }

        }
        //Get entityset(key)
        public HttpResponseMessage Get(string key)
        {
            var options = BuildQueryOptions();
            string dsName = (string)Request.Properties[Constants.ODataDataSource];
            var ds = DataSourceProvider.GetDataSource(dsName);
            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(dsName)
                {
                    Method = MethodType.Get,
                    QueryOptions = options,
                    Target = options.Context.Path.Segments[0].ToString()
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return Request.CreateResponse(ri.StatusCode, ri.Message);
            }
            try
            {
                var b = ds.Get(key, options);
                return Request.CreateResponse(HttpStatusCode.OK, b);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, err);
            }

        }
        public HttpResponseMessage Post(IEdmEntityObject entity)
        {
            if (entity == null)
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "entity cannot be empty");
            var path = Request.ODataProperties().Path;
            IEdmType edmType = path.EdmType;
            if (edmType.TypeKind != EdmTypeKind.Collection)
            {
                throw new Exception("we are serving POST {entityset}");
            }
            string rtv = null;
            string dsName = (string)Request.Properties[Constants.ODataDataSource];
            var ds = DataSourceProvider.GetDataSource(dsName);
            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(dsName)
                {
                    Method = MethodType.Create,
                    Target = (entity.GetEdmType().Definition as EdmEntityType).Name,
                    Entity = entity
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return Request.CreateResponse(ri.StatusCode, ri.Message);
            }
            try
            {
                rtv = ds.Create(entity);
                return Request.CreateResponse(HttpStatusCode.Created, rtv);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, err);
            }

        }
        public HttpResponseMessage Delete(string key)
        {
            var path = Request.ODataProperties().Path;
            var edmType = path.Segments[0].EdmType;
            var edmEntityType = ((EdmCollectionType)edmType).ElementType.Definition;
            string dsName = (string)Request.Properties[Constants.ODataDataSource];
            var ds = DataSourceProvider.GetDataSource(dsName);
            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(dsName)
                {
                    Method = MethodType.Delete,
                    Target = (edmEntityType as EdmEntityType).Name
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return Request.CreateResponse(ri.StatusCode, ri.Message);
            }
            try
            {
                var count = ds.Delete(key, edmEntityType);
                return Request.CreateResponse(HttpStatusCode.OK, count);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, err);
            }

        }
        public HttpResponseMessage Patch(string key, IEdmEntityObject entity)
        {
            string dsName = (string)Request.Properties[Constants.ODataDataSource];
            if (entity == null)
                return Request.CreateResponse(HttpStatusCode.BadRequest, "entity cannot be empty.");
            var ds = DataSourceProvider.GetDataSource(dsName);
            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(dsName)
                {
                    Method = MethodType.Merge,
                    Target = (entity.GetEdmType().Definition as EdmEntityType).Name,
                    Entity = entity
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return Request.CreateResponse(ri.StatusCode, ri.Message);
            }
            try
            {
                var count = ds.Merge(key, entity);
                return Request.CreateResponse(HttpStatusCode.OK, count);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, err);
            }

        }
        public HttpResponseMessage Put(string key, IEdmEntityObject entity)
        {
            string dsName = (string)Request.Properties[Constants.ODataDataSource];
            var ds = DataSourceProvider.GetDataSource(dsName);
            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(dsName)
                {
                    Method = MethodType.Replace,
                    Target = (entity.GetEdmType().Definition as EdmEntityType).Name,
                    Entity = entity
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return Request.CreateResponse(ri.StatusCode, ri.Message);
            }
            try
            {
                var count = ds.Replace(key, entity);
                return Request.CreateResponse(HttpStatusCode.OK, count);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, err);
            }

        }


        ODataQueryOptions BuildQueryOptions()
        {
            var path = Request.ODataProperties().Path;
            IEdmType edmType = path.Segments[0].EdmType;
            IEdmType elementType = edmType.TypeKind == EdmTypeKind.Collection
                ? (edmType as IEdmCollectionType).ElementType.Definition
                : edmType;
            ODataQueryContext queryContext = new ODataQueryContext(Request.GetModel(), elementType, path);
            ODataQueryOptions queryOptions = new ODataQueryOptions(queryContext, Request);
            return queryOptions;
        }
    }
}
