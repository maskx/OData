using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;

namespace maskx.OData.Extensions
{
    public static class IApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseDynamicOData(this IApplicationBuilder app,Action<DynamicOdataOptions> setupAction)
        {
            var odataOptions = app.ApplicationServices.GetService<IOptions<ODataOptions>>();
            if (odataOptions == null)
                throw new Exception("you should AddOData in ConfigureServices");
            var dynamicOdataOptions = app.ApplicationServices.GetService<IOptions<DynamicOdataOptions>>();
            if (dynamicOdataOptions == null)
                throw new Exception("you should AddDynamicOdata in ConfigureServices");
            var dynamicOdata = dynamicOdataOptions.Value;
            dynamicOdata.ServiceProvider = app.ApplicationServices;
            dynamicOdata.ODataOptions = odataOptions.Value;
            setupAction(dynamicOdata);
            return app;
        }
    }
}
