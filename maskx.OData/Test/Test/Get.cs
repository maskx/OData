using System.Net;
using Xunit;

namespace Test
{
    [Collection("WebHost collection")]
    [Trait("Category", "Get")]
    public class Get
    {
        [Fact]
        public void GetSuccess()
        {
            var rtv = Common.GetJObject("Categories");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Categories", rtv.Item2.Property("@odata.context").Value.ToString());

        }
        [Fact]
        public void GetWithSchemaSuccess()
        {
            var rtv = Common.GetJObject("dbo.Orders");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#dbo.Orders", rtv.Item2.Property("@odata.context").Value.ToString());

        }
        [Fact]
        public void GetByKeySuccess()
        {
            var rtv = Common.GetJObject("Employees(2)");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Orders/$entity", rtv.Item2.Property("@odata.context").Value.ToString());

        }
        [Fact]
        public void GetByKeyWithExpandSuccess()
        {
            // todo: table name may contain space, need support
            var rtv = Common.GetJObject("Orders(10248)?$expand=Customers");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("/$metadata#Orders(Customers())/$entity", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void GetByKeyWithMulitExpandSuccess()
        {
            // todo: table name may contain space, need support
            var rtv = Common.GetJObject("Orders(10248)?$expand=Customers,Employees");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("/$metadata#Orders(Customers())/$entity", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void GetPropertySuccess()
        {
            //TODO: not support now
            //~/entityset/key/property/
            var rtv = Common.GetJObject("Orders(10248)/ShipName");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag/$entity", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void GetPropertyRawValueSuccess()
        {
            //TODO: not support now
            //~/entityset/key/property/$value
            var rtv = Common.GetJObject("Orders(10248)/ShipName/$value");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag/$entity", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void QueryServiceDocument()
        {
            var rtv = Common.GetJObject(string.Empty);
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void QueryMetadata()
        {
            var rtv = Common.GetContent("$metadata");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.StartsWith("<", rtv.Item2);
            Assert.EndsWith(">", rtv.Item2);
        }
    }
}
