using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData.Abstracts;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.Options;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace maskx.OData
{
    public class DynamicRoutingMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
    {
        DynamicODataOptions _Options;
        public DynamicRoutingMatcherPolicy(IOptions<DynamicODataOptions> options)
        {
            _Options = options?.Value;
        }
        public override int Order => 1;

        public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
        {
            return endpoints.Any(e => e.Metadata.OfType<ODataRoutingMetadata>().FirstOrDefault() != null);
        }
        
        public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            IODataFeature odataFeature = httpContext.ODataFeature();
            if (odataFeature.Path != null)
            {
                // If we have the OData path setting, it means there's some Policy working.
                // Let's skip this default OData matcher policy.
                return Task.CompletedTask;
            }

            // The goal of this method is to perform the final matching:
            // Map between route values matched by the template and the ones we want to expose to the action for binding.
            // (tweaking the route values is fine here)
            // Invalidating the candidate if the key/function values are not valid/missing.
            // Perform overload resolution for functions by looking at the candidates and their metadata.
            for (var i = 0; i < candidates.Count; i++)
            {
                ref CandidateState candidate = ref candidates[i];
                if (!candidates.IsValidCandidate(i))
                {
                    continue;
                }

                IODataRoutingMetadata metadata = candidate.Endpoint.Metadata.OfType<IODataRoutingMetadata>().FirstOrDefault();
                if (metadata == null)
                {
                    continue;
                }

                //IEdmModel model = GetEdmModel(candidate.Values);
                //if (model == null)
                //{
                //    continue;
                //}

                //ODataTemplateTranslateContext translatorContext
                //    = new ODataTemplateTranslateContext(httpContext, candidate.Endpoint, candidate.Values, model);

                //try
                //{
                //    ODataPath odataPath = _translator.Translate(metadata.Template, translatorContext);
                //    if (odataPath != null)
                //    {
                //        odataFeature.PrefixName = metadata.Prefix;
                //        odataFeature.Model = model;
                //        odataFeature.Path = odataPath;

                //        MergeRouteValues(translatorContext.UpdatedValues, candidate.Values);
                //    }
                //    else
                //    {
                //        candidates.SetValidity(i, false);
                //    }
                //}
                //catch
                //{
                //}
            }

            return Task.CompletedTask;
        }
    }
}
