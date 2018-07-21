# maskx.OData

## Note

>This dependence on Microsoft.AspNetCore.OData, more infomation about Microsoft.AspNetCore.OData please folw those links:

* <https://github.com/OData/WebApi/tree/feature/netcore>
* <https://github.com/OData/WebApi/issues/939>

## Introduction

maskx.OData support build a Odata database proxy turn your database into a OData WebApi server.
Dynamic generate OData Controller from database.

With the web server build by maskx.odata, the client can do:

* Query, insert, delete, update the database table
* Query the view and table-valued function
* Invoke the stored procedure

The Web API is followed the OData protocol (<http://www.odata.org/)>

## Why this library

## Quickstart

### Setup WebApi

* **Create a asp.net core Web API project**

* **Install nuget package** from <https://www.nuget.org/packages/maskx.OData/>

* **Configure the controller**

```CSharp
  class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOData();
            services.AddMvc();
        }
        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc(routeBuilder => {
                routeBuilder.MapDynamicODataServiceRoute("odata","db1",
                    new maskx.OData.Sql.SQL2012("odata", "Data Source=.;Initial Catalog=Group;Integrated Security=True"));
            });
        }
    }
```

### Run or Deploy

* Hit F5 to start the web server or deploy the project

### Access the database by Web Api

Now you can access the database through the web API. you can visit this page for basic OData knowledge: <http://www.odata.org/getting-started/understand-odata-in-6-steps/>

## Usage
### Note

>OData is case-sensitive, if you want case-insensitive, see Configuration

### Requesting Entity Collections

* Query Table

```javascript
  $.get('db1/<table name>')
  .done(function (data) {alert(data.value) });
```

* Query View

```javascript
 $.get('db1/<view name>')
 .done(function (data) {alert(data.value) });
 ```

* Query Table-valued function

 ```javascript
  $.get('db1/<Table-valued function name>()')
  .done(function (data) {alert(data.value) });
```

### Requesting an Individual Entity by ID

```javascript
  $.get('db1/<table name>(<the value of ID>)')
  .done(function (data) {alert(data) });
```

### Requesting an Individual Property

not support yet

### Querying

you can user:

* [$filter](http://docs.oasis-open.org/odata/odata/v4.0/errata03/os/complete/part1-protocol/odata-v4.0-errata03-os-part1-protocol-complete.html#_The_$filter_System)
* [$orderby](http://docs.oasis-open.org/odata/odata/v4.0/errata03/os/complete/part1-protocol/odata-v4.0-errata03-os-part1-protocol-complete.html#_The_$orderby_System)
* [$top](http://docs.oasis-open.org/odata/odata/v4.0/errata03/os/complete/part1-protocol/odata-v4.0-errata03-os-part1-protocol-complete.html#_The_$top_System_1)
* [$skip](http://docs.oasis-open.org/odata/odata/v4.0/errata03/os/complete/part1-protocol/odata-v4.0-errata03-os-part1-protocol-complete.html#_The_$skip_System)
* [$count](http://docs.oasis-open.org/odata/odata/v4.0/errata03/os/complete/part1-protocol/odata-v4.0-errata03-os-part1-protocol-complete.html#_The_$inlinecount_System)
* [$expand]()
* [$select]()
* [$search]()

### Data Modification

* **Create an Entity**

```javascript
  $.post('db1/<table>',{
    'col1':'col1 value',
    'col2':'col2 value',
    ...
  }).done(function (data) {alert(data.value) });
```

* **Update an Entity**

```javascript
  $.ajax({
    url:'db1/<table>(<ID>)',
    type:'PUT',
    data:{
      'col1':'col1 value',
      'col2':'col2 value',
      ...
    }
  }).done(function (data) {alert(data)});
```

* **Merge an Entity**

``` javascript
$.ajax({
    url:'db1/<table>(<ID>)',
    type:'PATCH',
    data:{
      'col1':'col1 value',
      'col2':'col2 value',
      ...
    }
  }).done(function (data) {alert(data)});
```

* **Delete an Entity**

```javascript
  $.ajax({
    url:'db1/<table>(<ID>)',
    type:'DELETE'
  }).done(function (data) {alert(data) });
```

### View

  for view, only query is supported

```javascript
$.get('db1/<view>').done(function (data) {alert(data.value) });
```

### Stored procedure

```javascript
 $.post('db1/<Stroed procedure name >()',{
    'par1':'par1 value',
    'par2':'par2 value',
    ...
  }).done(function (data) {alert(data) });

```

## Table-valued function

```javascript
$.get('db1/<table-valued function name>()')
.done(function (data) {alert(data.value) });
```

* **Parameter**

```javascript
   $.get('db1/<table-valued function name>(ParameterName1=arameterValue1，ParameterName2=ParameterValue2)')
   .done(function (data) {alert(data.value) });
```

* **Querying**

 you can query a table-valued function as a table

```javascript
$.get('db1/<table-valued function name>()')
.done(function (data) {alert(data.value) });
```

## Schema

the default schema of sql server is **dbo**, so you can query the table by name directly when the table's shcema is **dbo**

when the schema of a table is not **dbo**, you must query the table with schema name

```javascript
$.get('db1/<schema name>.<table name>')
.done(function (data) {alert(data.value) });
```

* **Customer default schema**

If you want make another schema( not dbo) as your default schema for the query url convenient, you can change it

```csharp
public void Configure(IApplicationBuilder app)
{
  app.UseMvc(routeBuilder =>
  {
     var dataSource = new maskx.OData.Sql.SQL2012("odata", "Data Source=.;Initial Catalog=Group;Integrated Security=True");
     dataSource.Configuration.DefaultSchema = "schemaB";
     routeBuilder.MapDynamicODataServiceRoute("odata1", "db1", dataSource);
  });
}
```

## Security

SQLDataSource has a BeforeExcute property, you can judge user's permission in there

```csharp
new maskx.OData.Sql.SQL2012(<DataSourceName>)
{
   BeforeExcute = (ri) =>{
      if (ri.QueryOptions != null && ri.QueryOptions.SelectExpand != null) {

      }
      Console.WriteLine("BeforeExcute:{0}", ri.Target);
   }
 };
```

## Audit

SQLDataSource has a BeforeExcute and AfterExcute properties, you can judge user's permission in there

## SQL Server 2008

## Handling special characters in odata queries

| Special Character| Special Meaning                               | Hexadecimal Value|
|      :---:       | ---                                           |      :---:       |
| +                | Indicates a space(space cannot be used in url)| %28              |
| /                | Separates directories and subdirectories      | %2F              |
| ?                | Separates the actual URL and the Parameters   | %3F              |
| %                |Specifiers special characters                  | %25              |
|#                 |Indicates the bookmark                         | %23              |
|&                 |Spearator between parameters specified the URL | %26              |

## Configuration

* **DefaultSchema** :Defalut Schema name, default is **dbo**
* **LowerName**: make the name of database object to lower, default is false

## Contributing

Contributions are absolutely welcome!

1. Fork it!
2. Create your feature branch: git checkout -b my-new-feature
3. Commit your changes: git commit -am 'Add some feature'
4. Push to the branch: git push origin my-new-feature
Submit a pull request :D

## License

The MIT License (MIT) - See file 'LICENSE' in this project
