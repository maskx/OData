-- Create the data type
CREATE TYPE [dbo].[UDT_Categories] AS TABLE 
(
	[CategoryName] [nvarchar](15) NOT NULL,
	[Description] [ntext] NULL,
	[Picture] [image] NULL
)
GO

CREATE PROCEDURE dbo.CreateCategory
	@Category dbo.UDT_Categories READONLY
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	 insert into dbo.Categories select CategoryName,[Description],Picture from @Category

END
GO