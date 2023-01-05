using System.Collections.Generic;
using System.Linq;

namespace maskx.OData.Infrastructure
{
    public class Entity
    {
        public Entity(string name, string originalName)
        {
            this.Name = name;
            this.OriginalName = originalName;
            this.Properties = new Properties(this);
        }
        public string Name { get; }
        public string NameSpace { get; set; }
        public string Schema { get; set;  }
        public string FullName { get { return $"{NameSpace}.{Name}"; } }
        public string OriginalName { get; }
        public Properties Properties { get; }
        public IEnumerable<ForeignKey> GetForeignKeys()
        {
            return Properties.ToList().Where(p => p is ForeignKey).Cast<ForeignKey>();
        }
    }
}
