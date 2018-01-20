
/****** Object:  StoredProcedure [dbo].[GetEdmUDTInfo]    Script Date: 1/20/2018 20:38:24 ******/
DROP PROCEDURE [dbo].[GetEdmUDTInfo]
GO

/****** Object:  StoredProcedure [dbo].[GetEdmUDTInfo]    Script Date: 1/20/2018 20:38:24 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		maskx
-- Create date: 2018-1-20
-- Description:	get user-defined table types information
-- =============================================
CREATE PROCEDURE [dbo].[GetEdmUDTInfo] 
	@NAME nvarchar(100),
	@SCHEMA_NAME nvarchar(100)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select  
		c.[name] as [COLUMN_NAME]
		,s.[name] as [SCHEMA_NAME]
		,type_name(c.user_type_id) as [DATA_TYPE]
		,c.max_length as [COLUMN_LENGTH]
		,c.is_nullable as [IS_NULLABLE]
	from sys.table_types tt
		INNER JOIN sys.columns c on c.object_id = tt.type_table_object_id
		INNER JOIN [sys].[schemas] as [s] on [s].[schema_id]=[tt].[schema_id]
	where tt.[name] =@NAME
		and s.[name]=@SCHEMA_NAME
END

GO


