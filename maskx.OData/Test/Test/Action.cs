using System.Net;
using Xunit;

namespace Test
{
    [Collection("WebHost collection")]
    [Trait("Category", "Action")]
    public class Action
    {
        [Fact]
        public void NoParameterSuccess()
        {
            var rtv = Common.Post("Ten_Most_Expensive_Products", null);
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#ns.ActionResultSet", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void WithParameterSuccess()
        {
            var rtv = Common.Post("CreateCategory", new
            {
                Category =
                new[] {
                new { CategoryName = "ALFKI", Description="ww11" },
                new { CategoryName = "123",Description="ww11" } }
            });
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#ns.ActionResultSet", rtv.Item2.Property("@odata.context").Value.ToString());
            var d1 = Common.GetJObject("Categories?$filter=startswith(Description,  'ww11')");
            var b = d1.Item2;

        }
    }
}
