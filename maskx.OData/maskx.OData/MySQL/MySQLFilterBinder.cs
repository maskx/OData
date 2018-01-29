using maskx.OData.Database;
using Microsoft.OData.UriParser;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Linq;

namespace maskx.OData.MySQL
{
    public class MySQLFilterBinder : FilterBinder
    {
        public override string BindSingleValueFunctionCallNode(SingleValueFunctionCallNode node, List<DbParameter> pars)
        {
            var arguments = node.Parameters.ToList();
            string name = string.Empty;
            string parName = string.Empty;
            object parValue = null;
            switch (node.Name)
            {
                case "concat":
                    List<string> p = new List<string>();
                    foreach (var item in node.Parameters)
                    {
                        parValue = Bind(item, pars);
                        parName = "p" + DateTime.Now.Ticks;
                        p.Add("@" + parName);
                        pars.Add(new MySqlParameter(parName, parValue));
                    }
                    return string.Format("concat({0})", string.Join(",", p));
                case "contains":
                    name = Bind(arguments[0], pars);
                    parName = name + pars.Count;
                    parValue = string.Format("%{0}%", (arguments[1] as ConstantNode).Value);
                    pars.Add(new MySqlParameter(parName, parValue.ToString()));
                    return string.Format("{0} like @{1}", name, parName);
                case "endswith":
                    name = Bind(arguments[0], pars);
                    parName = name + pars.Count;
                    parValue = string.Format("%{0}", (arguments[1] as ConstantNode).Value);
                    pars.Add(new MySqlParameter(parName, parValue.ToString()));
                    return string.Format("{0} like @{1}", name, parName);
                case "startswith":
                    name = Bind(arguments[0], pars);
                    parName = name + pars.Count;
                    parValue = string.Format("{0}%", (arguments[1] as ConstantNode).Value);
                    pars.Add(new MySqlParameter(parName, parValue.ToString()));
                    return string.Format("{0} like @{1}", name, parName);
                case "length":
                    return string.Format("len({0})", Bind(arguments[0], pars));
                case "indexof":
                    name = Bind(arguments[0], pars);
                    parName = name + pars.Count;
                    parValue = (arguments[0] as ConstantNode).Value;
                    pars.Add(new MySqlParameter(parName, parValue.ToString()));
                    return string.Format("charindex(@{0},{1})", parName, name);
                case "substring":
                    parValue = Bind(arguments[0], pars);
                    parName = "p" + DateTime.Now.Ticks;
                    pars.Add(new MySqlParameter(parName, parValue));
                    return string.Format("SUBSTRING(@{0},{1},{2})",
                        parName,
                        (arguments[1] as ConstantNode).Value,
                       arguments.Count > 2 ? (arguments[2] as ConstantNode).Value : 0);
                case "tolower":
                    parValue = Bind(arguments[0], pars);
                    parName = "p" + DateTime.Now.Ticks;
                    pars.Add(new MySqlParameter(parName, parValue));
                    return "LOWER(@" + parName + ")";
                case "toupper":
                    parValue = Bind(arguments[0], pars);
                    parName = "p" + DateTime.Now.Ticks;
                    pars.Add(new MySqlParameter(parName, parValue));
                    return "UPPER(@" + parName + ")";
                case "trim":
                case "year":
                case "years":
                case "month":
                case "months":
                case "day":
                case "days":
                case "hour":
                case "hours":
                case "minute":
                case "minutes":
                case "second":
                case "seconds":
                case "round":
                case "floor":
                case "ceiling":
                    parValue = Bind(arguments[0], pars);
                    parName = "p" + DateTime.Now.Ticks;
                    pars.Add(new MySqlParameter(parName, parValue));
                    return node.Name + "(@" + parName + ")";
                default:
                    return string.Empty;
            }
        }
        public override string BindBinaryOperatorNode(BinaryOperatorNode node, List<DbParameter> pars)
        {
            var left = Bind(node.Left, pars);
            var right = Bind(node.Right, pars);
            //TODO: ConstantNode parameterization
            //TODO: left =null. like null eq name
            if (node.OperatorKind == BinaryOperatorKind.Equal
                && right == "null")
                return "(" + left + " is null)";
            if (node.OperatorKind == BinaryOperatorKind.NotEqual
                && right == "null")
                return "(" + left + " is not null)";
            return "(" + left + " " + BindBinaryOperatorKind(node.OperatorKind) + " " + right + ")";
        }

        public override DbParameter CreateSqlParameter(string name, object value)
        {
            return new MySqlParameter(name,value);
        }
    }

}
