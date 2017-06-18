
# Introduction

Dynamic generate OData Controller from SQL Server. 
It expose the database to client through web API. 
Through this generated web API, client can query/insert/delete/update the database table , invoke the stored procedure, query the view and table-valued function.  
the web API is followed the OData protocol ([http://www.odata.org/](http://www.odata.org/)) 


# Install

## Database setup
In your database execute the database initial script, the initial script locate at folder 'Sql/initialScript/' 
[https://github.com/maskx/OData/tree/master/maskx.OData/maskx.OData/Sql/initialScript/v2012](https://github.com/maskx/OData/tree/master/maskx.OData/maskx.OData/Sql/initialScript/v2012)

those scripts will create the stored procedures query the database schema for build web API
~ Note
those scripts is for SQL server 2012 and beyond, and for SQL Server 2008, you should use the scripts in v2008 folder, it will need you do more configure.
~

## WebApi setup
### Create a web API project

### Install odata nuget package 

[https://www.nuget.org/packages/maskx.OData/](https://www.nuget.org/packages/maskx.OData/)

### Configure the controller
```CSharp
 configuration.Routes.MapDynamicODataServiceRoute("odata","odata");
 DataSourceProvider.AddDataSource(new maskx.OData.Sql.SQLDataSource("db");
```
the "db" is database connection string key in web.config

### Configure database connection string in web.config
```CSharp
   <connectionStrings>
    <add name="db" connectionString="Data Source=.;Initial Catalog=<your database>;Integrated Security=True" />
  </connectionStrings>
```
# Usage
now you can access the database object through the web API. you can visit this page for basic OData knowledge [http://www.odata.org/getting-started/understand-odata-in-6-steps/](http://www.odata.org/getting-started/understand-odata-in-6-steps/)
## Table
### Query
```javascript
  $.get('odata/db/<table>').done(function (data) {alert(data.value) });
```

you can user 
[$filter](http://docs.oasis-open.org/odata/odata/v4.0/errata03/os/complete/part1-protocol/odata-v4.0-errata03-os-part1-protocol-complete.html#_The_$filter_System), 
[$orderby](http://docs.oasis-open.org/odata/odata/v4.0/errata03/os/complete/part1-protocol/odata-v4.0-errata03-os-part1-protocol-complete.html#_The_$orderby_System), 
[$top](http://docs.oasis-open.org/odata/odata/v4.0/errata03/os/complete/part1-protocol/odata-v4.0-errata03-os-part1-protocol-complete.html#_The_$top_System_1), 
[$skip](http://docs.oasis-open.org/odata/odata/v4.0/errata03/os/complete/part1-protocol/odata-v4.0-errata03-os-part1-protocol-complete.html#_The_$skip_System), 
[$count](http://docs.oasis-open.org/odata/odata/v4.0/errata03/os/complete/part1-protocol/odata-v4.0-errata03-os-part1-protocol-complete.html#_The_$inlinecount_System) query option restricts the set of items returned

### Insert

```javascript
  $.post('odata/db/<table>',{
    'col1':'col1 value',
    'col2':'col2 value',
    ...
  }).done(function (data) {alert(data.value) });
```
### Update
```javascript
  $.ajax({
    url:'odata/db/<table>(<ID>)',
    type:'PUT',
    data:{
      'col1':'col1 value',
      'col2':'col2 value',
      ...  
    }
  }).done(function (data) {alert(data)});
```
### Merge
``` javascript
$.ajax({
    url:'odata/db/<table>(<ID>)',
    type:'PATCH',
    data:{
      'col1':'col1 value',
      'col2':'col2 value',
      ...  
    }
  }).done(function (data) {alert(data)});
```
### Delete
```javascript
  $.ajax({
    url:'odata/db/<table>(<ID>)',
    type:'DELETE'
  }).done(function (data) {alert(data) });
```
##  View
  for view, only can query support 
```javascript
    $.get('odata/db/<view>').done(function (data) {alert(data.value) });
```
## Stored procedure
```javascript
 $.post('odata/db/<Stroed procedure name >()',{
    'par1':'par1 value',
    'par2':'par2 value',
    ...
  }).done(function (data) {alert(data) });
  
```

## Table-valued function
```javascript
   $.get('odata/db/<table-valued function name>()').done(function (data) {alert(data.value) });
```

## Security
SQLDataSource has a BeforeExcute property, you can judge user's permission in there

```csharp
DataSourceProvider.AddDataSource(new maskx.OData.Sql.SQLDataSource(<DataSourceName>)
 {
   BeforeExcute = (ri) =>{
      if (ri.QueryOptions != null && ri.QueryOptions.SelectExpand != null) {
     
      }
      Console.WriteLine("BeforeExcute:{0}", ri.Target);
   }
 });
```


## Audit
SQLDataSource has a BeforeExcute and AfterExcute properties, you can judge user's permission in there

## More
### SQL Server 2008
### Handling special characters in odata queries


| Special Character| Special Meaning                               | Hexadecimal Value|
|      :---:       | ---                                           |      :---:       |
| +                | Indicates a space(space cannot be used in url)| %28              |
| /                | Separates directories and subdirectories      | %2F              |
| ?                | Separates the actual URL and the Parameters   | %3F              |
| %                |Specifiers special characters                  | %25              |
|#                 |Indicates the bookmark                         | %23              |
|&                 |Spearator between parameters specified the URL | %26              |


# License
The MIT License (MIT) - See file 'LICENSE' in this project