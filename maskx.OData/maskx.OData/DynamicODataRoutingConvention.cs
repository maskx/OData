using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNet.OData.Routing.Conventions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace maskx.OData
{
    public class DynamicODataRoutingConvention : IODataRoutingConvention
    {
        public IEnumerable<ControllerActionDescriptor> SelectAction(RouteContext routeContext)
        {
            var odataFeature = routeContext.HttpContext.ODataFeature();

            IActionDescriptorCollectionProvider actionCollectionProvider =
                    routeContext.HttpContext.RequestServices.GetRequiredService<IActionDescriptorCollectionProvider>();
            Contract.Assert(actionCollectionProvider != null);

            IEnumerable<ControllerActionDescriptor> actionDescriptors = actionCollectionProvider
                    .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
                    .Where(c => c.ControllerName == "DynamicOData");

            string actionName = string.Empty;
            switch (odataFeature.Path.PathTemplate)
            {
                case "~/unboundaction":
                    actionName = "DoAction";
                    break;
                case "~/unboundfunction/$count":
                    actionName = "GetFuncResultCount";
                    break;
                case "~/unboundfunction":
                    actionName = "GetSimpleFunction";
                    break;
                case "~/entityset/$count":
                    actionName = "GetCount";
                    break;
                case "~/entityset":
                    switch (routeContext.HttpContext.Request.Method)
                    {
                        case "POST":
                            actionName = "Post";
                            break;
                        default:
                            actionName = "Get";
                            break;
                    }
                    break;
                case "~/entityset/key":
                case "~/entityset/key/property":
                case "~/entityset/key/property/$value":
                    switch (routeContext.HttpContext.Request.Method)
                    {
                        case "DELETE":
                            actionName = "Delete";
                            break;
                        case "PATCH":
                            actionName = "Patch";
                            break;
                        case "PUT":
                            actionName = "Put";
                            break;
                        default:
                            actionName = "GetByKey";
                            break;
                    }
                    break;
                default:
                    break;
            }

            if (actionDescriptors != null)
            {
                if (!String.IsNullOrEmpty(actionName))
                {
                    return actionDescriptors.Where(
                        c => String.Equals(c.ActionName, actionName, StringComparison.OrdinalIgnoreCase));
                }
            }
            return null;
        }
    }
}
