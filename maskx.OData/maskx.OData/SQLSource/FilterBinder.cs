using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace maskx.OData.SQLSource
{
    public static class FilterBinder
    {
        public static string ParseFilter(this ODataQueryOptions options, List<DbParameter> pars, DbUtility dbUtility)
        {
            if (options.Filter == null || options.Filter.FilterClause == null)
                return string.Empty;
            string where = ParseFilter(options.Filter.FilterClause, pars, dbUtility);
            if (!string.IsNullOrEmpty(where))
                where = " where " + where;
            return where;
        }
        public static string ParseFilter(this ExpandedNavigationSelectItem expanded, List<DbParameter> pars, DbUtility dbUtility)
        {
            if (expanded == null || expanded.FilterOption == null)
                return string.Empty;
            string where = ParseFilter(expanded.FilterOption, pars, dbUtility);
            if (string.IsNullOrEmpty(where))
                return string.Empty;
            return string.Format(" and ({0}) ", where);
        }
        public static string ParseFilter(this FilterClause filterClause, List<DbParameter> pars, DbUtility dbUtility)
        {
            if (filterClause == null || filterClause.Expression == null)
                return string.Empty;
            return Bind(filterClause.Expression, pars, dbUtility);
        }


        internal static string Bind(QueryNode node, List<DbParameter> pars, DbUtility dbUtility)
        {
            switch (node.Kind)
            {
                case QueryNodeKind.None:
                    break;
                case QueryNodeKind.Constant:
                    return BindConstantNode(node as ConstantNode, pars, dbUtility);
                case QueryNodeKind.Convert:
                    return BindConvertNode(node as ConvertNode, pars, dbUtility);
                case QueryNodeKind.NonResourceRangeVariableReference:
                    return BindRangeVariable((node as NonResourceRangeVariableReferenceNode).RangeVariable, dbUtility);
                case QueryNodeKind.BinaryOperator:
                    return BindBinaryOperatorNode(node as BinaryOperatorNode, pars, dbUtility);
                case QueryNodeKind.UnaryOperator:
                    return BindUnaryOperatorNode(node as UnaryOperatorNode, pars, dbUtility);
                case QueryNodeKind.SingleValuePropertyAccess:
                    return BindPropertyAccessQueryNode(node as SingleValuePropertyAccessNode, dbUtility);
                case QueryNodeKind.CollectionPropertyAccess:
                    return BindCollectionPropertyAccessNode(node as CollectionPropertyAccessNode, dbUtility);
                case QueryNodeKind.SingleValueFunctionCall:
                    return BindSingleValueFunctionCallNode(node as SingleValueFunctionCallNode, pars, dbUtility);
                case QueryNodeKind.Any:
                    return BindAnyNode(node as AnyNode, pars, dbUtility);
                case QueryNodeKind.CollectionNavigationNode:
                case QueryNodeKind.SingleNavigationNode:
                    SingleNavigationNode navigationNode = node as SingleNavigationNode;
                    return BindNavigationPropertyNode(navigationNode.Source, navigationNode.NavigationProperty, pars, dbUtility);
                case QueryNodeKind.SingleValueOpenPropertyAccess:
                    break;
                case QueryNodeKind.SingleResourceCast:
                    break;
                case QueryNodeKind.All:
                    return BindAllNode(node as AllNode, pars, dbUtility);
                case QueryNodeKind.CollectionResourceCast:
                    break;
                case QueryNodeKind.ResourceRangeVariableReference:
                    return BindRangeVariable((node as ResourceRangeVariableReferenceNode).RangeVariable, dbUtility);
                case QueryNodeKind.SingleResourceFunctionCall:
                    break;
                case QueryNodeKind.CollectionFunctionCall:
                    break;
                case QueryNodeKind.CollectionResourceFunctionCall:
                    break;
                case QueryNodeKind.NamedFunctionParameter:
                    break;
                case QueryNodeKind.ParameterAlias:
                    break;
                case QueryNodeKind.EntitySet:
                    break;
                case QueryNodeKind.KeyLookup:
                    break;
                case QueryNodeKind.SearchTerm:
                    break;
                case QueryNodeKind.CollectionOpenPropertyAccess:
                    break;
                case QueryNodeKind.CollectionComplexNode:
                    break;
                case QueryNodeKind.SingleComplexNode:
                    break;
                case QueryNodeKind.Count:
                    break;
                case QueryNodeKind.SingleValueCast:
                    break;
                default:
                    return string.Empty;
            }
            return string.Empty;
        }

        static string BindCollectionPropertyAccessNode(CollectionPropertyAccessNode collectionPropertyAccessNode, DbUtility dbUtility)
        {
            return dbUtility.SafeDbObject(collectionPropertyAccessNode.Property.Name);
            //return Bind(collectionPropertyAccessNode.Source) + "." + collectionPropertyAccessNode.Property.Name;
        }

        static string BindNavigationPropertyNode(SingleValueNode singleValueNode, IEdmNavigationProperty edmNavigationProperty, List<DbParameter> pars, DbUtility dbUtility)
        {
            return Bind(singleValueNode, pars, dbUtility) + "." + dbUtility.SafeDbObject(edmNavigationProperty.Name);
        }
        static string BindNavigationPropertyNode(SingleEntityNode singleEntityNode, IEdmNavigationProperty edmNavigationProperty, List<DbParameter> pars, DbUtility dbUtility)
        {
            return Bind(singleEntityNode, pars, dbUtility) + "." + edmNavigationProperty.Name;
        }
        static string BindUnaryOperatorNode(UnaryOperatorNode unaryOperatorNode, List<DbParameter> pars, DbUtility dbUtility)
        {
            return BindUnaryOperatorKind(unaryOperatorNode.OperatorKind) + "(" + Bind(unaryOperatorNode.Operand, pars, dbUtility) + ")";
        }
        static string BindPropertyAccessQueryNode(SingleValuePropertyAccessNode singleValuePropertyAccessNode, DbUtility dbUtility)
        {
            return singleValuePropertyAccessNode.Property.Name;
        }

        static string BindRangeVariable(NonResourceRangeVariable nonentityRangeVariable, DbUtility dbUtility)
        {
            return nonentityRangeVariable.Name;
        }

        static string BindRangeVariable(ResourceRangeVariable entityRangeVariable, DbUtility dbUtility)
        {
            return entityRangeVariable.Name;
        }

        static string BindConvertNode(ConvertNode convertNode, List<DbParameter> pars, DbUtility dbUtility)
        {
            return Bind(convertNode.Source, pars, dbUtility);
        }
        static string BindBinaryOperatorKind(BinaryOperatorKind binaryOpertor, DbUtility dbUtility)
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
        static string BindUnaryOperatorKind(UnaryOperatorKind unaryOperator)
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
        static string BindConstantNode(ConstantNode constantNode, List<DbParameter> pars, DbUtility dbUtility)
        {
            return dbUtility.CreateParameter(constantNode.Value, pars).ParameterName;
        }
        static string BindAllNode(AllNode allNode, List<DbParameter> pars, DbUtility dbUtility)
        {
            string innerQuery = "not exists ( from " + Bind(allNode.Source, pars, dbUtility) + " " + allNode.RangeVariables.First().Name;
            innerQuery += " where NOT(" + Bind(allNode.Body, pars, dbUtility) + ")";
            return innerQuery + ")";
        }

        static string BindAnyNode(AnyNode anyNode, List<DbParameter> pars, DbUtility dbUtility)
        {
            string innerQuery = "exists ( from " + Bind(anyNode.Source, pars, dbUtility) + " " + anyNode.RangeVariables.First().Name;
            if (anyNode.Body != null)
            {
                innerQuery += " where " + Bind(anyNode.Body, pars, dbUtility);
            }
            return innerQuery + ")";
        }
        static string BindSingleValueFunctionCallNode(SingleValueFunctionCallNode node, List<DbParameter> pars, DbUtility dbUtility)
        {
            var arguments = node.Parameters.ToList();
            string name = string.Empty;
            string parName = string.Empty;
            object parValue = null;
            DbParameter dbpar = null;
            switch (node.Name)
            {
                case "concat":
                    List<string> p = new List<string>();
                    foreach (var item in arguments)
                    {
                        parValue = Bind(item, pars, dbUtility);
                        dbpar = dbUtility.CreateParameter(parValue, pars);
                        p.Add(dbpar.ParameterName);
                    }
                    return string.Format("concat({0})", string.Join(",", p));
                case "contains":
                    name = dbUtility.SafeDbObject(Bind(arguments[0], pars, dbUtility));
                    parValue = string.Format("%{0}%", (arguments[1] as ConstantNode).Value);
                    dbpar = dbUtility.CreateParameter(parValue, pars);
                    return string.Format("{0} like {1}", name, dbpar.ParameterName);
                case "endswith":
                    name = dbUtility.SafeDbObject(Bind(arguments[0], pars, dbUtility));
                    parValue = string.Format("%{0}", (arguments[1] as ConstantNode).Value);
                    dbpar = dbUtility.CreateParameter(parValue, pars);
                    return string.Format("{0} like {1}", name, dbpar.ParameterName);
                case "startswith":
                    name = Bind(arguments[0], pars, dbUtility);
                    parValue = string.Format("{0}%", (arguments[1] as ConstantNode).Value);
                    dbpar = dbUtility.CreateParameter(parValue, pars);
                    return string.Format("{0} like {1}", name, parName);
                case "length":
                    return string.Format("len({0})", Bind(arguments[0], pars, dbUtility));
                case "indexof":
                    name = dbUtility.SafeDbObject(Bind(arguments[0], pars, dbUtility));
                    parValue = (arguments[1] as ConstantNode).Value;
                    dbpar = dbUtility.CreateParameter(parValue, pars);
                    return string.Format("charindex({0},{1})", dbpar.ParameterName, name);
                case "substring":
                    parValue = Bind(arguments[0], pars, dbUtility);
                    dbpar = dbUtility.CreateParameter(parValue, pars);
                    return string.Format("SUBSTRING({0},{1},{2})",
                        dbpar.ParameterName,
                        (arguments[1] as ConstantNode).Value,
                       arguments.Count > 2 ? (arguments[2] as ConstantNode).Value : 0);
                case "tolower":
                    parValue = Bind(arguments[0], pars, dbUtility);
                    dbpar = dbUtility.CreateParameter(parValue, pars);
                    return "LOWER(" + dbpar.ParameterName + ")";
                case "toupper":
                    parValue = Bind(arguments[0], pars, dbUtility);
                    dbpar = dbUtility.CreateParameter(parValue, pars);
                    return "UPPER(" + parName + ")";
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
                    parValue = Bind(arguments[0], pars, dbUtility);
                    dbpar = dbUtility.CreateParameter(parValue, pars);
                    return node.Name + "(" + parName + ")";
                default:
                    throw new NotImplementedException();
            }
        }
        static string BindBinaryOperatorNode(BinaryOperatorNode binaryOperatorNode, List<DbParameter> pars, DbUtility dbUtility)
        {
            var left = Bind(binaryOperatorNode.Left, pars, dbUtility);
            var right = Bind(binaryOperatorNode.Right, pars, dbUtility);
            if (binaryOperatorNode.OperatorKind == BinaryOperatorKind.Equal
                && right == "null")
                return "(" + left + " is null)";
            if (binaryOperatorNode.OperatorKind == BinaryOperatorKind.NotEqual
                && right == "null")
                return "(" + left + " is not null)";
            return "(" + left + " " + BindBinaryOperatorKind(binaryOperatorNode.OperatorKind, dbUtility) + " " + right + ")";

        }


    }
}
