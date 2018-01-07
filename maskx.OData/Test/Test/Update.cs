using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Xunit;

namespace Test
{
    [Collection("WebHost collection")]
    [Trait("Category", "Update")]
    public class Update
    {
        [Fact]
        [Trait("Category", "Update")]
        public void UpdateSuccess()
        {
            var rtv = Common.Put("Tag(3)", new { ParentId = 2, hasChild = false, Name = "Name1", Description = "Des1 Updated" });
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Edm.Int32", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        [Trait("Category", "Patch")]
        public void PatchSuccess()
        {
            var rtv = Common.Patch("Tag(2)", new { Name = "Name patched" });
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Edm.Int32", rtv.Item2.Property("@odata.context").Value.ToString());
        }
    }
}
