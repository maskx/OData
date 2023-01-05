using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Routing.Template;
using Microsoft.Extensions.Options;
using Microsoft.OData.Edm;
using System.Collections.Generic;
using System.Linq;
using maskx.OData.Extensions;

namespace maskx.OData
{
    public class DynamicApplicationModelProvider : IApplicationModelProvider
    {
        public int Order => 1;
        private readonly DynamicODataOptions _Options;
        public DynamicApplicationModelProvider(IOptions<ODataOptions> odataOptions, IOptions<DynamicODataOptions> options)
        {
            _Options = options.Value;
            foreach (var ds in options.Value.DataSources)
            {
                odataOptions.Value.AddRouteComponents(ds.Key, ds.Value.Model);
            }
        }
        void BuildGet(ActionModel actionModel)
        {
            foreach (var ds in _Options.DataSources)
            {
                foreach (var entitySet in ds.Value.Model.EntityContainer.EntitySets())
                {
                    actionModel.AddSelector("Get", ds.Key, ds.Value.Model, new ODataPathTemplate(
                                   new List<ODataSegmentTemplate>{
                                        new EntitySetSegmentTemplate(entitySet)
                                   }));
                }
            }
        }
        void BuildGetByKey(ActionModel actionModel)
        {
            foreach (var ds in _Options.DataSources)
            {
                foreach (var entitySet in ds.Value.Model.EntityContainer.EntitySets())
                {
                    var t = entitySet.EntityType();
                    var keys = t.Key().ToArray();
                    if (keys.Length > 0)
                    {
                        actionModel.AddSelector("get", ds.Key, ds.Value.Model, new ODataPathTemplate(
                           new List<ODataSegmentTemplate>{
                                        new EntitySetSegmentTemplate(entitySet),
                                        ODataSegmentTemplateExtensions.CreateKeySegment(t, entitySet)
                           }));
                    }
                }
            }
        }
        /// <summary>
        /// reference:AspNetCoreOData\src\Microsoft.AspNetCore.OData\Routing\Conventions\OperationImportRoutingConvention.cs
        /// </summary>
        /// <param name="dataSources"></param>
        /// <param name="actionModel"></param>
        void BuildDoAction(ActionModel actionModel)
        {
            foreach (var ds in _Options.DataSources)
            {
                foreach (var operationImport in ds.Value.Model.EntityContainer.OperationImports())
                {
                    if (operationImport.IsActionImport())
                    {
                        IEdmEntitySetBase targetEntitySet;
                        operationImport.TryGetStaticEntitySet(ds.Value.Model, out targetEntitySet);
                        ODataPathTemplate template = new ODataPathTemplate(new ActionImportSegmentTemplate(operationImport as IEdmActionImport, targetEntitySet));
                        actionModel.AddSelector("Post", ds.Key, ds.Value.Model, template, null);
                    }
                }
            }
        }
        /// <summary>
        /// reference:AspNetCoreOData\src\Microsoft.AspNetCore.OData\Routing\Conventions\OperationImportRoutingConvention.cs
        /// </summary>
        void BuildGetSimpleFunction(ActionModel actionModel)
        {
            foreach (var ds in _Options.DataSources)
            {
                foreach (var operationImport in ds.Value.Model.EntityContainer.OperationImports())
                {
                    if (operationImport.IsFunctionImport())
                    {
                        IEdmEntitySetBase targetSet;
                        operationImport.TryGetStaticEntitySet(ds.Value.Model, out targetSet);
                        ODataPathTemplate template = new ODataPathTemplate(new FunctionImportSegmentTemplate(operationImport as IEdmFunctionImport, targetSet));
                        actionModel.AddSelector("Get", ds.Key, ds.Value.Model, template, null);
                    }
                }
            }
        }
        /// <summary>
        /// aspnetcoreodata\src\Microsoft.AspNetCore.OData\Routing\Conventions\EntitySetRoutingConvention.cs
        /// </summary>
        /// <param name="actionModel"></param>
        void BuildPost(ActionModel actionModel)
        {
            foreach (var ds in _Options.DataSources)
            {
                foreach (var entitySet in ds.Value.Model.EntityContainer.EntitySets())
                {
                    actionModel.AddSelector("Post", ds.Key, ds.Value.Model, new ODataPathTemplate(
                                  new List<ODataSegmentTemplate>{
                                        new EntitySetSegmentTemplate(entitySet)
                                  }));
                }
            }
        }
        void BuildDelete(ActionModel actionModel)
        {
            foreach (var ds in _Options.DataSources)
            {
                foreach (var entitySet in ds.Value.Model.EntityContainer.EntitySets())
                {
                    var t = entitySet.EntityType();
                    var keys = t.Key().ToArray();
                    if (keys.Length > 0)
                    {
                        actionModel.AddSelector("Delete", ds.Key, ds.Value.Model, new ODataPathTemplate(
                           new List<ODataSegmentTemplate>{
                                        new EntitySetSegmentTemplate(entitySet),
                                        ODataSegmentTemplateExtensions.CreateKeySegment(t, entitySet)
                           }));
                    }
                }
            }
        }
        void BuildPatch(ActionModel actionModel)
        {
            foreach (var ds in _Options.DataSources)
            {
                foreach (var entitySet in ds.Value.Model.EntityContainer.EntitySets())
                {
                    // aspnetcoreodata\src\Microsoft.AspNetCore.OData\Routing\Conventions\EntitySetRoutingConvention.cs
                    actionModel.AddSelector("Patch", ds.Key, ds.Value.Model, new ODataPathTemplate(
                                  new List<ODataSegmentTemplate>{
                                        new EntitySetSegmentTemplate(entitySet)
                                  }));
                    // aspnetcoreodata\src\Microsoft.AspNetCore.OData\Routing\Conventions\EntityRoutingConvention.cs
                    var t = entitySet.EntityType();
                    var keys = t.Key().ToArray();
                    if (keys.Length > 0)
                    {
                        actionModel.AddSelector("Patch", ds.Key, ds.Value.Model, new ODataPathTemplate(
                           new List<ODataSegmentTemplate>{
                                        new EntitySetSegmentTemplate(entitySet),
                                        ODataSegmentTemplateExtensions.CreateKeySegment(t, entitySet)
                           }));
                    }
                }
            }
        }
        void BuildPut(ActionModel actionModel)
        {
            foreach (var ds in _Options.DataSources)
            {
                foreach (var entitySet in ds.Value.Model.EntityContainer.EntitySets())
                {
                    var t = entitySet.EntityType();
                    var keys = t.Key().ToArray();
                    if (keys.Length > 0)
                    {
                        actionModel.AddSelector("Put", ds.Key, ds.Value.Model, new ODataPathTemplate(
                           new List<ODataSegmentTemplate>{
                                        new EntitySetSegmentTemplate(entitySet),
                                        ODataSegmentTemplateExtensions.CreateKeySegment(t, entitySet)
                           }));
                    }
                }
            }
        }
        void BuildGetCount(ActionModel actionModel)
        {
            foreach (var ds in _Options.DataSources)
            {
                foreach (var entitySet in ds.Value.Model.EntityContainer.EntitySets())
                {
                    actionModel.AddSelector("Get", ds.Key, ds.Value.Model, new ODataPathTemplate(
                                   new List<ODataSegmentTemplate>{
                                        new EntitySetSegmentTemplate(entitySet),
                                       CountSegmentTemplate.Instance
                                   }));
                }
            }
        }
        void BuildGetFuncResultCount(ActionModel actionModel)
        {
            foreach (var ds in _Options.DataSources)
            {
                foreach (var operationImport in ds.Value.Model.EntityContainer.OperationImports())
                {
                    if (operationImport.IsFunctionImport())
                    {
                        IEdmEntitySetBase targetSet;
                        operationImport.TryGetStaticEntitySet(ds.Value.Model, out targetSet);
                        ODataPathTemplate template = new ODataPathTemplate(
                             new List<ODataSegmentTemplate>{
                                 new FunctionImportSegmentTemplate(operationImport as IEdmFunctionImport, targetSet),
                                 CountSegmentTemplate.Instance
                             });
                        actionModel.AddSelector("Get", ds.Key, ds.Value.Model, template, null);
                    }
                }
            }
        }
        public void OnProvidersExecuted(ApplicationModelProviderContext context)
        {
            var controllerModel = context.Result.Controllers.FirstOrDefault(e => e.ControllerName == "DynamicOData");
            if (controllerModel == null)
                return;
            foreach (var actionModel in controllerModel.Actions)
            {
                switch (actionModel.ActionName)
                {
                    case "Get":
                        BuildGet(actionModel);
                        break;
                    case "GetByKey":
                        BuildGetByKey(actionModel);
                        break;
                    case "Post":
                        BuildPost(actionModel);
                        break;
                    case "Delete":
                        BuildDelete(actionModel);
                        break;
                    case "Patch":
                        BuildPatch(actionModel);
                        break;
                    case "Put":
                        BuildPut(actionModel);
                        break;
                    case "GetSimpleFunction":
                        BuildGetSimpleFunction(actionModel);
                        break;
                    case "DoAction":
                        BuildDoAction(actionModel);
                        break;
                    case "GetCount":
                        BuildGetCount(actionModel);
                        break;
                    case "GetFuncResultCount":
                        BuildGetFuncResultCount(actionModel);
                        break;
                    default:
                        break;
                }


            }
        }

        public void OnProvidersExecuting(ApplicationModelProviderContext context)
        {
            //throw new NotImplementedException();
        }
    }
}
