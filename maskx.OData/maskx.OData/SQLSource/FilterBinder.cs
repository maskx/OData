using maskx.OData.Infrastructure;
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
        public static string ParseFilter(this ODataQueryOptions options, Entity entity, SQLBase dbUtility, List<DbParameter> pars)
        {
            if (options.Filter == null || options.Filter.FilterClause == null)
                return string.Empty;
            string where = ParseFilter(options.Filter.FilterClause, entity, dbUtility, pars);
            if (!string.IsNullOrEmpty(where))
                where = " where " + where;
            return where;
        }
        public static string ParseFilter(this ExpandedNavigationSelectItem expanded, Entity entity, SQLBase dbUtility, List<DbParameter> pars)
        {
            if (expanded == null || expanded.FilterOption == null)
                return string.Empty;
            string where = ParseFilter(expanded.FilterOption, entity, dbUtility, pars);
            if (string.IsNullOrEmpty(where))
                return string.Empty;
            return string.Format(" and ({0}) ", where);
        }
        public static string ParseFilter(this FilterClause filterClause, Entity entity, SQLBase dbUtility, List<DbParameter> pars)
        {
            if (filterClause == null || filterClause.Expression == null)
                return string.Empty;
            return Bind(filterClause.Expression, entity, dbUtility, pars);
        }


        internal static string Bind(QueryNode node, Entity entity, SQLBase dbUtility, List<DbParameter> pars)
        {
            switch (node.Kind)
            {
                case QueryNodeKind.None:
                    break;
                case QueryNodeKind.Constant:
                    return BindConstantNode(node as ConstantNode, entity, pars, dbUtility);
                case QueryNodeKind.Convert:
                    return BindConvertNode(node as ConvertNode, entity, dbUtility, pars);
                case QueryNodeKind.NonResourceRangeVariableReference:
                    return BindRangeVariable((node as NonResourceRangeVariableReferenceNode).RangeVariable, dbUtility);
                case QueryNodeKind.BinaryOperator:
                    return BindBinaryOperatorNode(node as BinaryOperatorNode, entity, dbUtility, pars);
                case QueryNodeKind.UnaryOperator:
                    return BindUnaryOperatorNode(node as UnaryOperatorNode, entity, dbUtility, pars);
                case QueryNodeKind.SingleValuePropertyAccess:
                    return BindPropertyAccessQueryNode(node as SingleValuePropertyAccessNode, dbUtility);
                case QueryNodeKind.CollectionPropertyAccess:
                    return BindCollectionPropertyAccessNode(node as CollectionPropertyAccessNode, dbUtility, entity);
                case QueryNodeKind.SingleValueFunctionCall:
                    return BindSingleValueFunctionCallNode(node as SingleValueFunctionCallNode, entity, dbUtility, pars);
                case QueryNodeKind.Any:
                    return BindAnyNode(node as AnyNode, entity, dbUtility, pars);
                case QueryNodeKind.CollectionNavigationNode:
                case QueryNodeKind.SingleNavigationNode:
                    SingleNavigationNode navigationNode = node as SingleNavigationNode;
                    return BindNavigationPropertyNode(navigationNode.Source, navigationNode.NavigationProperty, entity, dbUtility, pars);
                case QueryNodeKind.SingleValueOpenPropertyAccess:
                    break;
                case QueryNodeKind.SingleResourceCast:
                    break;
                case QueryNodeKind.All:
                    return BindAllNode(node as AllNode, entity, dbUtility, pars);
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

        static string BindCollectionPropertyAccessNode(CollectionPropertyAccessNode collectionPropertyAccessNode, SQLBase dbUtility, Entity entity)
        {
            return dbUtility.SafeDbObject(entity.Properties[collectionPropertyAccessNode.Property.Name].OriginalName);
            //return Bind(collectionPropertyAccessNode.Source) + "." + collectionPropertyAccessNode.Property.Name;
        }

        static string BindNavigationPropertyNode(SingleValueNode singleValueNode, IEdmNavigationProperty edmNavigationProperty, Entity entity, SQLBase dbUtility, List<DbParameter> pars)
        {
            return Bind(singleValueNode, entity, dbUtility, pars) + "." + dbUtility.SafeDbObject(edmNavigationProperty.Name);
        }
        static string BindNavigationPropertyNode(SingleEntityNode singleEntityNode, IEdmNavigationProperty edmNavigationProperty, Entity entityy, SQLBase dbUtility, List<DbParameter> pars)
        {
            return Bind(singleEntityNode, entityy, dbUtility, pars) + "." + edmNavigationProperty.Name;
        }
        static string BindUnaryOperatorNode(UnaryOperatorNode unaryOperatorNode, Entity entity, SQLBase dbUtility, List<DbParameter> pars)
        {
            return BindUnaryOperatorKind(unaryOperatorNode.OperatorKind) + "(" + Bind(unaryOperatorNode.Operand, entity, dbUtility, pars) + ")";
        }
        static string BindPropertyAccessQueryNode(SingleValuePropertyAccessNode singleValuePropertyAccessNode, SQLBase dbUtility)
        {
            return dbUtility.SafeDbObject(singleValuePropertyAccessNode.Property.Name);
        }

        static string BindRangeVariable(NonResourceRangeVariable nonentityRangeVariable, SQLBase dbUtility)
        {
            return nonentityRangeVariable.Name;
        }

        static string BindRangeVariable(ResourceRangeVariable entityRangeVariable, SQLBase dbUtility)
        {
            return entityRangeVariable.Name;
        }

        static string BindConvertNode(ConvertNode convertNode, Entity entity, SQLBase dbUtility, List<DbParameter> pars)
        {
            return Bind(convertNode.Source, entity, dbUtility, pars);
        }
        static string BindBinaryOperatorKind(BinaryOperatorKind binaryOpertor, SQLBase dbUtility)
        {
            switch (binaryOpertor)
            {
                case BinaryOperatorKind.Add:
                    return "+";
                case BinaryOperatorKind.And:
                    return " AND ";
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
                    return " OR ";
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
                    return " NOT ";
                default:
                    return null;
            }
        }
        static string BindConstantNode(ConstantNode constantNode, Entity entity, List<DbParameter> pars, SQLBase dbUtility)
        {
            return dbUtility.CreateParameter(constantNode.Value, pars).ParameterName;
        }
        static string BindAllNode(AllNode allNode, Entity entity, SQLBase dbUtility, List<DbParameter> pars)
        {
            string innerQuery = "not exists ( from " + Bind(allNode.Source, entity, dbUtility, pars) + " " + allNode.RangeVariables.First().Name;
            innerQuery += " where NOT(" + Bind(allNode.Body, entity, dbUtility, pars) + ")";
            return innerQuery + ")";
        }

        static string BindAnyNode(AnyNode anyNode, Entity entity, SQLBase dbUtility, List<DbParameter> pars)
        {
            string innerQuery = "exists ( from " + Bind(anyNode.Source, entity, dbUtility, pars) + " " + anyNode.RangeVariables.First().Name;
            if (anyNode.Body != null)
            {
                innerQuery += " where " + Bind(anyNode.Body, entity, dbUtility, pars);
            }
            return innerQuery + ")";
        }
        static string BindSingleValueFunctionCallNode(SingleValueFunctionCallNode node, Entity entity, SQLBase dbUtility, List<DbParameter> pars)
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
                        p.Add(Bind(item, entity, dbUtility, pars));
                    }
                    return string.Format("concat({0})", string.Join(",", p));
                case "contains":
                    dbpar = dbUtility.CreateParameter(parValue, pars);
                    return string.Format("{0} like '%'+{1}+'%'", Bind(arguments[0], entity, dbUtility, pars), Bind(arguments[1], entity, dbUtility, pars));
                case "endswith":
                    return string.Format("{0} like '%'+{1}", Bind(arguments[0], entity, dbUtility, pars), Bind(arguments[1], entity, dbUtility, pars));
                case "startswith":
                    return string.Format("{0} like {1}+'%'", Bind(arguments[0], entity, dbUtility, pars), Bind(arguments[1], entity, dbUtility, pars));
                case "length":
                    return string.Format("len({0})", Bind(arguments[0], entity, dbUtility, pars));
                case "indexof":
                    return string.Format("charindex({0},{1})", Bind(arguments[0], entity, dbUtility, pars), Bind(arguments[1], entity, dbUtility, pars));
                case "substring":
                    return string.Format("SUBSTRING({0},{1},{2})",
                        Bind(arguments[0], entity, dbUtility, pars),
                        Bind(arguments[1], entity, dbUtility, pars),
                       arguments.Count > 2 ? Bind(arguments[2], entity, dbUtility, pars) : 0);
                case "tolower":
                    return "LOWER(" + Bind(arguments[0], entity, dbUtility, pars) + ")";
                case "toupper":
                    return "UPPER(" + Bind(arguments[0], entity, dbUtility, pars) + ")";
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
                    return node.Name + "(" + Bind(arguments[0], entity, dbUtility, pars) + ")";
                default:
                    throw new NotImplementedException();
            }
        }
        static string BindBinaryOperatorNode(BinaryOperatorNode binaryOperatorNode, Entity entity, SQLBase dbUtility, List<DbParameter> pars)
        {
            var left = Bind(binaryOperatorNode.Left, entity, dbUtility, pars);
            var right = Bind(binaryOperatorNode.Right, entity, dbUtility, pars);
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
