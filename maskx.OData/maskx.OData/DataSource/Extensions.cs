using Microsoft.AspNet.OData;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;

namespace maskx.OData.DataSource
{
    public static class Extensions
    {
        internal static void SetEntityPropertyValue(this DbDataReader reader, int fieldIndex, EdmStructuredObject entity)
        {
            string name = reader.GetName(fieldIndex);
            if (reader.IsDBNull(fieldIndex))
            {
                entity.TrySetPropertyValue(name, null);
                return;
            }
            var t = reader.GetFieldType(fieldIndex);
            entity.TryGetPropertyType(name, out Type et);
            if (t == typeof(DateTime))
            {
                entity.TrySetPropertyValue(name, new DateTimeOffset(reader.GetDateTime(fieldIndex)));
            }
            else if(et==typeof(bool?) || et == typeof(bool))
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
