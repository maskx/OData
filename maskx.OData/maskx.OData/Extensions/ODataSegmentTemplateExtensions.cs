using Microsoft.AspNetCore.OData.Routing.Template;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;

namespace maskx.OData.Extensions
{
    public static class ODataSegmentTemplateExtensions
    {
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
