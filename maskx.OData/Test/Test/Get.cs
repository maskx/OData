using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Test;
using System.Net;

namespace Get
{
    [Collection("WebHost collection")]
    public class Get
    {
        [Fact]
        public void Success()
        {
            var rtv = Common.Get("Tag");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag", rtv.Item2.Property("@odata.context").Value.ToString());

        }
    }
    [Collection("WebHost collection")]
    public class GetByKey
    {
        [Fact]
        public void Success()
        {
            var rtv = Common.Get("Tag(1)");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag/$entity", rtv.Item2.Property("@odata.context").Value.ToString());

        }
    }
    [Collection("WebHost collection")]
    public class GetProperty
    {
        [Fact]
        public void Success()
        {
            //TODO: not support now
            //~/entityset/key/property/
            var rtv = Common.Get("Tag(1)/Name");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag/$entity", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void SuccessRawValue()
        {
            //TODO: not support now
            //~/entityset/key/property/$value
            var rtv = Common.Get("Tag(1)/Name/$value");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag/$entity", rtv.Item2.Property("@odata.context").Value.ToString());
        }
    }
}
