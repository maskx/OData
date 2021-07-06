using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace maskx.OData.Extensions
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddDynamicOdata(this IServiceCollection services)
        {
            services.AddControllers().AddApplicationPart(typeof(DynamicODataController).Assembly);
            services.TryAddEnumerable(ServiceDescriptor.Transient<IApplicationModelProvider, DynamicApplicationModelProvider>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, DynamicRoutingMatcherPolicy>());
            return services;
        }
    }
}
