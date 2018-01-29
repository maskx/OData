using Microsoft.AspNet.OData.Query;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;

namespace maskx.OData.Database
{
    public abstract class FilterBinder
    {
        internal string ParseFilter(ODataQueryOptions options, List<DbParameter> pars)
        {
            if (options.Filter == null || options.Filter.FilterClause == null)
                return string.Empty;
            string where = ParseFilter(options.Filter.FilterClause, pars);
            if (!string.IsNullOrEmpty(where))
                where = " where " + where;
            return where;
        }
        internal string ParseFilter(ExpandedNavigationSelectItem expanded, List<DbParameter> pars)
        {
            if (expanded == null || expanded.FilterOption == null)
                return string.Empty;
            string where = ParseFilter(expanded.FilterOption, pars);
            if (string.IsNullOrEmpty(where))
                return string.Empty;
            return string.Format(" and ({0}) ", where);
        }
        public string ParseFilter(FilterClause filterClause, List<DbParameter> pars)
        {
            if (filterClause == null || filterClause.Expression == null)
                return string.Empty;
            return Bind(filterClause.Expression, pars);
        }


        internal string Bind(QueryNode node, List<DbParameter> pars)
        {
            switch (node.Kind)
            {
                case QueryNodeKind.None:
                    break;
                case QueryNodeKind.Constant:
                    return BindConstantNode(node as ConstantNode,pars);
                case QueryNodeKind.Convert:
                    return BindConvertNode(node as ConvertNode, pars);
                case QueryNodeKind.NonResourceRangeVariableReference:
                    return BindRangeVariable((node as NonResourceRangeVariableReferenceNode).RangeVariable);
                case QueryNodeKind.BinaryOperator:
                    return BindBinaryOperatorNode(node as BinaryOperatorNode, pars);
                case QueryNodeKind.UnaryOperator:
                    return BindUnaryOperatorNode(node as UnaryOperatorNode, pars);
                case QueryNodeKind.SingleValuePropertyAccess:
                    return BindPropertyAccessQueryNode(node as SingleValuePropertyAccessNode);
                case QueryNodeKind.CollectionPropertyAccess:
                    return BindCollectionPropertyAccessNode(node as CollectionPropertyAccessNode);
                case QueryNodeKind.SingleValueFunctionCall:
                    return BindSingleValueFunctionCallNode(node as SingleValueFunctionCallNode, pars);
                case QueryNodeKind.Any:
                    return BindAnyNode(node as AnyNode, pars);
                case QueryNodeKind.CollectionNavigationNode:
                case QueryNodeKind.SingleNavigationNode:
                    SingleNavigationNode navigationNode = node as SingleNavigationNode;
                    return BindNavigationPropertyNode(navigationNode.Source, navigationNode.NavigationProperty, pars);
                case QueryNodeKind.SingleValueOpenPropertyAccess:
                    break;
                case QueryNodeKind.SingleResourceCast:
                    break;
                case QueryNodeKind.All:
                    return BindAllNode(node as AllNode, pars);
                case QueryNodeKind.CollectionResourceCast:
                    break;
                case QueryNodeKind.ResourceRangeVariableReference:
                    return BindRangeVariable((node as ResourceRangeVariableReferenceNode).RangeVariable);
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

        private string BindCollectionPropertyAccessNode(CollectionPropertyAccessNode collectionPropertyAccessNode)
        {
            return collectionPropertyAccessNode.Property.Name;
            //return Bind(collectionPropertyAccessNode.Source) + "." + collectionPropertyAccessNode.Property.Name;
        }

        private string BindNavigationPropertyNode(SingleValueNode singleValueNode, IEdmNavigationProperty edmNavigationProperty, List<DbParameter> pars)
        {
            return Bind(singleValueNode, pars) + "." + edmNavigationProperty.Name;
        }
        private string BindNavigationPropertyNode(SingleEntityNode singleEntityNode, IEdmNavigationProperty edmNavigationProperty, List<DbParameter> pars)
        {
            return Bind(singleEntityNode, pars) + "." + edmNavigationProperty.Name;
        }
        private string BindUnaryOperatorNode(UnaryOperatorNode unaryOperatorNode, List<DbParameter> pars)
        {
            return BindUnaryOperatorKind(unaryOperatorNode.OperatorKind) + "(" + Bind(unaryOperatorNode.Operand, pars) + ")";
        }
        private string BindPropertyAccessQueryNode(SingleValuePropertyAccessNode singleValuePropertyAccessNode)
        {
            return singleValuePropertyAccessNode.Property.Name;
        }

        private string BindRangeVariable(NonResourceRangeVariable nonentityRangeVariable)
        {
            return nonentityRangeVariable.Name;
        }

        private string BindRangeVariable(ResourceRangeVariable entityRangeVariable)
        {
            return entityRangeVariable.Name;
        }

        private string BindConvertNode(ConvertNode convertNode, List<DbParameter> pars)
        {
            return Bind(convertNode.Source, pars);
        }

        #region virtual
        public virtual string BindBinaryOperatorKind(BinaryOperatorKind binaryOpertor)
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
        public virtual string BindUnaryOperatorKind(UnaryOperatorKind unaryOperator)
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
        public virtual string BindConstantNode(ConstantNode constantNode,List<DbParameter> pars)
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
        public virtual string BindAllNode(AllNode allNode, List<DbParameter> pars)
        {
            string innerQuery = "not exists ( from " + Bind(allNode.Source, pars) + " " + allNode.RangeVariables.First().Name;
            innerQuery += " where NOT(" + Bind(allNode.Body, pars) + ")";
            return innerQuery + ")";
        }

        public virtual string BindAnyNode(AnyNode anyNode, List<DbParameter> pars)
        {
            string innerQuery = "exists ( from " + Bind(anyNode.Source, pars) + " " + anyNode.RangeVariables.First().Name;
            if (anyNode.Body != null)
            {
                innerQuery += " where " + Bind(anyNode.Body, pars);
            }
            return innerQuery + ")";
        }
        #endregion
        #region abstract
        public abstract string BindSingleValueFunctionCallNode(SingleValueFunctionCallNode node, List<DbParameter> pars);
        public abstract string BindBinaryOperatorNode(BinaryOperatorNode node, List<DbParameter> pars);
        public abstract DbParameter CreateSqlParameter(string name, object value);
        #endregion

    }
}
