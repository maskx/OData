using maskx.OData.Infrastructure;
using System.Collections.Generic;
using System.Linq;

namespace maskx.OData.Infrastructure
{
    public class NameSpaces
    {
        private readonly Dictionary<string, EntityCollection> _Dictionary = new Dictionary<string, EntityCollection>();
        private readonly CSharpUniqueNamer _NamespaceNamer = new CSharpUniqueNamer();
        public EntityCollection this[string name]
        {
            get
            {
                return _Dictionary[name];
            }
        }
        public string GetName(string originalName)
        {
            return _NamespaceNamer.GetName(originalName);
        }
        public string GetOriginalName(string name)
        {
            return _NamespaceNamer.GetDbName(name);
        }
        public EntityCollection GetByOriginalName(string originalName)
        {
            EntityCollection ec = _Dictionary.Values.FirstOrDefault(e => e.Schema == originalName);
            if (ec == null)
            {
                ec = new EntityCollection(_NamespaceNamer.GetName(originalName),originalName);
                _Dictionary.Add(originalName, ec);
            }
            return ec;
        }

    }
}
