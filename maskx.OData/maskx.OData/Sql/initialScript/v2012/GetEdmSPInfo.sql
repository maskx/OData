
/****** Object:  StoredProcedure [dbo].[GetEdmFuncInfo]    Script Date: 2015/8/24 20:56:57 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


-- =============================================
-- Author:		maskx
-- Create date: 2015-6-10
-- Description:	Get Stored Procedures information 
-- =============================================
CREATE PROCEDURE [dbo].[GetEdmSPInfo]
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

   select	
		p.name as SPECIFIC_NAME
		,par.PARAMETER_NAME
		,par.PARAMETER_MODE
		,par.DATA_TYPE
		,par.USER_DEFINED_TYPE_NAME
		,isnull(par.CHARACTER_MAXIMUM_LENGTH,par.NUMERIC_PRECISION) as MAX_LENGTH
		,par.NUMERIC_SCALE as SCALE
	from sys.procedures as p
		left join INFORMATION_SCHEMA.PARAMETERS as par on par.SPECIFIC_NAME=p.name
	order by p.name
END
GO


