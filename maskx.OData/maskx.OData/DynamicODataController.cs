using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Formatter.Deserialization;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;

namespace maskx.OData
{
    /// <summary>
    /// 
    /// </summary>
    public class DynamicODataController : ODataController
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult Get()
        {
            var ds = HttpContext.Items["DataSource"] as IDataSource;
            var options = GetQueryOptions();
            EdmEntityObjectCollection rtv = null;
            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(ds.Name)
                {
                    Method = MethodType.Get,
                    QueryOptions = options,
                    Target = options.Context.Path.Segments[0].ToString()
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return StatusCode((int)ri.StatusCode, ri.Message);
            }
            try
            {
                rtv = ds.Get(options);
                if (options.SelectExpand != null)
                    Request.ODataFeature().SelectExpandClause = options.SelectExpand.SelectExpandClause;
                return Ok(rtv);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode((int)HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, err);
            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult GetSimpleFunction()
        {
            var ds = HttpContext.Items["DataSource"] as IDataSource;

            var path = Request.ODataFeature().Path;

            OperationImportSegment seg = path.Segments[0] as OperationImportSegment;
            IEdmType edmType = seg.EdmType;

            IEdmType elementType = edmType.TypeKind == EdmTypeKind.Collection
                ? (edmType as IEdmCollectionType).ElementType.Definition
                : edmType;
            ODataQueryContext queryContext = new ODataQueryContext(Request.GetModel(), elementType, path);
            ODataQueryOptions queryOptions = new ODataQueryOptions(queryContext, Request);

            JObject pars = new JObject();
            foreach (var p in seg.Parameters)
            {
                try
                {
                    var n = (p.Value as ConstantNode).Value;
                    pars.Add(p.Name, new JValue(n));
                }
                catch (Exception ex)
                {
                    return StatusCode((int)HttpStatusCode.InternalServerError, ex);
                }
            }
            var ri = new RequestInfo(ds.Name)
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
                    return StatusCode((int)ri.StatusCode, ri.Message);
            }
            try
            {
                var func = (seg.OperationImports.FirstOrDefault() as EdmFunctionImport).Function;
                var b = ds.InvokeFunction(func, ri.Parameters, ri.QueryOptions);
                if (b is EdmComplexObjectCollection)
                    return StatusCode((int)HttpStatusCode.OK, b as EdmComplexObjectCollection);
                else
                    return StatusCode((int)HttpStatusCode.OK, b as EdmComplexObject);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode((int)HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, err);
            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult DoAction()
        {
            var ds = HttpContext.Items["DataSource"] as IDataSource;
            var path = Request.ODataFeature().Path;
            OperationImportSegment seg = path.Segments[0] as OperationImportSegment;
            IEdmType elementType = seg.EdmType;
            JObject jobj = null;
            string s = Request.GetRawBodyStringAsync().Result;
            if (!string.IsNullOrEmpty(s))
                jobj = JObject.Parse(s);
            if (Request.HasFormContentType)
            {

            }
            else
            {

            }
            var ri = new RequestInfo(ds.Name)
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
                    return StatusCode((int)ri.StatusCode, ri.Message);
            }
            try
            {
                IEdmAction a = null;
                foreach (var item in seg.OperationImports)
                {
                    a = item.Operation as IEdmAction;
                }
                var b = ds.DoAction(a, ri.Parameters);
                return StatusCode((int)HttpStatusCode.OK, b as EdmComplexObject);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode((int)HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, err);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult GetCount()
        {
            var ds = HttpContext.Items["DataSource"] as IDataSource;
            var options = GetQueryOptions();

            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(ds.Name)
                {
                    Method = MethodType.Count,
                    QueryOptions = options,
                    Target = options.Context.Path.Segments[0].ToString(),
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return StatusCode((int)ri.StatusCode, ri.Message);
            }
            try
            {
                int count = ds.GetCount(options);
                return StatusCode((int)HttpStatusCode.OK, count);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode((int)HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, err);
            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult GetFuncResultCount()
        {
            var ds = HttpContext.Items["DataSource"] as IDataSource;
            var options = GetQueryOptions();
            var path = Request.ODataFeature().Path;
            OperationImportSegment seg = path.Segments[0] as OperationImportSegment;

            JObject pars = new JObject();

            foreach (var p in seg.Parameters)
            {
                try
                {
                    var n = (p.Value as ConstantNode).Value;
                    pars.Add(p.Name, new JValue(n));
                }
                catch (Exception ex)
                {
                    return StatusCode((int)HttpStatusCode.InternalServerError, ex);
                }
            }

            var ri = new RequestInfo(ds.Name)
            {
                Method = MethodType.Count,
                Parameters = pars,
                Target = seg.Identifier,
                QueryOptions = options
            };
            if (ds.BeforeExcute != null)
            {
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return StatusCode((int)ri.StatusCode, ri.Message); ;
            }
            try
            {
                var func = (seg.OperationImports.FirstOrDefault() as EdmFunctionImport).Function;
                var count = ds.GetFuncResultCount(func, ri.Parameters, ri.QueryOptions);
                return StatusCode((int)HttpStatusCode.OK, count);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode((int)HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, err);
            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult GetByKey()
        {
            var ds = HttpContext.Items["DataSource"] as IDataSource;
            var options = GetQueryOptions();
            string key = GetKey();

            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(ds.Name)
                {
                    Method = MethodType.Get,
                    QueryOptions = options,
                    Target = options.Context.Path.Segments[0].ToString()
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return StatusCode((int)ri.StatusCode, ri.Message);
            }
            try
            {
                var b = ds.Get(key, options);
                return StatusCode((int)HttpStatusCode.OK, b);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode((int)HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, err);
            }

        }

        public ActionResult Post()
        {
            var entity = GetEdmEntityObject();
            if (entity == null)
                return StatusCode((int)HttpStatusCode.BadRequest, "entity cannot be empty");

            string rtv = string.Empty;
            var ds = HttpContext.Items["DataSource"] as IDataSource;
            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(ds.Name)
                {
                    Method = MethodType.Create,
                    Target = (entity.GetEdmType().Definition as EdmEntityType).Name,
                    Entity = entity
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return StatusCode((int)ri.StatusCode, ri.Message);
            }
            try
            {
                rtv = ds.Create(entity);
                return StatusCode((int)HttpStatusCode.Created, rtv);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode((int)HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, err);
            }

        }
        public ActionResult Delete()
        {
            var ds = HttpContext.Items["DataSource"] as IDataSource;
            var options = GetQueryOptions();
            string key = GetKey();
            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(ds.Name)
                {
                    Method = MethodType.Delete,
                    Target = (options.Context.ElementType as EdmEntityType).Name
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return StatusCode((int)ri.StatusCode, ri.Message);
            }
            try
            {
                var count = ds.Delete(key, options.Context.Path.EdmType);
                return StatusCode((int)HttpStatusCode.OK, count);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode((int)HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, err);
            }

        }
        public ActionResult Patch()
        {
            var entity = GetEdmEntityObject();
            if (entity == null)
                return StatusCode((int)HttpStatusCode.BadRequest, "entity cannot be empty.");
            var ds = HttpContext.Items["DataSource"] as IDataSource;
            string key = GetKey();
            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(ds.Name)
                {
                    Method = MethodType.Merge,
                    Target = (entity.GetEdmType().Definition as EdmEntityType).Name,
                    Entity = entity
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return StatusCode((int)ri.StatusCode, ri.Message);
            }
            try
            {
                var count = ds.Merge(key, entity);
                return StatusCode((int)HttpStatusCode.OK, count);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode((int)HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, err);
            }

        }
        public ActionResult Put()
        {
            var entity = GetEdmEntityObject();
            if (entity == null)
                return StatusCode((int)HttpStatusCode.BadRequest, "entity cannot be empty.");
            var ds = HttpContext.Items["DataSource"] as IDataSource;
            string key = GetKey();

            if (ds.BeforeExcute != null)
            {
                var ri = new RequestInfo(ds.Name)
                {
                    Method = MethodType.Replace,
                    Target = (entity.GetEdmType().Definition as EdmEntityType).Name,
                    Entity = entity
                };
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return StatusCode((int)ri.StatusCode, ri.Message);
            }
            try
            {
                var count = ds.Replace(key, entity);
                return StatusCode((int)HttpStatusCode.OK, count);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode((int)HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, err);
            }

        }

        string GetKey()
        {
            string key = string.Empty;
            var path = Request.ODataFeature().Path;
            foreach (var item in (path.Segments[1] as KeySegment).Keys)
            {
                key = item.Value.ToString();
            }
            return key;
        }

        ODataQueryOptions GetQueryOptions()
        {
            var path = Request.ODataFeature().Path;
            IEdmType edmType = path.Segments[0].EdmType;
            IEdmType elementType = edmType.TypeKind == EdmTypeKind.Collection
                ? (edmType as IEdmCollectionType).ElementType.Definition
                : edmType;
            IDataSource ds = HttpContext.Items["DataSource"] as IDataSource;
            IEdmModel model = ds.Model;
            ODataQueryContext queryContext = new ODataQueryContext(model, elementType, path);
            ODataQueryOptions queryOptions = new ODataQueryOptions(queryContext, Request);
            return queryOptions;
        }
        private IEdmEntityObject GetEdmEntityObject()
        {
            if (Request.ContentLength == 0)
                return null;
            var ds = HttpContext.Items["DataSource"] as IDataSource;
            var path = Request.ODataFeature().Path;
            IEdmTypeReference edmTypeReference = null;
            if (path.EdmType is EdmCollectionType edmType)
            {
                edmTypeReference = edmType.ElementType;
            }
            else
            {
                if (path.EdmType is EdmEntityType edmEntityType)
                    edmTypeReference = new EdmEntityTypeReference(edmEntityType, false);
            }

            if (edmTypeReference == null)
                return null;
            var p = HttpContext.ODataFeature().RequestContainer.GetService(typeof(ODataDeserializerProvider)) as DefaultODataDeserializerProvider;
            var deserializer = p.GetEdmTypeDeserializer(edmTypeReference) as ODataResourceDeserializer;
            InMemoryMessage message = new InMemoryMessage(Request);
            ODataMessageReaderSettings settings = new ODataMessageReaderSettings();
            ODataMessageReader reader = new ODataMessageReader((IODataRequestMessage)message, settings, ds.Model);
            IEdmEntityObject entity = deserializer.Read(reader, typeof(EdmEntityObject), new ODataDeserializerContext()
            {
                Model = ds.Model,
                Request = Request,
                Path = path,
                ResourceType = typeof(EdmEntityObject)
            }) as IEdmEntityObject;
            return entity;
        }
    }
}
