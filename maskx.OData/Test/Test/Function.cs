using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Xunit;
using Test;

namespace Function
{
    [Collection("WebHost collection")]
    public class Function
    {
        [Fact]
        public void Success()
        {
            var rtv = Common.Get("ChildrenTags(ParentId=2)");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Collection(ns.ChildrenTags_RtvCollectionType)", rtv.Item2.Property("@odata.context").Value.ToString());
        }
    }
}
