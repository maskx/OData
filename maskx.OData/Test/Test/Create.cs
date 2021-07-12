using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Xunit;

namespace Test
{
    [Collection("WebHost collection")]
    [Trait("Category", "Create")]
    public class Create
    {
        [Fact]
        public void Success()
        {
            var rtv = Common.Post("Customers", new { CustomerID = "12345", CompanyName = "CompanyName", ContactName = "Name1" });
            Assert.Equal(HttpStatusCode.Created, rtv.Item1);
            Assert.EndsWith("$metadata#Edm.String", rtv.Item2.Property("@odata.context").Value.ToString());
        }
    }
}
