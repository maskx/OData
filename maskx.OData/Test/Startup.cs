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
            services.AddRouting();            
            services.AddOData(opt => opt.Count().Filter().Expand().Select().OrderBy().SetMaxTop(5));
            services.AddDynamicOdata();
        }
        public void Configure(IApplicationBuilder app)
        {
            // this should run before UseEndpoints
            app.UseDynamicOData((op) =>
            {
                op.AddDataSource(Common._RouterPrefix, new SQLServer("ds", "Data Source=.;Initial Catalog=ODataTest;Integrated Security=True"));
            });

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

    }
}
