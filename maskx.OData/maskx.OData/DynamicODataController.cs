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
            var ds = HttpContext.ODataFeature().RequestContainer.GetService(typeof(IDataSource)) as IDataSource;
            var options = GetQueryOptions();
            var ri = new RequestInfo(ds.Name)
            {
                Method = MethodType.Get,
                QueryOptions = options,
                Target = options.Context.Path.Segments[0].ToString()
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
            var ds = HttpContext.ODataFeature().RequestContainer.GetService(typeof(IDataSource)) as IDataSource;
            var options = GetQueryOptions();
            var ri = new RequestInfo(ds.Name)
            {
                Method = MethodType.Get,
                QueryOptions = options,
                Target = options.Context.Path.Segments[0].ToString()
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
            var ds = HttpContext.ODataFeature().RequestContainer.GetService(typeof(IDataSource)) as IDataSource;
            var path = Request.ODataFeature().Path;

            OperationImportSegment seg = path.Segments[0] as OperationImportSegment;
            IEdmType edmType = seg.EdmType;

            IEdmType elementType = edmType.TypeKind == EdmTypeKind.Collection
                ? (edmType as IEdmCollectionType).ElementType.Definition
                : edmType;
            ODataQueryContext queryContext = new ODataQueryContext(Request.GetModel(), elementType, path);
            ODataQueryOptions queryOptions = new ODataQueryOptions(queryContext, Request);


            var ri = new RequestInfo(ds.Name)
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
            var options = GetQueryOptions();
            var ds = HttpContext.ODataFeature().RequestContainer.GetService(typeof(IDataSource)) as IDataSource;
            var path = Request.ODataFeature().Path;
            OperationImportSegment seg = path.Segments[0] as OperationImportSegment;

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
                return ds.DoAction(a, ri.Parameters);
            });

        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult GetCount()
        {
            var ds = HttpContext.ODataFeature().RequestContainer.GetService(typeof(IDataSource)) as IDataSource;
            var options = GetQueryOptions();
            var ri = new RequestInfo(ds.Name)
            {
                Method = MethodType.Count,
                QueryOptions = options,
                Target = options.Context.Path.Segments[0].ToString(),
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
            var ds = HttpContext.ODataFeature().RequestContainer.GetService(typeof(IDataSource)) as IDataSource;
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
            var ds = HttpContext.ODataFeature().RequestContainer.GetService(typeof(IDataSource)) as IDataSource;
            var ri = new RequestInfo(ds.Name)
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
            var ds = HttpContext.ODataFeature().RequestContainer.GetService(typeof(IDataSource)) as IDataSource;
            var options = GetQueryOptions();

            var ri = new RequestInfo(ds.Name)
            {
                Method = MethodType.Delete,
                Target = (options.Context.ElementType as EdmEntityType).Name
            };
            return Excute(ri, () =>
            {
                string key = GetKey();
                return ds.Delete(key, options.Context.Path.EdmType);
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
            var ds = HttpContext.ODataFeature().RequestContainer.GetService(typeof(IDataSource)) as IDataSource;
            var ri = new RequestInfo(ds.Name)
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
            var ds = HttpContext.ODataFeature().RequestContainer.GetService(typeof(IDataSource)) as IDataSource;
            var ri = new RequestInfo(ds.Name)
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
            var ds = HttpContext.ODataFeature().RequestContainer.GetService(typeof(IDataSource)) as IDataSource;

            IEdmModel model = ds.Model;
            ODataQueryContext queryContext = new ODataQueryContext(model, elementType, path);
            ODataQueryOptions queryOptions = new ODataQueryOptions(queryContext, Request);
            return queryOptions;
        }
        private IEdmEntityObject GetEdmEntityObject()
        {
            if (Request.ContentLength == 0)
                return null;
            var ds = HttpContext.ODataFeature().RequestContainer.GetService(typeof(IDataSource)) as IDataSource;
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
        ActionResult Excute(RequestInfo ri, Func<object> func, Func<object, ActionResult> result = null)
        {
            object rtv = null;
            var ds = HttpContext.ODataFeature().RequestContainer.GetService(typeof(IDataSource)) as IDataSource;
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
