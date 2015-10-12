using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.OData.Batch;
using System.Web.OData.Extensions;
using System.Web.OData.Routing;
using System.Web.OData.Routing.Conventions;

namespace maskx.OData
{
    public static class Extensions
    {
        public static ODataRoute MapDynamicODataServiceRoute(
            this HttpRouteCollection routes,
            string routeName,
            string routePrefix)
        {
            IList<IODataRoutingConvention> routingConventions = ODataRoutingConventions.CreateDefault();
            routingConventions.Insert(0, new DynamicODataRoutingConvention());
            return MapDynamicODataServiceRoute(
                routes,
                routeName,
                routePrefix,
                GetModelFuncFromRequest(),
                new DefaultODataPathHandler(),
                routingConventions,
                batchHandler: null);
        }
        public static ODataRoute MapDynamicODataServiceRoute(
           this HttpRouteCollection routes,
           string routeName,
           string routePrefix,
           HttpServer httpServer)
        {
            IList<IODataRoutingConvention> routingConventions = ODataRoutingConventions.CreateDefault();
            routingConventions.Insert(0, new DynamicODataRoutingConvention());
            return MapDynamicODataServiceRoute(
                routes,
                routeName,
                routePrefix,
                GetModelFuncFromRequest(),
                new DefaultODataPathHandler(),
                routingConventions,
                batchHandler: new DynamicODataBatchHandler(httpServer));
        }
        private static ODataRoute MapDynamicODataServiceRoute(
            HttpRouteCollection routes,
            string routeName,
            string routePrefix,
            Func<HttpRequestMessage, IEdmModel> modelProvider,
            IODataPathHandler pathHandler,
            IEnumerable<IODataRoutingConvention> routingConventions,
            ODataBatchHandler batchHandler)
        {
            if (!string.IsNullOrEmpty(routePrefix))
            {
                int prefixLastIndex = routePrefix.Length - 1;
                if (routePrefix[prefixLastIndex] == '/')
                {
                    routePrefix = routePrefix.Substring(0, routePrefix.Length - 1);
                }
            }
            if (batchHandler != null)
            {
                batchHandler.ODataRouteName = routeName;
                string batchTemplate = string.IsNullOrEmpty(routePrefix)
                    ? ODataRouteConstants.Batch
                    : routePrefix + '/' + ODataRouteConstants.Batch;
                routes.MapHttpBatchRoute(routeName + "Batch", batchTemplate, batchHandler);
            }
            DynamicODataPathRouteConstraint routeConstraint = new DynamicODataPathRouteConstraint(
                  pathHandler,
                  modelProvider,
                  routeName,
                  routingConventions);
            DynamicODataRoute odataRoute = new DynamicODataRoute(routePrefix, routeConstraint);
            routes.Add(routeName, odataRoute);
           
            return odataRoute;
        }
        private static Func<HttpRequestMessage, IEdmModel> GetModelFuncFromRequest()
        {
            return request =>
            {
                string odataPath = request.Properties[Constants.CustomODataPath] as string ?? string.Empty;
                string[] segments = odataPath.Split('/');
                string dataSource = segments[0];
                request.Properties[Constants.ODataDataSource] = dataSource;
                IEdmModel model = DataSourceProvider.GetEdmModel(dataSource);
                request.Properties[Constants.CustomODataPath] = string.Join("/", segments, 1, segments.Length - 1);
                return model;
            };
        }
        internal static IEdmType GetEdmType(this System.Web.OData.Routing.ODataPath path)
        {
            return path.Segments[0].GetEdmType(path.EdmType);

        }
        internal static IEdmType GetEdmType(this HttpRequestMessage requset)
        {
            var path = requset.ODataProperties().Path;
            return path.GetEdmType();
        }
        public static object ChangeType(this object v, Type t)
        {
            if (v == null || Convert.IsDBNull(v))
                return null;
            else
            {
                try
                {
                    return Convert.ChangeType(v, t);
                }
                catch
                {
                    if (t == typeof(Guid))
                    {
                        Guid g;
                        if (Guid.TryParse(v.ToString(), out g))
                            return g;
                    }
                }
            }
            return null;
        }
        public static Type ToClrType(this EdmPrimitiveTypeKind t)
        {
            switch (t)
            {
                case EdmPrimitiveTypeKind.Binary:
                    break;
                case EdmPrimitiveTypeKind.Boolean:
                    return typeof(bool);
                case EdmPrimitiveTypeKind.Byte:
                    return typeof(Byte);
                case EdmPrimitiveTypeKind.Date:
                    break;
                case EdmPrimitiveTypeKind.DateTimeOffset:
                    return typeof(DateTime);
                case EdmPrimitiveTypeKind.Decimal:
                    return typeof(decimal);
                case EdmPrimitiveTypeKind.Double:
                    return typeof(double);
                case EdmPrimitiveTypeKind.Duration:
                    break;
                case EdmPrimitiveTypeKind.Geography:
                    break;
                case EdmPrimitiveTypeKind.GeographyCollection:
                    break;
                case EdmPrimitiveTypeKind.GeographyLineString:
                    break;
                case EdmPrimitiveTypeKind.GeographyMultiLineString:
                    break;
                case EdmPrimitiveTypeKind.GeographyMultiPoint:
                    break;
                case EdmPrimitiveTypeKind.GeographyMultiPolygon:
                    break;
                case EdmPrimitiveTypeKind.GeographyPoint:
                    break;
                case EdmPrimitiveTypeKind.GeographyPolygon:
                    break;
                case EdmPrimitiveTypeKind.Geometry:
                    break;
                case EdmPrimitiveTypeKind.GeometryCollection:
                    break;
                case EdmPrimitiveTypeKind.GeometryLineString:
                    break;
                case EdmPrimitiveTypeKind.GeometryMultiLineString:
                    break;
                case EdmPrimitiveTypeKind.GeometryMultiPoint:
                    break;
                case EdmPrimitiveTypeKind.GeometryMultiPolygon:
                    break;
                case EdmPrimitiveTypeKind.GeometryPoint:
                    break;
                case EdmPrimitiveTypeKind.GeometryPolygon:
                    break;
                case EdmPrimitiveTypeKind.Guid:
                    return typeof(Guid);
                case EdmPrimitiveTypeKind.Int16:
                    return typeof(Int16);
                case EdmPrimitiveTypeKind.Int32:
                    return typeof(Int32);
                case EdmPrimitiveTypeKind.Int64:
                    return typeof(Int64);
                case EdmPrimitiveTypeKind.None:
                    break;
                case EdmPrimitiveTypeKind.SByte:
                    break;
                case EdmPrimitiveTypeKind.Single:
                    break;
                case EdmPrimitiveTypeKind.Stream:
                    break;
                case EdmPrimitiveTypeKind.String:
                    return typeof(string);
                case EdmPrimitiveTypeKind.TimeOfDay:
                    break;
                default:
                    break;
            }
            return typeof(object);
        }

        public static object ChangeType(this object v, EdmPrimitiveTypeKind t)
        {
            return v.ChangeType(t.ToClrType());
        }

    }
}
