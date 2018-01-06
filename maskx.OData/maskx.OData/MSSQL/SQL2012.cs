namespace maskx.OData.Sql
{
    public class SQL2012 : SQLDataSource
    {
        public SQL2012(string name, string connectionString,
            string modelCommand = "GetEdmModelInfo",
            string actionCommand = "GetEdmSPInfo",
            string functionCommand = "GetEdmTVFInfo",
            string relationCommand = "GetEdmRelationship",
            string storedProcedureResultSetCommand = "GetEdmSPResultSet",
            string userDefinedTableCommand = "GetEdmUDTInfo",
            string tableValuedResultSetCommand = "GetEdmTVFResultSet"):base(
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
    }
}
