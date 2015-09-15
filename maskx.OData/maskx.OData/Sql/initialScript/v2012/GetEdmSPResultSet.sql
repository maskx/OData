/****** Object:  StoredProcedure [dbo].[GetEdmSPResultSet]    Script Date: 2015/8/31 11:43:28 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		maskx
-- Create date: 2015-8-31
-- Description:	get the first result set of stored procedures
-- =============================================
CREATE PROCEDURE [dbo].[GetEdmSPResultSet] 
	@Name nvarchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	SELECT 
		name as COLUMN_NAME
		,TYPE_NAME(system_type_id) as DATA_TYPE
	FROM sys.dm_exec_describe_first_result_set_for_object 
	(
		OBJECT_ID(@Name), 
		NULL
	)	
END

GO


