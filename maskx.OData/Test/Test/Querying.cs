using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System;
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
            var rtv = Common.GetJObject("AspNetUsers?$filter=(endswith(UserName,'min')) or (UserName eq null)&$top=1&$skip=1&$orderby=UserName desc");
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
            var rtv = Common.GetContent("Tag/$count");
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
            var rtv = Common.GetJObject("AspNetUsers?$expand=AspNetUserRoles");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.Equal(2, rtv.Item2.Count);
            Assert.EndsWith("$metadata#AspNetUsers", rtv.Item2.Property("@odata.context").Value.ToString());
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
