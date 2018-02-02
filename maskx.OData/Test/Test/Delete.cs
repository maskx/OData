using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
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
            var rtv = Common.Delete("Tag(1)");
            Assert.Equal(HttpStatusCode.NoContent, rtv.Item1);
        }
        [Fact]
        public void DeleteNotExist()
        {
            var rtv = Common.Delete("Tag(19)");
            Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, rtv.Item1);
        }
    }
}
