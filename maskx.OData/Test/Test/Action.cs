using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
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
            var rtv = Common.Post("GetEdmModelInfo()", null);
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.Equal(2, rtv.Item2.Count);
            Assert.EndsWith("$metadata#dbo.GetEdmModelInfo_RtvType", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void WithParameterSuccess()
        {
            var rtv = Common.Post("GetEdmSPResultSet()", new { Name = "GetEdmModelInfo" });
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.Equal(2, rtv.Item2.Count);
            Assert.EndsWith("$metadata#dbo.GetEdmSPResultSet_RtvType", rtv.Item2.Property("@odata.context").Value.ToString());
        }
    }
}
