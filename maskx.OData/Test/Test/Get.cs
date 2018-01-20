using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System.Net;
using System.Net.Http;

namespace Test
{
    [Collection("WebHost collection")]
    [Trait("Category", "Get")]
    public class Get
    {
        [Fact]
        public void GetSuccess()
        {
            var rtv = Common.GetJObject("Tag");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag", rtv.Item2.Property("@odata.context").Value.ToString());

        }
        [Fact]
        public void GetWithSchemaSuccess()
        {
            var rtv = Common.GetJObject("schemaB.Group");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#schemaB.Group", rtv.Item2.Property("@odata.context").Value.ToString());

        }
        [Fact]
        public void GetByKeySuccess()
        {
            var rtv = Common.GetJObject("Tag(1)");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag/$entity", rtv.Item2.Property("@odata.context").Value.ToString());

        }
        [Fact]
        public void GetByKeyWithExpandSuccess()
        {
            var rtv = Common.GetJObject("AspNetUsers('3ceb1059-9953-4f77-bdc6-357db132500c')?$expand=AspNetUserRoles");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#AspNetUsers/$entity", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void GetPropertySuccess()
        {
            //TODO: not support now
            //~/entityset/key/property/
            var rtv = Common.GetJObject("Tag(1)/Name");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag/$entity", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void GetPropertyRawValueSuccess()
        {
            //TODO: not support now
            //~/entityset/key/property/$value
            var rtv = Common.GetJObject("Tag(1)/Name/$value");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata#Tag/$entity", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void QueryServiceDocument()
        {
            var rtv = Common.GetJObject(string.Empty);
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.EndsWith("$metadata", rtv.Item2.Property("@odata.context").Value.ToString());
        }
        [Fact]
        public void QueryMetadata()
        {
            var rtv = Common.GetContent("$metadata");
            Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            Assert.StartsWith("<", rtv.Item2);
            Assert.EndsWith(">", rtv.Item2);
        }
    }
}
