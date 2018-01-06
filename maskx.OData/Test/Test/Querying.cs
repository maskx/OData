using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Net;
using Xunit;
using Test;

namespace Querying
{
    [Collection("WebHost collection")]
    public class Filter
    {
        [Fact]
        public void Endswith()
        {
            var rtv = Common.Get("AspNetUsers?$filter=(endswith(UserName,'min')) or (UserName eq null)&$top=1&$skip=1&$orderby=UserName desc");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.Equal(2, rtv.Item2.Count);
            Assert.EndsWith("$metadata#AspNetUsers", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        

        public void GetCount()
        {
            var rtv = Common.Get("Tag/$count");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag/$entity", rtv.Item2.Property("@odata.context").Value.ToString());

        }
    }

    [Collection("WebHost collection")]
    public class OrderBy
    {
        [Fact]
        public void Success()
        {

        }
    }
    [Collection("WebHost collection")]
    public class TopSkip
    {
        [Fact]
        public void Success()
        {

        }
    }
    [Collection("WebHost collection")]
    public class Count
    {
        [Fact]
        public void Success()
        {

        }
    }
    [Collection("WebHost collection")]
    public class Expand
    {
        [Fact]
        public void Success()
        {

        }
    }
    [Collection("WebHost collection")]
    public class Select
    {
        [Fact]
        public void Success()
        {

        }
    }
    [Collection("WebHost collection")]
    public class Search
    {
        [Fact]
        public void Success()
        {

        }
    }
}
