using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNet.OData.Routing.Conventions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace maskx.OData
{
    public static class Extensions
    {
        private static ODataRoute MapDynamicODataServiceRoute(this IRouteBuilder builder, string routeName,
            string routePrefix, IODataPathHandler pathHandler,
            IEnumerable<IODataRoutingConvention> routingConventions,
            IDataSource dataSource)
        {
            var odataRoute = builder.MapODataServiceRoute(routeName, routePrefix, containerBuilder =>
            {
                containerBuilder
                    .AddService<IEdmModel>(Microsoft.OData.ServiceLifetime.Singleton, sp =>
                                       {
                                           var b = DataSourceProvider.GetEdmModel(routePrefix);
                                           return b;
                                       })
                    .AddService(Microsoft.OData.ServiceLifetime.Scoped, sp => routingConventions.ToList().AsEnumerable());
                if (pathHandler != null)
                    containerBuilder.AddService(Microsoft.OData.ServiceLifetime.Singleton, sp => pathHandler);
            });
            DataSourceProvider.AddDataSource(routePrefix, dataSource);
            builder.EnableDependencyInjection();
            return odataRoute;
        }
        public static ODataRoute MapDynamicODataServiceRoute(this IRouteBuilder builder, string routeName, string routePrefix, IDataSource dataSource)
        {
            IList<IODataRoutingConvention> routingConventions = ODataRoutingConventions.CreateDefault();
            routingConventions.Insert(0, new DynamicODataRoutingConvention());
            return builder.MapDynamicODataServiceRoute(routeName, routePrefix, null, routingConventions, dataSource);
        }
        internal static IEdmType GetEdmType(this ODataPath path)
        {
            return path.Segments[0].EdmType;

        }
        internal static object ChangeType(this object v, Type t)
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
                        if (Guid.TryParse(v.ToString(), out Guid g))
                            return g;
                    }
                }
            }
            return null;
        }
        internal static object ChangeType(this object v, EdmPrimitiveTypeKind t)
        {
            return v.ChangeType(t.ToClrType());
        }
        internal static Type ToClrType(this EdmPrimitiveTypeKind t)
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
        /// <summary>
        /// Retrieve the raw body as a string from the Request.Body stream
        /// </summary>
        /// <param name="request">Request instance to apply to</param>
        /// <param name="encoding">Optional - Encoding, defaults to UTF8</param>
        /// <returns></returns>
        public static async Task<string> GetRawBodyStringAsync(this HttpRequest request, Encoding encoding = null)
        {
            if (encoding == null)
                encoding = Encoding.UTF8;

            using (StreamReader reader = new StreamReader(request.Body, encoding))
                return await reader.ReadToEndAsync();
        }

        /// <summary>
        /// Retrieves the raw body as a byte array from the Request.Body stream
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<byte[]> GetRawBodyBytesAsync(this HttpRequest request)
        {
            using (var ms = new MemoryStream(2048))
            {
                await request.Body.CopyToAsync(ms);
                return ms.ToArray();
            }
        }
    }
}
