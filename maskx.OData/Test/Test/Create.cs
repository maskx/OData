using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Xunit;
using Test;

namespace Create
{
    [Collection("WebHost collection")]
    public class Create
    {
        [Fact]
        public void Success()
        {
            var rtv = Common.Post("Tag", new { ParentId = 0, hasChild = false, Name = "Name1", Description = "Des1" });

            Assert.Equal(HttpStatusCode.Created, rtv.Item1);
            Assert.EndsWith("$metadata#Edm.String", rtv.Item2.Property("@odata.context").Value.ToString());
        }
    }
}
