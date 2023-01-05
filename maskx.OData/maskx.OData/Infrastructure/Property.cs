using Microsoft.OData.Edm;
using System.Data;

namespace maskx.OData.Infrastructure
{
    public class Property:BaseProperty
    {
        public Property(string name, string originalName) : base(name,originalName) { }
        public int DbType { get; set; }
        public EdmPrimitiveTypeKind EdmPrimitiveTypeKind { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable { get; set; }
        public int Size { get; set; }
        public byte Scale { get; set; }
        public byte Precision { get; set; }
    }
}
