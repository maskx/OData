/****** Object:  StoredProcedure [dbo].[GetEdmSPInfo]    Script Date: 1/20/2018 20:35:57 ******/
DROP PROCEDURE [dbo].[GetEdmSPInfo]
GO

/****** Object:  StoredProcedure [dbo].[GetEdmSPInfo]    Script Date: 1/20/2018 20:35:57 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



-- =============================================
-- Author:		maskx
-- Create date: 2018-1-19
-- Description:	Get Stored Procedures information 
-- =============================================
CREATE PROCEDURE [dbo].[GetEdmSPInfo]
AS
BEGIN

	SET NOCOUNT ON;

   select
		p.[name] as SPECIFIC_NAME
		,[s].[name] as [SCHEMA_NAME]
		,par.PARAMETER_NAME
		,par.PARAMETER_MODE
		,par.DATA_TYPE
		,par.USER_DEFINED_TYPE_NAME
		,par.USER_DEFINED_TYPE_SCHEMA
		,isnull(par.CHARACTER_MAXIMUM_LENGTH,par.NUMERIC_PRECISION) as MAX_LENGTH
		,par.NUMERIC_SCALE as NUMERIC_SCALE
	from sys.procedures as p
		JOIN [sys].[schemas] as [s] on [s].[schema_id]=[p].[schema_id]
		LEFT JOIN INFORMATION_SCHEMA.PARAMETERS as par on par.SPECIFIC_NAME=p.[name]
	order by p.[name]
END

GO


