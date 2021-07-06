using System.Linq;
using System.Collections.Generic;

namespace maskx.OData.Infrastructure
{
    /// <summary>
    /// <see cref="efcore\src\EFCore.Design\Scaffolding\Internal\CSharpUniqueNamer.cs"/>
    /// </summary>
    public class CSharpUniqueNamer
    {
        private readonly Dictionary<string, string> _usedNames = new Dictionary<string, string>();
        private readonly CSharpUtilities _cSharpUtilities = new CSharpUtilities();
        public string GetName(string item)
        {
            var exist = _usedNames.FirstOrDefault(e => e.Value == item);
            if (!exist.Equals(default(KeyValuePair<string, string>)))
                return exist.Key;
            var input = _cSharpUtilities.GenerateCSharpIdentifier(
                item, existingIdentifiers: null, null);
            var name = input;
            var suffix = 1;

            while (_usedNames.ContainsKey(name))
            {
                name = input + suffix++;
            }

            _usedNames.Add(name, item);
            return name;
        }
        public string GetDbName(string name)
        {
            return _usedNames[name];
        }
    }
}
