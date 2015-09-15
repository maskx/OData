

/****** Object:  StoredProcedure [dbo].[GetEdmUDTInfo]    Script Date: 2015/8/31 11:45:41 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		maskx
-- Create date: 2015-8-31
-- Description:	get user-defined table types information
-- =============================================
CREATE PROCEDURE [dbo].[GetEdmUDTInfo] 
	@Name nvarchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select  
		c.name
		,type_name(c.user_type_id) as ColumnType
		,c.max_length as ColumnLength
		,c.is_nullable as ColumnIsNullable
	from sys.table_types tt
		inner join sys.columns c on c.object_id = tt.type_table_object_id
	where tt.name =@Name
END

GO


