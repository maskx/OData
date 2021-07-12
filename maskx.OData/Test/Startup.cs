using maskx.OData;
using maskx.OData.Extensions;
using maskx.OData.SQLSource;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;

namespace Test
{
    class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddOData(opt => opt.Count().Filter().Expand().Select().OrderBy().SetMaxTop(5));
            services.AddDynamicOdata();
            services.AddOptions<DynamicOdataOptions>().Configure((options) =>
            {
                options.DataSources.Add(Common._RouterPrefix, new SQLServer("ds", "Data Source=.;Initial Catalog=Northwind;Integrated Security=True"));
            });
        }
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseODataRouteDebug();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

    }
}
