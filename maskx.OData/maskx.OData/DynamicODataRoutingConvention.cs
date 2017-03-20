using System.Linq;
using System.Web.OData.Routing;
using System.Web.OData.Routing.Conventions;

namespace maskx.OData
{
    public class DynamicODataRoutingConvention : IODataRoutingConvention
    {
        public string SelectAction(System.Web.OData.Routing.ODataPath odataPath, System.Web.Http.Controllers.HttpControllerContext controllerContext, ILookup<string, System.Web.Http.Controllers.HttpActionDescriptor> actionMap)
        {
            // stored procedure
            if (odataPath.PathTemplate == "~/unboundaction")
                return "DoAction";
            if (odataPath.PathTemplate == "~/unboundfunction/$count")
                return "GetFuncResultCount";
            if (odataPath.PathTemplate == "~/unboundfunction")
                return "GetSimpleFunction";
            if (odataPath.PathTemplate == "~/entityset/$count")
                return "GetCount";
            return null;
        }

        public string SelectController(ODataPath odataPath, System.Net.Http.HttpRequestMessage request)
        {
            var seg = odataPath.Segments.FirstOrDefault();
            if (seg is EntitySetPathSegment
                || seg is UnboundFunctionPathSegment
                || seg is UnboundActionPathSegment
                )
                return "DynamicOData";
            return null;
        }
    }
}
