using System.Collections.Generic;
using System.Linq;

namespace maskx.OData.Infrastructure
{
    public class Properties
    {
        private readonly Dictionary<string, BaseProperty> _Dicitionary = new Dictionary<string, BaseProperty>();
        private readonly CSharpUniqueNamer _CSharpNamer = new CSharpUniqueNamer();
        public Entity Entity { get; }
        public Properties(Entity entity)
        {
            this.Entity = entity;
        }
        public BaseProperty this[string name]
        {
            get
            {
                return _Dicitionary[name];
            }
        }
        public Property GetProperty(string originalName)
        {
            var exist = _Dicitionary.Values.FirstOrDefault(e => e.OriginalName == originalName);
            if (exist == null)
            {
                exist = new Property(_CSharpNamer.GetName(originalName), originalName) { Entity = Entity };
                _Dicitionary.Add(exist.Name, exist);
            }
            return exist as Property;
        }
        public ForeignKey AddForeignKey(ForeignKey fk)
        {
            var exist = _Dicitionary.Values.FirstOrDefault(e => e.OriginalName == fk.OriginalName);
            if (exist == null)
            {
                fk.Entity = this.Entity;
                fk.BuildPrincipalName();
                exist = fk;
                _Dicitionary.Add(exist.OriginalName, fk);
            }
            return exist as ForeignKey;
        }
        public ICollection<BaseProperty> ToList()
        {
            return _Dicitionary.Values;
        }
    }
}
