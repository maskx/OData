using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.OData.Routing;
using System.Web.OData.Routing.Conventions;

namespace maskx.OData
{
    public class DynamicODataRoutingConvention : IODataRoutingConvention
    {
        public string SelectAction(System.Web.OData.Routing.ODataPath odataPath, System.Web.Http.Controllers.HttpControllerContext controllerContext, ILookup<string, System.Web.Http.Controllers.HttpActionDescriptor> actionMap)
        {
            if (odataPath.PathTemplate == "~/unboundfunction/$count")
                return "GetFuncResultCount";
            var seg = odataPath.Segments.FirstOrDefault();
            if (controllerContext.Request.Method == System.Net.Http.HttpMethod.Post)
            {
                if (seg is UnboundFunctionPathSegment)
                    return "PostComplexFunction";
            }
            else if (controllerContext.Request.Method == System.Net.Http.HttpMethod.Get)
            {
                if (odataPath.PathTemplate == "~/entityset/$count")
                    return "GetCount";
                if (seg is UnboundFunctionPathSegment)
                    return "GetSimpleFunction";
            }
            return null;
        }

        public string SelectController(System.Web.OData.Routing.ODataPath odataPath, System.Net.Http.HttpRequestMessage request)
        {
            var seg = odataPath.Segments.FirstOrDefault();
            if (seg is EntitySetPathSegment || seg is UnboundFunctionPathSegment)
                return "DynamicOData";
            return null;
        }
    }
}
