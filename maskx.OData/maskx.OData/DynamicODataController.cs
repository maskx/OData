using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Batch;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Formatter.Deserialization;
using Microsoft.AspNetCore.OData.Formatter.Value;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.Options;
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
        readonly DynamicODataOptions _Options;
        public DynamicODataController(IOptions<DynamicODataOptions> options)
        {
            _Options = options?.Value;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult Get()
        {
            var feature = HttpContext.ODataFeature();
            var ds = _Options.DataSources[feature.RoutePrefix];
            var options = GetQueryOptions();
            var ri = new RequestInfo()
            {
                Method = MethodType.Get,
                QueryOptions = options,
                Target = options.Context.Path.FirstSegment.ToString()
            };
            return Excute(ri, () =>
            {
                if (options.SelectExpand != null)
                    Request.ODataFeature().SelectExpandClause = options.SelectExpand.SelectExpandClause;
                return ds.Get(options);
            });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult GetByKey()
        {
            var feature = HttpContext.ODataFeature();
            // todo: when key data type wrong in query string, feature.PrefixName will be null
            var ds = _Options.DataSources[feature.RoutePrefix];

            var options = GetQueryOptions();
            var ri = new RequestInfo()
            {
                Method = MethodType.Get,
                QueryOptions = options,
                Target = options.Context.Path.FirstSegment.ToString()
            };
            return Excute(ri, () =>
            {
                if (options.SelectExpand != null)
                    Request.ODataFeature().SelectExpandClause = options.SelectExpand.SelectExpandClause;
                string key = GetKey();

                return ds.Get(key, options);
            });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult GetSimpleFunction()
        {
            var ds = _Options.DataSources[Request.ODataFeature().RoutePrefix];
            var path = Request.ODataFeature().Path;

            OperationImportSegment seg = path.FirstSegment as OperationImportSegment;
            IEdmType edmType = seg.EdmType;

            IEdmType elementType = edmType.TypeKind == EdmTypeKind.Collection
                ? (edmType as IEdmCollectionType).ElementType.Definition
                : edmType;
            ODataQueryContext queryContext = new(Request.GetModel(), elementType, path);
            ODataQueryOptions queryOptions = new(queryContext, Request);


            var ri = new RequestInfo()
            {
                Method = MethodType.Function,
                Target = seg.Identifier,
                QueryOptions = queryOptions
            };
            return Excute(ri, () =>
            {
                return ds.InvokeFunction(ri.QueryOptions);

            });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult DoAction()
        {
            var ds = _Options.DataSources[Request.ODataFeature().RoutePrefix];
            var path = Request.ODataFeature().Path;
            OperationImportSegment seg = path.FirstSegment as OperationImportSegment;

            JObject jobj = null;
            string s = Request.GetRawBodyStringAsync().Result;
            if (!string.IsNullOrEmpty(s) && s != "null")
                jobj = JObject.Parse(s);
            var ri = new RequestInfo()
            {
                Method = MethodType.Action,
                Target = seg.Identifier,
                QueryOptions = null
            };
            return Excute(ri, () =>
            {
                IEdmAction a = null;
                foreach (var item in seg.OperationImports)
                {
                    a = item.Operation as IEdmAction;
                }
                return ds.DoAction(a, jobj);
            });

        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult GetCount()
        {
            var ds = _Options.DataSources[Request.ODataFeature().RoutePrefix];
            var options = GetQueryOptions();
            var ri = new RequestInfo()
            {
                Method = MethodType.Count,
                QueryOptions = options,
                Target = options.Context.Path.FirstSegment.ToString(),
            };
            return Excute(ri, () =>
            {
                return ds.GetCount(options);
            });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult GetFuncResultCount()
        {
            var ds = _Options.DataSources[Request.ODataFeature().RoutePrefix];
            var options = GetQueryOptions();
            var path = Request.ODataFeature().Path;
            OperationImportSegment seg = path.FirstSegment as OperationImportSegment;
            JObject pars = new();
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

            var ri = new RequestInfo()
            {
                Method = MethodType.Count,
                Parameters = pars,
                Target = seg.Identifier,
                QueryOptions = options
            };
            return Excute(ri, () =>
            {
                return ds.GetFuncResultCount(ri.QueryOptions);
            });
        }

        public ActionResult Post()
        {
            var entity = GetEdmEntityObject();
            if (entity == null)
                return StatusCode((int)HttpStatusCode.BadRequest, "entity cannot be empty");
            var ds = _Options.DataSources[Request.ODataFeature().RoutePrefix];
            var ri = new RequestInfo()
            {
                Method = MethodType.Create,
                Target = (entity.GetEdmType().Definition as EdmEntityType).Name,
                Entity = entity
            };
            return Excute(ri, () =>
            {
                return ds.Create(entity);
            }, (rtv) =>
            {
                return StatusCode((int)HttpStatusCode.Created, rtv);
            });
        }
        public ActionResult Delete()
        {
            var ds = _Options.DataSources[Request.ODataFeature().RoutePrefix];
            var options = GetQueryOptions();

            var ri = new RequestInfo()
            {
                Method = MethodType.Delete,
                Target = (options.Context.ElementType as EdmEntityType).Name
            };
            return Excute(ri, () =>
            {
                string key = GetKey();
                return ds.Delete(key, options.Context.Path.GetEdmType());
            }, (rtv) =>
            {
                if ((int)rtv == 1)
                    return StatusCode((int)HttpStatusCode.NoContent);
                return StatusCode((int)HttpStatusCode.RequestedRangeNotSatisfiable, rtv);
            });

        }
        public ActionResult Patch()
        {
            var entity = GetEdmEntityObject();
            if (entity == null)
                return StatusCode((int)HttpStatusCode.BadRequest, "entity cannot be empty.");
            var ds = _Options.DataSources[Request.ODataFeature().RoutePrefix];
            var ri = new RequestInfo()
            {
                Method = MethodType.Merge,
                Target = (entity.GetEdmType().Definition as EdmEntityType).Name,
                Entity = entity
            };
            return Excute(ri, () =>
            {
                string key = GetKey();
                return ds.Merge(key, entity);
            }, (rtv) =>
            {
                if ((int)rtv == 1)
                    return StatusCode((int)HttpStatusCode.NoContent);
                return StatusCode((int)HttpStatusCode.RequestedRangeNotSatisfiable, rtv);
            });
        }
        public ActionResult Put()
        {
            var entity = GetEdmEntityObject();
            if (entity == null)
                return StatusCode((int)HttpStatusCode.BadRequest, "entity cannot be empty.");
            var ds = _Options.DataSources[Request.ODataFeature().RoutePrefix];
            var ri = new RequestInfo()
            {
                Method = MethodType.Replace,
                Target = (entity.GetEdmType().Definition as EdmEntityType).Name,
                Entity = entity
            };
            return Excute(ri, () =>
            {
                string key = GetKey();
                return ds.Replace(key, entity);
            }, (rtv) =>
            {
                if ((int)rtv == 1)
                    return StatusCode((int)HttpStatusCode.NoContent);
                return StatusCode((int)HttpStatusCode.RequestedRangeNotSatisfiable, rtv);
            });
        }
        string GetKey()
        {
            string key = string.Empty;
            var path = Request.ODataFeature().Path;
            // todo: need get keys
            KeySegment keySegment = path.First((e) => e is KeySegment) as KeySegment;
            foreach (var item in keySegment.Keys)
            {
                key = item.Value.ToString();
            }
            return key;
        }
        ODataQueryOptions GetQueryOptions()
        {
            var path = Request.ODataFeature().Path;
            IEdmType edmType = path.FirstSegment.EdmType;
            IEdmType elementType = edmType.TypeKind == EdmTypeKind.Collection
                ? (edmType as IEdmCollectionType).ElementType.Definition
                : edmType;
            IEdmModel model = Request.ODataFeature().Model;
            ODataQueryContext queryContext = new(model, elementType, path);
            ODataQueryOptions queryOptions = new(queryContext, Request);
            return queryOptions;
        }
        private IEdmEntityObject GetEdmEntityObject()
        {
            if (Request.ContentLength == 0)
                return null;
            var ds = _Options.DataSources[Request.ODataFeature().RoutePrefix];
            var path = Request.ODataFeature().Path;
            IEdmTypeReference edmTypeReference = null;
            if (path.GetEdmType() is EdmCollectionType edmType)
            {
                edmTypeReference = edmType.ElementType;
            }
            else
            {
                if (path.GetEdmType() is EdmEntityType edmEntityType)
                    edmTypeReference = new EdmEntityTypeReference(edmEntityType, false);
            }

            if (edmTypeReference == null)
                return null;
            IServiceProvider requestContainer = Request.CreateRouteServices(Request.ODataFeature().RoutePrefix);
            ODataMessageReader reader = Request.GetODataMessageReader(requestContainer);
            var p = Request.GetDeserializerProvider();
            var deserializer = p.GetEdmTypeDeserializer(edmTypeReference) as ODataResourceDeserializer;
           
            IEdmEntityObject entity = deserializer.ReadAsync(reader, typeof(EdmEntityObject), new ODataDeserializerContext()
            {
                Model = ds.Model,
                Request = Request,
                Path = path,
                ResourceType = typeof(EdmEntityObject)
            }).Result as IEdmEntityObject;
            return entity;
        }
        ActionResult Excute(RequestInfo ri, Func<object> func, Func<object, ActionResult> result = null)
        {
            object rtv = null;
            var ds = _Options.DataSources[HttpContext.ODataFeature().RoutePrefix];
            var options = GetQueryOptions();
            if (ds.BeforeExcute != null)
            {
                ds.BeforeExcute(ri);
                if (!ri.Result)
                    return StatusCode((int)ri.StatusCode, ri.Message);
            }
            try
            {
                rtv = func();
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode((int)HttpStatusCode.Unauthorized, ex);
            }
            catch (Exception err)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, err);
            }
            if (ds.AfrerExcute != null)
                rtv = ds.AfrerExcute(ri, rtv);
            if (result == null)
                return Ok(rtv);
            else
                return result(rtv);
        }
    }
}
