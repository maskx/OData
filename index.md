# maskx.Odata
Dynamic generate OData Controller from SQL server.

# Nuget

# Usage
## SQL Config
execute the database initial script：(at folder Sql/initialScript/ )

*  GetEdmFuncInfo.sql
*  GetEdmModelInfo.sql 
*  GetEdmRelationship.sql  
*  GetEdmTVFInfo.sql

## Server config
1.  In OWIN Startup class Configuration function add follow code： 
`using maskx.OData;

config.Routes.MapDynamicODataServiceRoute("routeName","routePrefix");

DynamicOData.AddDataSource(new maskx.OData.Sql.DataSource("db", "ConnectionString"));`

## Client usage
###Query：
	$.get('odata/db/<表/视图/表值函数()>/<查询条件>')		
	查询条件参考：
		http://www.asp.net/web-api/overview/odata-support-in-aspnet-web-api/supporting-odata-query-options	
		http://docs.oasis-open.org/odata/odata/v4.0/errata02/os/complete/part2-url-conventions/odata-v4.0-errata02-os-part2-url-conventions-complete.html#_Toc406398094
	返回值：
		data.value 是查询的结果集

查询可以使用封装好的类库： 'common/search'
search.js 使用方法：
1. 参考 \Scripts\app\train\home.js 添加search.js 引用
2. 创建Search实例，需要两个参数,形如： new search('tablename','name desc')
	1) 表/视图/表值函数() 名称
	2) 排序方式， 形如 'Name desc'
3. 调用Search() 方法执行查询，Search方法有个一个可选参数，当设定为fasle时，将不查询总的记录数, 此种调用适用于改变排序方式等不影响查询结果数量的操作，可以减轻服务端的压力
4. searrch.js 
	属性说明：
		名称				类型					说明
		Filter			Array				查询条件，多个条件以 and 连接
		OrderBy		string				排序条件
		RowCount		observable			每页的数据条目数量
		TotalCount		observable			总数据条目数
		TotalPages		observableArray		分页数量
		NavigatePages	observableArray		展示层显示的导航页面数
		CurrentPage		observable			当前显示的页面号
		Result			observableArray		查询结果
		HasNext		observable			是否还有下一页
		HasPre			observable			是否还有上一页
	方法说明：		
		1.	Search			执行到服务端查询数据操作					
			参数：
				includeCount	是否要返回总的数据条目数，当设定为fasle时，将不查询总的记录数, 此种调用适用于改变排序方式等不影响查询结果数量的操作，可以减轻服务端的压力
			返回值：
				无，查询结果将更新到 Result属性
		2.	GoPage			跳转到指定页码
			参数：
				p				要跳转到的页码
			返回值：
				无，查询结果将更新到 Result属性
		3.	GoNext			跳转到下一页
		4.	GoPre			跳转到上一页
		5.	GoFirst		跳转到第一页
		6.	GoLast			跳转到最后一页

存储过程调用：
	$.post('odata/db/<存储过程名()>',<存储过程参数>)		存储过程参数 为json对象，name 是存储过程参数名，value是存储过程参数的值
	返回值：
		1.	当存储过程没有输出参数时
				data.value 是存储过程返回的结果集
		2.	当存储过程有输出参数时：
				data.<输出参数名>		是存储过程返回的输出参数值
				data.$Result		是存储过程返回的结果集
标量函数调用：
		略


OData webapi 权限控制：
对OData WebApi的访问控制在表Authorization中配置
字段说明：
	Role：	角色名称，Anonymous 代表匿名用户， 其他角色为AspNetRoles表中角色名(Name 字段)
	Action：	对应WebApi的操作：
		值					对应WebApi操作
		Count				$count
		Func				存储过程调用/标量函数调用
		FuncResultCount		表值函数查询使用 $count
		Get					Get 查询
	Target：	数据对象名（表名/视图名/存储过程名/函数名）