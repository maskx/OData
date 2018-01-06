using Microsoft.AspNet.OData.Query;
using Microsoft.OData.UriParser;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace maskx.OData.Sql
{
    public class SQL2008 : SQLDataSource
    {
        public SQL2008(string name, string connectionString,
            string modelCommand = "GetEdmModelInfo",
            string actionCommand = "GetEdmSPInfo",
            string functionCommand = "GetEdmTVFInfo",
            string relationCommand = "GetEdmRelationship",
            string storedProcedureResultSetCommand = "GetEdmSPResultSet",
            string userDefinedTableCommand = "GetEdmUDTInfo",
            string tableValuedResultSetCommand = "GetEdmTVFResultSet") : base(
                name,
                connectionString,
                modelCommand,
                actionCommand,
                functionCommand,
                relationCommand,
                storedProcedureResultSetCommand,
                userDefinedTableCommand,
                tableValuedResultSetCommand)
        { }

        internal override string BuildSqlQueryCmd(ExpandedNavigationSelectItem expanded, string condition, List<SqlParameter> pars)
        {
            string table = string.Format("[{0}]", expanded.NavigationSource.Name);
            string cmdTxt = string.Empty;
            
            if (!expanded.CountOption.HasValue && expanded.TopOption.HasValue)
            {
                if (expanded.SkipOption.HasValue)
                {
                    cmdTxt = string.Format(
@"select t.* from(
select ROW_NUMBER() over ({0}) as rowIndex,{1} from {2} {3}
) as t
where t.rowIndex between {4} and {5}"
                     , expanded.ParseOrderBy()
                     , expanded.ParseSelect()
                     , table
                     , expanded.ParseFilter(condition,pars)
                     , expanded.SkipOption.Value + 1
                     , expanded.SkipOption.Value + expanded.TopOption.Value);
                }
                else
                    cmdTxt = string.Format("select top {0} {1} from {2} {3} {4}"
                       , expanded.TopOption.Value
                       , expanded.ParseSelect()
                       , table
                       , expanded.ParseFilter(condition,pars)
                       , expanded.ParseOrderBy());

            }
            else
            {
                cmdTxt = string.Format("select  {0}  from {1} {2} {3} "
                         , expanded.ParseSelect()
                         , table
                         , expanded.ParseFilter(condition,pars)
                         , expanded.ParseOrderBy());
            }

            return cmdTxt;
        }
        internal override string BuildSqlQueryCmd(ODataQueryOptions options, List<SqlParameter> pars,string target = "")
        {
            var cxt = options.Context;
            string table = target;
            if (string.IsNullOrEmpty(target))
                table = string.Format("[{0}]", cxt.Path.Segments[0]);

            string cmdTxt = string.Empty;
            if (options.Count == null && options.Top != null)
            {
                if (options.Skip != null)
                {
                    cmdTxt = string.Format(
@"select t.* from(
select ROW_NUMBER() over ({0}) as rowIndex,{1} from {2} {3}
) as t
where t.rowIndex between {4} and {5}"
                     , options.ParseOrderBy()
                     , options.ParseSelect()
                     , table
                     , options.ParseFilter(pars)
                     , options.Skip.Value + 1
                     , options.Skip.Value + options.Top.Value);
                }
                else
                    cmdTxt = string.Format("select top {0} {1} from {2} {3} {4}"
                        , options.Top.RawValue
                        , options.ParseSelect()
                        , table
                        , options.ParseFilter(pars)
                        , options.ParseOrderBy());
            }
            else
            {
                cmdTxt = string.Format("select  {0}  from {1} {2} {3} "
                         , options.ParseSelect()
                         , table
                         , options.ParseFilter(pars)
                         , options.ParseOrderBy());
            }
            return cmdTxt;
        }
    }
}
