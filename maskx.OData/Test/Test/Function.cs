using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Xunit;

namespace Test
{
    [Collection("WebHost collection")]
    [Trait("Category", "Delete")]
    public class Function
    {
        [Fact]
        public void Success()
        {
            var rtv = Common.GetJObject("ChildrenTags(ParentId=2)");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Collection(dbo.ChildrenTags_RtvCollectionType)", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void FilterSuccess()
        {
            var rtv = Common.GetJObject("ChildrenTags(ParentId=2)?$top=3");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Collection(dbo.ChildrenTags_RtvCollectionType)", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void CountSuccess()
        {
            var rtv = Common.GetContent("ChildrenTags(ParentId=2)/$count");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.True(int.TryParse(rtv.Item2, out int count));
        }
    }
}
