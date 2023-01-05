using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.ObjectModel;

namespace maskx.OData.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static void UseDynamicOdata(this IApplicationBuilder builder, Action<DataSourceBuilder> sourceBuilder)
        {
            var options = builder.ApplicationServices.GetRequiredService<IOptions<DynamicODataOptions>>();
            DataSourceBuilder ds = new DataSourceBuilder(options.Value);
            sourceBuilder(ds);
            options.Value.DataSources = new ReadOnlyDictionary<string, IDataSource>(ds.DataSources);
        }
    }
}
