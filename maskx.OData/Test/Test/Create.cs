using System.Net;
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
            var rtv = Common.Post("Customers", new { CustomerID = "abcde", CompanyName = "CompanyName", ContactName = "Name1" });
            Assert.Equal(HttpStatusCode.Created, rtv.Item1);
            Assert.EndsWith("$metadata#Edm.String", rtv.Item2.Property("@odata.context").Value.ToString());
            var rtv1 = Common.Delete("Customers('abcde')");
            Assert.Equal(HttpStatusCode.NoContent, rtv1.Item1);
        }
    }
}
