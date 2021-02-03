using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Routing.Conventions;
using Microsoft.AspNetCore.OData.Routing.Template;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;

namespace maskx.OData
{
    public class DynamicODataControllerActionConvention : IODataControllerActionConvention
    {
        public int Order => 1;

        public bool AppliesToAction(ODataControllerActionContext context)
        {
            ActionModel action = context.Action;
            switch (context.Action.ActionName)
            {
                case "Get":
                    BuildGetAction(context.Prefix, context.Model, context.Action);
                    break;
                default:
                    break;
            }

            return true;
        }

        public bool AppliesToController(ODataControllerActionContext context)
        {
            if (context.Controller.ControllerType.FullName == "maskx.OData.DynamicODataController")
                return true;
            return false;
        }
        private void BuildGetAction(string prefix, IEdmModel model, ActionModel actionModel)
        {
            foreach (var entitySet in model.EntityContainer.EntitySets())
            {
                IList<ODataSegmentTemplate> segments = new List<ODataSegmentTemplate>{
                    new EntitySetSegmentTemplate(entitySet)
                };
                actionModel.AddSelector("Get", prefix, model, new ODataPathTemplate(segments));
            }
        }
    }
}
