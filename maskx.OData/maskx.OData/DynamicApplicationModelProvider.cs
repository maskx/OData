using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Routing.Template;
using Microsoft.Extensions.Options;
using Microsoft.OData.Edm;
using System.Collections.Generic;
using System.Linq;

namespace maskx.OData
{
    public class DynamicApplicationModelProvider : IApplicationModelProvider
    {
        public int Order => 1;
        private readonly DynamicOdataOptions _Options;
        public DynamicApplicationModelProvider(IOptions<ODataOptions> odataOptions, IOptions<DynamicOdataOptions> options)
        {
            _Options = options.Value;
            foreach (var ds in options.Value.DataSources)
            {
                odataOptions.Value.AddRouteComponents(ds.Key, ds.Value.Model);
            }
        }
        public void OnProvidersExecuted(ApplicationModelProviderContext context)
        {
            var controllerModel = context.Result.Controllers.FirstOrDefault(e => e.ControllerName == "DynamicOData");
            if (controllerModel == null)
                return;
            foreach (var ds in _Options.DataSources)
            {
                foreach (var entitySet in ds.Value.Model.EntityContainer.EntitySets())
                {

                    foreach (var actionModel in controllerModel.Actions)
                    {
                        switch (actionModel.ActionName)
                        {
                            case "Get":
                                ProcessGet(actionModel);
                                break;
                            case "":
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

        }
        private  void ProcessGet(ActionModel actionModel)
        {
            foreach (var ds in _Options.DataSources)
            {
                foreach (var entitySet in ds.Value.Model.EntityContainer.EntitySets())
                {
                    IList<ODataSegmentTemplate> segments = new List<ODataSegmentTemplate>{
                                        new EntitySetSegmentTemplate(entitySet)
                                    };
                    actionModel.AddSelector("get", ds.Key, ds.Value.Model, new ODataPathTemplate(segments));
                }
            }
        }

        public void OnProvidersExecuting(ApplicationModelProviderContext context)
        {
            //throw new NotImplementedException();
        }
    }
}
