using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Routing;
using Microsoft.AspNetCore.OData.Routing.Conventions;
using Microsoft.AspNetCore.OData.Routing.Template;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;

namespace maskx.OData
{
    public class DynamicODataControllerActionConvention : IODataControllerActionConvention
    {
        public int Order => 1;

        public bool AppliesToAction(ODataControllerActionContext context)
        {
            switch (context.Action.ActionName)
            {
                case "Get":
                    BuildGetAction(context.Prefix, context.Model, context.Action);
                    break;
                case "GetByKey":
                    BuildGeByKeytAction(context.Prefix, context.Model, context.Action, context.Options?.RouteOptions);
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
        private static void BuildGeByKeytAction(string prefix, IEdmModel model, ActionModel actionModel,ODataRouteOptions options)
        {
            foreach (var entitySet in model.EntityContainer.EntitySets())
            {
                // ~/Customers({key})
                var entityType = entitySet.EntityType();
                IList<ODataSegmentTemplate> segments = new List<ODataSegmentTemplate>{
                    new EntitySetSegmentTemplate(entitySet),
                    CreateKeySegment(entityType,entitySet)
                };
                actionModel.AddSelector("Get", prefix, model, new ODataPathTemplate(segments), options);

                // ~/Customers({key})/Ns.VipCustomer
                foreach (var nav in entitySet.NavigationPropertyBindings)
                {
                    var castType = nav.Target.EntityType();
                    IList<ODataSegmentTemplate> seg = new List<ODataSegmentTemplate>{
                        new EntitySetSegmentTemplate(entitySet),
                        CreateKeySegment(entitySet.EntityType(),entitySet),
                        new CastSegmentTemplate(castType, entityType, entitySet)
                    };
                    actionModel.AddSelector("Get", prefix, model, new ODataPathTemplate(seg), options);
                }

            }
        }
        private static void BuildGetAction(string prefix, IEdmModel model, ActionModel actionModel)
        {
            foreach (var entitySet in model.EntityContainer.EntitySets())
            {
                IList<ODataSegmentTemplate> segments = new List<ODataSegmentTemplate>{
                    new EntitySetSegmentTemplate(entitySet)
                };
                actionModel.AddSelector("Get", prefix, model, new ODataPathTemplate(segments));
            }
        }
        internal static KeySegmentTemplate CreateKeySegment(IEdmEntityType entityType, IEdmNavigationSource navigationSource, string keyPrefix = "key")
        {
            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }

            IDictionary<string, string> keyTemplates = new Dictionary<string, string>();
            var keys = entityType.Key().ToArray();
            if (keys.Length == 1)
            {
                // Id={key}
                keyTemplates[keys[0].Name] = $"{{{keyPrefix}}}";
            }
            else
            {
                // Id1={keyId1},Id2={keyId2}
                foreach (var key in keys)
                {
                    keyTemplates[key.Name] = $"{{{keyPrefix}{key.Name}}}";
                }
            }

            return new KeySegmentTemplate(keyTemplates, entityType, navigationSource);
        }
    }
}
