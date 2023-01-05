using System.Net;
using Xunit;

namespace Test
{
    [Collection("WebHost collection")]
    [Trait("Category", "Querying")]
    public class Filter
    {
        [Fact]
        public void Endswith()
        {
            //endswith(Name,'Name2') or 
            var rtv = Common.GetJObject("Tag?$filter=0 eq ParentId&$top=1&$skip=1&$orderby=Name desc");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.Equal(2, rtv.Item2.Count);
            Assert.EndsWith("$metadata#Tag", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void Concat()
        {
            var rtv = Common.GetJObject("AspNetUsers?$filter=concat('Ad','min') eq UserName");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.Equal(2, rtv.Item2.Count);
            Assert.EndsWith("$metadata#AspNetUsers", rtv.Item2.Property("@odata.context").Value.ToString());

        }
        [Fact]
        public void Substring()
        {
            var rtv = Common.GetJObject("AspNetUsers?$filter=substring('Admin',0,6) eq UserName");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.Equal(2, rtv.Item2.Count);
            Assert.EndsWith("$metadata#AspNetUsers", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void Tolower()
        {
            var rtv = Common.GetJObject("AspNetUsers?$filter=tolower('Admin') eq UserName");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.Equal(2, rtv.Item2.Count);
            Assert.EndsWith("$metadata#AspNetUsers", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void Indexof()
        {
            var rtv = Common.GetJObject("AspNetUsers?$filter=indexof(UserName,'A') eq 1");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.Equal(2, rtv.Item2.Count);
            Assert.EndsWith("$metadata#AspNetUsers", rtv.Item2.Property("@odata.context").Value.ToString());
        }
    }

    [Collection("WebHost collection")]
    public class OrderBy
    {
        [Fact]
        public void Success()
        {
            var rtv = Common.GetJObject("Tag?$orderby=Name");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag", rtv.Item2.Property("@odata.context").Value.ToString());
        }
    }
    [Collection("WebHost collection")]
    public class TopSkip
    {
        [Fact]
        public void Success()
        {
            var rtv = Common.GetJObject("Tag?$top=5&$skip=3");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag", rtv.Item2.Property("@odata.context").Value.ToString());

        }
    }
    [Collection("WebHost collection")]
    public class Count
    {
        [Fact]
        public void Success()
        {
            var rtv = Common.GetContent("Categories/$count");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.True(int.TryParse(rtv.Item2, out int count));
        }
    }
    [Collection("WebHost collection")]
    public class Expand
    {
        [Fact]
        public void SimplyExpand()
        {
            var rtv = Common.GetJObject("Orders?$expand=Order_Details");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.Equal(2, rtv.Item2.Count);
            Assert.EndsWith("$metadata#Orders", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void NestExpand()
        {
            //TODO: Not implement
            var rtv = Common.GetJObject("AspNetUsers?$expand=AspNetUserRoles($expand=AspNetRoles)");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.Equal(2, rtv.Item2.Count);
            Assert.EndsWith("$metadata#AspNetUsers", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void MultiExpand()
        {
            var rtv = Common.GetJObject("AspNetUsers?$expand=AspNetUserRoles,AspNetUserClaims");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.Equal(2, rtv.Item2.Count);
            Assert.EndsWith("$metadata#AspNetUsers", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void ExpandSelect()
        {
            //TODO:Not implement
            var rtv = Common.GetJObject("AspNetUsers?$expand=AspNetUserRoles&$select=AspNetUserRoles/RoleId");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.Equal(2, rtv.Item2.Count);
            Assert.EndsWith("$metadata#AspNetUsers", rtv.Item2.Property("@odata.context").Value.ToString());

        }
    }
    [Collection("WebHost collection")]
    public class Select
    {
        [Fact]
        public void Success()
        {
            var rtv = Common.GetJObject("Tag?$select=ParentId,Name");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag(ParentId,Name)", rtv.Item2.Property("@odata.context").Value.ToString());

        }
    }
}
