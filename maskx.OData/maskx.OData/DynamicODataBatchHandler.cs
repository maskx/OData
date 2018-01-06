using Microsoft.AspNet.OData.Routing;
using Microsoft.OData;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using ODL = Microsoft.OData.UriParser;

namespace maskx.OData
{
    public class DynamicODataBatchHandler : DefaultODataPathHandler
    {
        public override ODataPath Parse(string serviceRoot, string odataPath, IServiceProvider requestContainer)
        {
            ODL.ODataUriParser uriParser;
            Uri serviceRootUri = null;
            Uri fullUri = null;
            string dataSourceName = odataPath.Split('/')[0];
            IEdmModel model = DataSourceProvider.GetEdmModel(dataSourceName);
            Contract.Assert(serviceRoot != null);

            serviceRootUri = new Uri(
                serviceRoot.EndsWith("/", StringComparison.Ordinal)
                    ? serviceRoot
                    : serviceRoot + "/");


            // Concatenate the root and path and create a Uri. Using Uri to build a Uri from
            // a root and relative path changes the casing on .NetCore. However, odataPath may
            // be a full Uri.
            if (!Uri.TryCreate(odataPath, UriKind.Absolute, out fullUri))
            {
                fullUri = new Uri(serviceRootUri + odataPath);
            }
            serviceRootUri = new Uri(serviceRootUri, dataSourceName);
            uriParser = new ODL.ODataUriParser(model, serviceRootUri, fullUri, requestContainer);

            if (UrlKeyDelimiter != null)
            {
                uriParser.UrlKeyDelimiter = UrlKeyDelimiter;
            }
            else
            {
                // ODL changes to use ODataUrlKeyDelimiter.Slash as default value.
                // Web API still uses the ODataUrlKeyDelimiter.Parentheses as default value.
                // Please remove it after fix: https://github.com/OData/odata.net/issues/642
                uriParser.UrlKeyDelimiter = ODataUrlKeyDelimiter.Parentheses;
            }

            ODL.ODataPath path;
            UnresolvedPathSegment unresolvedPathSegment = null;
            ODL.KeySegment id = null;

            try
            {
                path = uriParser.ParsePath();
            }
            catch (ODL.ODataUnrecognizedPathException ex)
            {
                if (ex.ParsedSegments != null &&
                    ex.ParsedSegments.Any() &&
                    (ex.ParsedSegments.Last().EdmType is IEdmComplexType ||
                     ex.ParsedSegments.Last().EdmType is IEdmEntityType) &&
                    ex.CurrentSegment != ODataSegmentKinds.Count)
                {
                    if (!ex.UnparsedSegments.Any())
                    {
                        path = new ODL.ODataPath(ex.ParsedSegments);
                        unresolvedPathSegment = new UnresolvedPathSegment(ex.CurrentSegment);
                    }
                    else
                    {
                        // Throw ODataException if there is some segment following the unresolved segment.
                        throw new ODataException(String.Format(CultureInfo.CurrentCulture,
                            "InvalidPathSegment",
                            ex.UnparsedSegments.First(),
                            ex.CurrentSegment));
                    }
                }
                else
                {
                    throw;
                }
            }
            if (path.LastSegment is ODL.NavigationPropertyLinkSegment)
            {
                IEdmCollectionType lastSegmentEdmType = path.LastSegment.EdmType as IEdmCollectionType;

                if (lastSegmentEdmType != null)
                {
                    ODL.EntityIdSegment entityIdSegment = null;
                    bool exceptionThrown = false;

                    try
                    {
                        entityIdSegment = uriParser.ParseEntityId();

                        if (entityIdSegment != null)
                        {
                            // Create another ODataUriParser to parse $id, which is absolute or relative.
                            ODL.ODataUriParser parser = new ODL.ODataUriParser(model, serviceRootUri, entityIdSegment.Id, requestContainer);
                            id = parser.ParsePath().LastSegment as ODL.KeySegment;
                        }
                    }
                    catch (ODataException)
                    {
                        // Exception was thrown while parsing the $id.
                        // We will throw another exception about the invalid $id.
                        exceptionThrown = true;
                    }

                    if (exceptionThrown ||
                        (entityIdSegment != null &&
                            (id == null ||
                                !(id.EdmType.IsOrInheritsFrom(lastSegmentEdmType.ElementType.Definition) ||
                                  lastSegmentEdmType.ElementType.Definition.IsOrInheritsFrom(id.EdmType)))))
                    {
                        // System.Net.Http on NetCore does not have the Uri extension method
                        // ParseQueryString(), to avoid a platform-specific call, extract $id manually.
                        string idValue = fullUri.Query;
                        string idParam = "$id=";
                        int start = idValue.IndexOf(idParam, StringComparison.OrdinalIgnoreCase);
                        if (start >= 0)
                        {
                            int end = idValue.IndexOf("&", start, StringComparison.OrdinalIgnoreCase);
                            if (end >= 0)
                            {
                                idValue = idValue.Substring(start + idParam.Length, end - 1);
                            }
                            else
                            {
                                idValue = idValue.Substring(start + idParam.Length);
                            }
                        }

                        throw new ODataException(String.Format(CultureInfo.CurrentCulture, "InvalidDollarId", idValue));
                    }
                }
            }

            // do validation for the odata path
            path.WalkWith(new DefaultODataPathValidator(model));

            // do segment translator (for example parameter alias, key & function parameter template, etc)
            var segments =
                ODataPathSegmentTranslator.Translate(model, path, uriParser.ParameterAliasNodes).ToList();

            if (unresolvedPathSegment != null)
            {
                segments.Add(unresolvedPathSegment);
            }
            AppendIdForRef(segments, id);
            return new ODataPath(segments);
        }

        private static void AppendIdForRef(IList<ODL.ODataPathSegment> segments, ODL.KeySegment id)
        {
            if (id == null || !(segments.Last() is ODL.NavigationPropertyLinkSegment))
            {
                return;
            }

            segments.Add(id);
        }
    }
}
