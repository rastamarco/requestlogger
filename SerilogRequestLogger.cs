using Microsoft.AspNetCore.Http;
using Serilog;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace TaxSerilog
{
    public class SerilogRequestLogger
    {
        private readonly RequestDelegate _next;
        private readonly string _projectName;

        public SerilogRequestLogger(RequestDelegate next, string appName)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            _next = next;

            _projectName = appName;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));

            using (var responseBodyMemoryStream = new MemoryStream())
            {
                //Request Body
                HttpRequestRewindExtensions.EnableBuffering(httpContext.Request);
                Stream body = httpContext.Request.Body;
                byte[] buffer = new byte[Convert.ToInt32(httpContext.Request.ContentLength)];
                await httpContext.Request.Body.ReadAsync(buffer, 0, buffer.Length);
                //string requestBody = Encoding.UTF8.GetString(buffer);
                body.Seek(0, SeekOrigin.Begin);
                httpContext.Request.Body = body;

                //Response Body
                var originalResponseBodyReference = httpContext.Response.Body;
                httpContext.Response.Body = responseBodyMemoryStream;

                await _next(httpContext);

                httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
                //var responseBody = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
                //httpContext.Response.Body.Seek(0, SeekOrigin.Begin);

                HeadersLog headers = new HeadersLog();

                foreach (var item in httpContext.Request.Headers)
                {
                    if (item.Key == "Accept")
                        headers.Accept = item.Value;
                    if (item.Key == "Connection")
                        headers.Connection = item.Value;
                    if (item.Key == "User-Agent")
                        headers.User_agent = item.Value;
                    if (item.Key == "Accept-Encoding")
                        headers.Accept_encoding = item.Value;
                    if (item.Key == "Content-Length")
                        headers.Content_length = item.Value;
                    if (item.Key == "Content-Type")
                        headers.Content_type = item.Value;
                }

                var requestHeaders = new
                {
                    accept = headers.Accept,
                    connection = headers.Connection,
                    user_agent = headers.User_agent,
                    accept_encoding = headers.Accept_encoding,
                    content_length = headers.Content_length,
                    content_type = headers.Content_type
                };

                string responseInformation = "Response information: " +
                    "{requestid} " +
                    "{id} " +
                    "{date} " +
                    "{time_elapsed} " +
                    "{country} " +
                    "{app_code} " +
                    "{http_request_uri} " +
                    "{http_status} " +
                    "{http_request_method} " +
                    "{http_content_type}" +
                    "{client_ip} " +
                    "{trace_id} " +
                    "{span_id} " +
                    "{@request_headers} ";

                Log.Debug(responseInformation,
                          httpContext.Connection.Id,
                          httpContext.Connection.Id,
                          DateTime.UtcNow,
                          Activity.Current?.StartTimeUtc.Millisecond,
                          CultureInfo.CurrentCulture.Name,
                          _projectName,
                          httpContext.Request.Path,
                          httpContext.Response.StatusCode,
                          httpContext.Request.Method,
                          httpContext.Response.ContentType,
                          httpContext.Connection.RemoteIpAddress.ToString(),
                          httpContext.TraceIdentifier,
                          Activity.Current?.RootId,
                          requestHeaders);

                await responseBodyMemoryStream.CopyToAsync(originalResponseBodyReference);
            }
        }
    }
}
