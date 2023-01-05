using maskx.OData.Infrastructure;
using Microsoft.OData.Edm;
using System.Collections.Generic;

namespace maskx.OData.Extensions
{
    public static class Extensions
    {
        public static IEnumerable<IEdmStructuralProperty> ToEdmProperties(this List<Property>  properties, EdmEntityType edmEntityType )
        {
            return properties.ConvertAll<IEdmStructuralProperty>(p=>edmEntityType.FindProperty(p.Name) as IEdmStructuralProperty);
        }
    }
}
