using Microsoft.OData.UriParser;
using System.Linq;
using System.Net.Http;
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

        public string SelectController(System.Web.OData.Routing.ODataPath odataPath, HttpRequestMessage request)
        {
            var seg = odataPath.Segments.FirstOrDefault();
            if (seg is EntitySetSegment
                || seg is OperationImportSegment
                )
                return "DynamicOData";
            return null;
        }
    }
}
