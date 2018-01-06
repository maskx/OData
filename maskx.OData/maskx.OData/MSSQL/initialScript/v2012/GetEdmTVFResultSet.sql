
/****** Object:  StoredProcedure [dbo].[GetEdmTVFResultSet]    Script Date: 2015/8/31 11:44:21 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		maskx
-- Create date: 2015-8-31
-- Description:	get table valued function result set
-- =============================================
CREATE PROCEDURE [dbo].[GetEdmTVFResultSet] 
	@Name nvarchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select
		col.name as COLUMN_NAME
		,type_name(col.user_type_id) as DATA_TYPE
	from sys.all_columns as col
	where col.object_id=object_id(@Name)
END

GO


