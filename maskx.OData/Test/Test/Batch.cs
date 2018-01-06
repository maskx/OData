using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Test;
using Xunit;
using System.Linq;

namespace Batch
{
    [Collection("WebHost collection")]
    public class Batch
    {
        [Fact]
        public void Success()
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage batchRequest = new HttpRequestMessage(HttpMethod.Post, string.Format(Common.tpl, "$batch"));
            AddRequest(batchRequest, HttpMethod.Get, string.Format(Common.tpl, "Tag"));
            AddRequest(batchRequest, HttpMethod.Get, string.Format(Common.tpl, "AspNetUsers"));
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
        void AddRequest(HttpRequestMessage batchRequest, HttpMethod httpMethod, string url, object content = null)
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
                request.Content = new JsonContent(content);
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
