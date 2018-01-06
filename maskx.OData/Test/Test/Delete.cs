using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Xunit;
using Test;

namespace Delete
{
    [Collection("WebHost collection")]
    public class Delete
    {
        [Fact]
        public void Success()
        {
            var rtv = Common.Delete("Tag(1)");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Edm.Int32", rtv.Item2.Property("@odata.context").Value.ToString());
        }
    }
}
