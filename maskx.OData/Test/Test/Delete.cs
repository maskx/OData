using System.Net;
using Xunit;

namespace Test
{
    [Collection("WebHost collection")]
    [Trait("Category", "Delete")]
    public class Delete
    {
        [Fact]
        public void DeleteSuccess()
        {
            var rtv = Common.Post("Customers", new { CustomerID = "12345", CompanyName = "CompanyName", ContactName = "Name1" });
            Assert.Equal(HttpStatusCode.Created, rtv.Item1);
            Assert.EndsWith("$metadata#Edm.String", rtv.Item2.Property("@odata.context").Value.ToString());
            var rtv1 = Common.Delete("Customers('12345')");
            Assert.Equal(HttpStatusCode.NoContent, rtv1.Item1);
        }
        [Fact]
        public void DeleteNotExist()
        {
            var rtv = Common.Delete("Customers('1')");
            Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, rtv.Item1);
        }
    }
}
