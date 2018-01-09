using Microsoft.AspNet.OData.Query;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace maskx.OData.Sql
{
    /// <summary>
    /// reference:
    /// https://aspnet.codeplex.com/SourceControl/changeset/view/72014f4c779e#Samples/WebApi/NHibernateQueryableSample/System.Web.Http.OData.NHibernate/NHibernateFilterBinder.cs
    /// </summary>
    static class SQLFilterBinder
    {
        internal static string ParseFilter(this ODataQueryOptions options, List<SqlParameter> pars)
        {
            if (options.Filter == null || options.Filter.FilterClause == null)
                return string.Empty;
            string where = options.Filter.FilterClause.ParseFilter(pars);
            if (!string.IsNullOrEmpty(where))
                where = " where " + where;
            return where;
        }
        internal static string ParseFilter(this ExpandedNavigationSelectItem expanded, List<SqlParameter> pars)
        {
            string where = expanded.FilterOption.ParseFilter(pars);
            if (string.IsNullOrEmpty(where))
                return string.Empty;
            return string.Format(" and ({0}) ", where);
        }
        public static string ParseFilter(this FilterClause filterClause, List<SqlParameter> pars)
        {
            if (filterClause == null)
                return string.Empty;
            return filterClause.BindFilterClause(pars) + Environment.NewLine;
        }

        static string BindFilterClause(this FilterClause filterClause, List<SqlParameter> pars)
        {
            return Bind(filterClause.Expression, pars);
        }

        static string Bind(QueryNode node, List<SqlParameter> pars)
        {
            CollectionNode collectionNode = node as CollectionNode;
            SingleValueNode singleValueNode = node as SingleValueNode;

            if (collectionNode != null)
            {
                switch (node.Kind)
                {
                    case QueryNodeKind.CollectionNavigationNode:
                        CollectionNavigationNode navigationNode = node as CollectionNavigationNode;
                        return BindNavigationPropertyNode(navigationNode.Source, navigationNode.NavigationProperty, pars);

                    case QueryNodeKind.CollectionPropertyAccess:
                        return BindCollectionPropertyAccessNode(node as CollectionPropertyAccessNode);
                }
            }
            else if (singleValueNode != null)
            {
                switch (node.Kind)
                {
                    case QueryNodeKind.BinaryOperator:
                        return BindBinaryOperatorNode(node as BinaryOperatorNode, pars);

                    case QueryNodeKind.Constant:
                        return BindConstantNode(node as ConstantNode);

                    case QueryNodeKind.Convert:
                        return BindConvertNode(node as ConvertNode, pars);

                    case QueryNodeKind.ResourceRangeVariableReference:
                        return BindRangeVariable((node as ResourceRangeVariableReferenceNode).RangeVariable);

                    case QueryNodeKind.NonResourceRangeVariableReference:
                        return BindRangeVariable((node as NonResourceRangeVariableReferenceNode).RangeVariable);

                    case QueryNodeKind.SingleValuePropertyAccess:
                        return BindPropertyAccessQueryNode(node as SingleValuePropertyAccessNode);

                    case QueryNodeKind.UnaryOperator:
                        return BindUnaryOperatorNode(node as UnaryOperatorNode, pars);

                    case QueryNodeKind.SingleValueFunctionCall:
                        return BindSingleValueFunctionCallNode(node as SingleValueFunctionCallNode, pars);

                    case QueryNodeKind.SingleNavigationNode:
                        SingleNavigationNode navigationNode = node as SingleNavigationNode;
                        return BindNavigationPropertyNode(navigationNode.Source, navigationNode.NavigationProperty, pars);

                    case QueryNodeKind.Any:
                        return BindAnyNode(node as AnyNode, pars);

                    case QueryNodeKind.All:
                        return BindAllNode(node as AllNode, pars);
                }
            }

            throw new NotSupportedException(String.Format("Nodes of type {0} are not supported", node.Kind));
        }

        private static string BindCollectionPropertyAccessNode(CollectionPropertyAccessNode collectionPropertyAccessNode)
        {
            return collectionPropertyAccessNode.Property.Name;
            //return Bind(collectionPropertyAccessNode.Source) + "." + collectionPropertyAccessNode.Property.Name;
        }

        private static string BindNavigationPropertyNode(SingleValueNode singleValueNode, IEdmNavigationProperty edmNavigationProperty, List<SqlParameter> pars)
        {
            return Bind(singleValueNode, pars) + "." + edmNavigationProperty.Name;
        }

        private static string BindAllNode(AllNode allNode, List<SqlParameter> pars)
        {
            string innerQuery = "not exists ( from " + Bind(allNode.Source, pars) + " " + allNode.RangeVariables.First().Name;
            innerQuery += " where NOT(" + Bind(allNode.Body, pars) + ")";
            return innerQuery + ")";
        }

        private static string BindAnyNode(AnyNode anyNode, List<SqlParameter> pars)
        {
            string innerQuery = "exists ( from " + Bind(anyNode.Source, pars) + " " + anyNode.RangeVariables.First().Name;
            if (anyNode.Body != null)
            {
                innerQuery += " where " + Bind(anyNode.Body, pars);
            }
            return innerQuery + ")";
        }

        private static string BindNavigationPropertyNode(SingleEntityNode singleEntityNode, IEdmNavigationProperty edmNavigationProperty, List<SqlParameter> pars)
        {
            return Bind(singleEntityNode, pars) + "." + edmNavigationProperty.Name;
        }

        private static string BindSingleValueFunctionCallNode(SingleValueFunctionCallNode singleValueFunctionCallNode, List<SqlParameter> pars)
        {
            var arguments = singleValueFunctionCallNode.Parameters.ToList();
            string name = string.Empty;
            string parName = string.Empty;
            object parValue = null;
            switch (singleValueFunctionCallNode.Name)
            {
                case "concat":
                    List<string> p = new List<string>();
                    foreach (var item in arguments)
                    {
                        parValue = Bind(item, pars);
                        parName = "p" + DateTime.Now.Ticks;
                        p.Add("@" + parName);
                        pars.Add(new SqlParameter(parName, parValue));
                    }
                    return string.Format("concat({0})", string.Join(",", p));
                case "contains":
                    name = Bind(arguments[0], pars);
                    parName = name + pars.Count;
                    parValue = string.Format("%{0}%", (arguments[1] as ConstantNode).Value);
                    pars.Add(new SqlParameter(parName, parValue.ToString()));
                    return string.Format("{0} like @{1}", name, parName);
                case "endswith":
                    name = Bind(arguments[0], pars);
                    parName = name + pars.Count;
                    parValue = string.Format("%{0}", (arguments[1] as ConstantNode).Value);
                    pars.Add(new SqlParameter(parName, parValue.ToString()));
                    return string.Format("{0} like @{1}", name, parName);
                case "startswith":
                    name = Bind(arguments[0], pars);
                    parName = name + pars.Count;
                    parValue = string.Format("{0}%", (arguments[1] as ConstantNode).Value);
                    pars.Add(new SqlParameter(parName, parValue.ToString()));
                    return string.Format("{0} like @{1}", name, parName);
                case "length":
                    return string.Format("len({0})", Bind(arguments[0], pars));
                case "indexof":
                    name = Bind(arguments[0], pars);
                    parName = name + pars.Count;
                    parValue = (arguments[1] as ConstantNode).Value;
                    pars.Add(new SqlParameter(parName, parValue.ToString()));
                    return string.Format("charindex(@{0},{1})", parName, name);
                case "substring":
                    parValue = Bind(arguments[0], pars);
                    parName = "p" + DateTime.Now.Ticks;
                    pars.Add(new SqlParameter(parName, parValue));
                    return string.Format("SUBSTRING(@{0},{1},{2})",
                        parName,
                        (arguments[1] as ConstantNode).Value,
                       arguments.Count > 2 ? (arguments[2] as ConstantNode).Value : 0);
                case "tolower":
                    parValue = Bind(arguments[0], pars);
                    parName = "p" + DateTime.Now.Ticks;
                    pars.Add(new SqlParameter(parName, parValue));
                    return "LOWER(@" + parName + ")";
                case "toupper":
                    parValue = Bind(arguments[0], pars);
                    parName = "p" + DateTime.Now.Ticks;
                    pars.Add(new SqlParameter(parName, parValue));
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
                    pars.Add(new SqlParameter(parName, parValue));
                    return singleValueFunctionCallNode.Name + "(@" + parName + ")";
                default:
                    throw new NotImplementedException();
            }
        }

        private static string BindUnaryOperatorNode(UnaryOperatorNode unaryOperatorNode, List<SqlParameter> pars)
        {
            return ToString(unaryOperatorNode.OperatorKind) + "(" + Bind(unaryOperatorNode.Operand, pars) + ")";
        }

        private static string BindPropertyAccessQueryNode(SingleValuePropertyAccessNode singleValuePropertyAccessNode)
        {
            return singleValuePropertyAccessNode.Property.Name;
        }

        private static string BindRangeVariable(NonResourceRangeVariable nonentityRangeVariable)
        {
            return nonentityRangeVariable.Name;
        }

        private static string BindRangeVariable(ResourceRangeVariable entityRangeVariable)
        {
            return entityRangeVariable.Name;
        }

        private static string BindConvertNode(ConvertNode convertNode, List<SqlParameter> pars)
        {
            return Bind(convertNode.Source, pars);
        }

        private static string BindConstantNode(ConstantNode constantNode)
        {
            if (constantNode.Value is string)
                return String.Format("'{0}'", constantNode.Value);
            else if (constantNode.Value is DateTimeOffset)
                return String.Format("'{0}'", ((DateTimeOffset)constantNode.Value).ToString("yyyy-MM-dd HH:mm:ss"));
            else if (constantNode.Value is Guid)
                return String.Format("'{0}'", constantNode.Value);
            else if (constantNode.Value is bool)
                return (bool)constantNode.Value ? "1" : "0";
            else if (constantNode.Value == null)
                return "null";
            return constantNode.Value.ToString();
        }

        private static string BindBinaryOperatorNode(BinaryOperatorNode binaryOperatorNode, List<SqlParameter> pars)
        {
            var left = Bind(binaryOperatorNode.Left, pars);
            var right = Bind(binaryOperatorNode.Right, pars);
            if (binaryOperatorNode.OperatorKind == BinaryOperatorKind.Equal
                && right == "null")
                return "(" + left + " is null)";
            if (binaryOperatorNode.OperatorKind == BinaryOperatorKind.NotEqual
                && right == "null")
                return "(" + left + " is not null)";
            return "(" + left + " " + ToString(binaryOperatorNode.OperatorKind) + " " + right + ")";
        }

        private static string ToString(BinaryOperatorKind binaryOpertor)
        {
            switch (binaryOpertor)
            {
                case BinaryOperatorKind.Add:
                    return "+";
                case BinaryOperatorKind.And:
                    return "AND";
                case BinaryOperatorKind.Divide:
                    return "/";
                case BinaryOperatorKind.Equal:
                    return "=";
                case BinaryOperatorKind.GreaterThan:
                    return ">";
                case BinaryOperatorKind.GreaterThanOrEqual:
                    return ">=";
                case BinaryOperatorKind.LessThan:
                    return "<";
                case BinaryOperatorKind.LessThanOrEqual:
                    return "<=";
                case BinaryOperatorKind.Modulo:
                    return "%";
                case BinaryOperatorKind.Multiply:
                    return "*";
                case BinaryOperatorKind.NotEqual:
                    return "<>";
                case BinaryOperatorKind.Or:
                    return "OR";
                case BinaryOperatorKind.Subtract:
                    return "-";
                default:
                    return null;
            }
        }

        private static string ToString(UnaryOperatorKind unaryOperator)
        {
            switch (unaryOperator)
            {
                case UnaryOperatorKind.Negate:
                    return "!";
                case UnaryOperatorKind.Not:
                    return "NOT";
                default:
                    return null;
            }
        }

    }
}
