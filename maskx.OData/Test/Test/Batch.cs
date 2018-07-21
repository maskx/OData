using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Test;
using Xunit;
using System.Linq;
using System.Net.Http.Formatting;

namespace Test
{
    [Collection("WebHost collection")]
    [Trait("Category", "Batch")]
    public class Batch
    {
        [Fact]
        
        public void Success()
        {
            //TODO: Batch not support now
            HttpClient client = new HttpClient();
            HttpRequestMessage batchRequest = new HttpRequestMessage(HttpMethod.Post, string.Format(Common.Tpl, "$batch"));
            AddRequest(batchRequest, HttpMethod.Get, string.Format(Common.Tpl, "Tag"));
            AddRequest(batchRequest, HttpMethod.Get, string.Format(Common.Tpl, "AspNetUsers"));
            var batchResponse = client.SendAsync(batchRequest).Result;
            MultipartStreamProvider streamProvider = batchResponse.Content.ReadAsMultipartAsync().Result;
            foreach (var content in streamProvider.Contents)
            {
                HttpResponseMessage response = content.ReadAsHttpResponseMessageAsync().Result;
                if (response.IsSuccessStatusCode)
                {
                    var b = response.Content.ToString();
                }
            }

        }
        static void AddRequest(HttpRequestMessage batchRequest, HttpMethod httpMethod, string url, object content = null)
        {
            if (batchRequest.Content == null)
            {
                string b = Guid.NewGuid().ToString();
                batchRequest.Content = new MultipartContent("mixed", "batch_" + b);
            }

            var multipartContent = batchRequest.Content as MultipartContent;
            int index = multipartContent.Count();

            HttpRequestMessage request = new HttpRequestMessage(httpMethod, url);
            if (content != null)
                request.Content = new ObjectContent(content.GetType(),content, new JsonMediaTypeFormatter());
            HttpMessageContent message = new HttpMessageContent(request);
            if (message.Headers.Contains("Content-Type"))
                message.Headers.Remove("Content-Type");
            message.Headers.Add("Content-Type", "application/http");
            message.Headers.Add("client-request-id", index.ToString());
            message.Headers.Add("return-client-request-id", "True");
            message.Headers.Add("Content-ID", index.ToString());
            message.Headers.Add("DataServiceVersion", "3.0");
            message.Headers.Add("Content-Transfer-Encoding", "binary");

            multipartContent.Add(message);
        }
    }
}
