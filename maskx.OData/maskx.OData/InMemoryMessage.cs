using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.OData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace maskx.OData
{
    public class InMemoryMessage : IODataRequestMessage, IODataResponseMessage, IContainerProvider, IDisposable
    {
        private readonly Dictionary<string, string> headers;
        public InMemoryMessage(HttpRequest request)
        {
            headers = request.Headers.ToDictionary((KeyValuePair<string, StringValues> kvp) => kvp.Key, (KeyValuePair<string, StringValues> kvp) => string.Join(";", kvp.Value));
            Stream = request.Body;
        }

        public IEnumerable<KeyValuePair<string, string>> Headers
        {
            get { return this.headers; }
        }

        public int StatusCode { get; set; }

        public Uri Url { get; set; }

        public string Method { get; set; }

        public Stream Stream { get; set; }

        public IServiceProvider Container { get; set; }

        public string GetHeader(string headerName)
        {
            return this.headers.TryGetValue(headerName, out string headerValue) ? headerValue : null;
        }

        public void SetHeader(string headerName, string headerValue)
        {
            headers[headerName] = headerValue;
        }

        public Stream GetStream()
        {
            return this.Stream;
        }

        public Action DisposeAction { get; set; }

        void IDisposable.Dispose()
        {
            if (this.DisposeAction != null)
            {
                DisposeAction();
            }
        }
    }

}
