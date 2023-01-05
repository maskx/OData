using maskx.OData.Infrastructure;
using System.Collections.Generic;
using System.Linq;

namespace maskx.OData.Infrastructure
{
    public class EntityCollection
    {
        private readonly Dictionary<string, Entity> _Dicitionary = new Dictionary<string, Entity>();
        private readonly CSharpUniqueNamer _EntityNamer = new CSharpUniqueNamer();
        public string Schema { get;  }
        public string NameSpace { get; }
        public EntityCollection(string nameSpace,string schema)
        {
            this.Schema = schema;
            this.NameSpace = nameSpace;
        }
        public Entity this[string name]
        {
            get
            {
                return _Dicitionary[name];
            }
        }
        public Entity GetByOriginalName(string originalName)
        {
            var exist = _Dicitionary.Values.FirstOrDefault(e => e.OriginalName == originalName);
            if (exist == null)
            {
                exist = new Entity(_EntityNamer.GetName(originalName), originalName);
                exist.NameSpace = NameSpace;
                exist.Schema= Schema;
                _Dicitionary.Add(exist.Name, exist);
            }
            return exist;
        }
    }
}
