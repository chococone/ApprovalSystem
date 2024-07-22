using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Graph;

namespace ApprovalSystem.Api;

[Route("api/[controller]")]
public class ProxyController : ControllerBase
{
    private readonly GraphServiceClient _graphServiceClient;
    public ProxyController(GraphServiceClient graphServiceClient)
    {
        _graphServiceClient = graphServiceClient;
    }

    [HttpGet]
    [Route("{*all}")]
    public async Task<IActionResult> GetAsync([FromRoute] string all)
    {
        return await ProcessRequestAsync(Microsoft.Graph.HttpMethods.GET, all, null).ConfigureAwait(false);
    }

    [HttpPost]
    [Route("{*all}")]
    public async Task<IActionResult> PostAsync([FromRoute] string all, [FromBody] object body)
    {
        return await ProcessRequestAsync(Microsoft.Graph.HttpMethods.POST, all, body).ConfigureAwait(false);
    }

    [HttpDelete]
    [Route("{*all}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] string all)
    {
        return await ProcessRequestAsync(Microsoft.Graph.HttpMethods.DELETE, all, null).ConfigureAwait(false);
    }

    [HttpPut]
    [Route("{*all}")]
    public async Task<IActionResult> PutAsync([FromRoute] string all, [FromBody] object body)
    {
        return await ProcessRequestAsync(Microsoft.Graph.HttpMethods.PUT, all, body).ConfigureAwait(false);
    }

    [HttpPatch]
    [Route("{*all}")]
    public async Task<IActionResult> PatchAsync([FromRoute] string all, [FromBody] object body)
    {
        return await ProcessRequestAsync(Microsoft.Graph.HttpMethods.PATCH, all, body).ConfigureAwait(false);
    }

    private async Task<IActionResult> ProcessRequestAsync(Microsoft.Graph.HttpMethods method, string all, object? content)
    {
        var qs = HttpContext.Request.QueryString;
        var url = $"{GetBaseUrlWithoutVersion(_graphServiceClient)}/{all}{qs.ToUriComponent()}";
        var request = new BaseRequest(url, _graphServiceClient, null)
        {
            Method = method,
            ContentType = HttpContext.Request.ContentType
        };

        var neededHeaders = Request.Headers.Where(h => h.Key.ToLower() == "if-match" || h.Key.ToLower() == "consistencylevel").ToList();
        if (neededHeaders.Count != 0)
        {
            foreach (var header in neededHeaders)
            {
                request.Headers.Add(new HeaderOption(header.Key, string.Join(",", header.Value.ToString())));
            }
        }

        var contentType = "application/json";

        try
        {
            using (var response = await request.SendRequestAsync(content?.ToString(), CancellationToken.None, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false))
            {
                response.Content.Headers.TryGetValues("content-type", out var contentTypes);

                contentType = contentTypes?.FirstOrDefault() ?? contentType;

                var byteArrayContent = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return new HttpResponseMessageResult(ReturnHttpResponseMessage(HttpStatusCode.OK, contentType, new ByteArrayContent(byteArrayContent)));
            }
        }
        catch (ServiceException ex)
        {
            return new HttpResponseMessageResult(ReturnHttpResponseMessage(ex.StatusCode, contentType, new StringContent(ex.Error.ToString())));
        }

    }

    private static HttpResponseMessage ReturnHttpResponseMessage(HttpStatusCode httpStatusCode, string contentType, HttpContent httpContent)
    {
        var httpResponseMessage = new HttpResponseMessage(httpStatusCode)
        {
            Content = httpContent
        };

        try
        {
            httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }
        catch
        {
            httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return httpResponseMessage;
    }

    private static string GetBaseUrlWithoutVersion(GraphServiceClient graphServiceClient)
    {
        var baseUrl = graphServiceClient.BaseUrl;
        var index = baseUrl.LastIndexOf('/');
        return baseUrl.Substring(0, index);
    }

    public class HttpResponseMessageResult : IActionResult
    {
        private readonly HttpResponseMessage _httpResponseMessage;
        public HttpResponseMessageResult(HttpResponseMessage httpResponseMessage)
        {
            _httpResponseMessage = httpResponseMessage;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.StatusCode = (int)_httpResponseMessage.StatusCode;

            foreach (var header in _httpResponseMessage.Headers)
            {
                context.HttpContext.Response.Headers.TryAdd(header.Key, new StringValues(header.Value.ToArray()));
            }

            context.HttpContext.Response.ContentType = _httpResponseMessage.Content.Headers.ContentType!.ToString();

            using var stream = await _httpResponseMessage.Content.ReadAsStreamAsync();
            await stream.CopyToAsync(context.HttpContext.Response.Body);
            await context.HttpContext.Response.Body.FlushAsync();
        }
    }
}