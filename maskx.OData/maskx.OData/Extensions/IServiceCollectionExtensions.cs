using Microsoft.AspNetCore.OData.Routing.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace maskx.OData.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddDynamicOdata(this IServiceCollection services)
        {
            services.AddControllers().AddApplicationPart(typeof(DynamicODataController).Assembly);
            services.TryAddEnumerable(ServiceDescriptor.Transient<IODataControllerActionConvention, DynamicODataControllerActionConvention>());
            return services;
        }
    }
}
