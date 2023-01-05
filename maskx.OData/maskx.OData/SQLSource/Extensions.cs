using Microsoft.AspNetCore.OData.Formatter.Value;
using System;
using System.Data.Common;

namespace maskx.OData.SQLSource
{
    public static class Extensions
    {
        internal static void SetEntityPropertyValue(this DbDataReader reader, int fieldIndex, EdmStructuredObject entity, bool lowerName = false)
        {
            string name = reader.GetName(fieldIndex);
            if (lowerName) name = name.ToLower();
            if (reader.IsDBNull(fieldIndex))
            {
                entity.TrySetPropertyValue(name, null);
                return;
            }
            var t = reader.GetFieldType(fieldIndex);
            entity.TryGetPropertyType(name, out Type et);
            // todo: byte[] need investment odata document
            if (t == typeof(byte[]))
            {
                entity.TrySetPropertyValue(name, null);
                return;
            }

            if (t == typeof(DateTime))
            {
                entity.TrySetPropertyValue(name, new DateTimeOffset(reader.GetDateTime(fieldIndex)));
            }
            else if (et == typeof(bool?) || et == typeof(bool))
            {
                entity.TrySetPropertyValue(name, reader[fieldIndex].ToString() != "0");
            }
            else
            {
                entity.TrySetPropertyValue(name, reader.GetValue(fieldIndex));
            }
        }
        public static bool IsDBNull(this DbDataReader reader, string columnName)
        {
            return Convert.IsDBNull(reader[columnName]);
        }
    }
}
