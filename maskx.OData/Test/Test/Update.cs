using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Xunit;
using Test;

namespace Update
{
    [Collection("WebHost collection")]
    public class Update
    {
        [Fact]
        public void Success()
        {
            var rtv = Common.Put("Tag(3)", new { ParentId = 2, hasChild = false, Name = "Name1", Description = "Des1 Updated" });
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Edm.Int32", rtv.Item2.Property("@odata.context").Value.ToString());
        }
    }
    [Collection("WebHost collection")]
    public class Patch
    {
        [Fact]
        public void Success()
        {
            var rtv = Common.Patch("Tag(2)", new { Name = "Name patched" });
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Edm.Int32", rtv.Item2.Property("@odata.context").Value.ToString());
        }
    }
}
