using Serilog.Context;
using System.Diagnostics;
using System.Text;

namespace OrderService.Api.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        // Paths to exclude from detailed logging (health checks, metrics, etc.)
        private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "/health",
            "/healthz",
            "/ready",
            "/metrics",
            "/favicon.ico"
        };

        // Headers that should never be logged (security sensitive)
        private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "Cookie",
            "Set-Cookie",
            "X-API-Key",
            "X-Auth-Token"
        };

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            // Skip logging for excluded paths
            if (ExcludedPaths.Contains(path))
            {
                await _next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var requestId = context.TraceIdentifier;

            // Capture original response body stream
            var originalBodyStream = context.Response.Body;

            try
            {
                // Log request
                await LogRequestAsync(context, requestId);

                // Capture response for logging (optional, be careful with large responses)
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                // Call the next middleware
                await _next(context);

                stopwatch.Stop();

                responseBody.Seek(0, SeekOrigin.Begin);
                // Log response
                await LogResponseAsync(context, requestId, stopwatch.ElapsedMilliseconds, responseBody);
                responseBody.Seek(0, SeekOrigin.Begin);
                // Copy response back to original stream
                await responseBody.CopyToAsync(originalBodyStream);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Log the exception with full context
                LogException(context, requestId, stopwatch.ElapsedMilliseconds, ex);

                throw; // Re-throw to let exception handling middleware handle it
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }

        private async Task LogRequestAsync(HttpContext context, string requestId)
        {
            var request = context.Request;

            // Get safe headers (excluding sensitive ones)
            var safeHeaders = GetSafeHeaders(request.Headers);

            // Read request body if needed (be careful with large bodies)
            string requestBody = string.Empty;
            if (ShouldLogRequestBody(request))
            {
                requestBody = await ReadRequestBodyAsync(request);
            }

            using (LogContext.PushProperty("RequestId", requestId))
            using (LogContext.PushProperty("Method", request.Method))
            using (LogContext.PushProperty("Path", request.Path))
            using (LogContext.PushProperty("QueryString", request.QueryString.ToString()))
            using (LogContext.PushProperty("ClientIP", GetClientIpAddress(context)))
            using (LogContext.PushProperty("UserAgent", request.Headers["User-Agent"].ToString()))
            {
                _logger.LogInformation(
                    "HTTP {Method} {Path}{QueryString} started from {ClientIP}",
                    request.Method,
                    request.Path,
                    request.QueryString,
                    GetClientIpAddress(context)
                );

                if (!string.IsNullOrEmpty(requestBody))
                {
                    _logger.LogDebug("Request Body: {RequestBody}", requestBody);
                }
            }
        }

        private async Task LogResponseAsync(HttpContext context, string requestId, long elapsedMilliseconds, MemoryStream responseBody)
        {
            var response = context.Response;

            // Read response body if needed
            string responseBodyText = string.Empty;
            if (ShouldLogResponseBody(response))
            {
                responseBody.Seek(0, SeekOrigin.Begin);
                responseBodyText = await new StreamReader(responseBody).ReadToEndAsync();
                responseBody.Seek(0, SeekOrigin.Begin);
            }

            var logLevel = GetLogLevelForStatusCode(response.StatusCode);

            using (LogContext.PushProperty("RequestId", requestId))
            using (LogContext.PushProperty("StatusCode", response.StatusCode))
            using (LogContext.PushProperty("ElapsedMilliseconds", elapsedMilliseconds))
            using (LogContext.PushProperty("ResponseSize", responseBody.Length))
            {
                _logger.Log(
                    logLevel,
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds}ms",
                    context.Request.Method,
                    context.Request.Path,
                    response.StatusCode,
                    elapsedMilliseconds
                );

                if (!string.IsNullOrEmpty(responseBodyText) && response.StatusCode >= 400)
                {
                    _logger.LogDebug("Response Body: {ResponseBody}", responseBodyText);
                }
            }

            // Log warning for slow requests
            if (elapsedMilliseconds > 3000) // 3 seconds threshold
            {
                _logger.LogWarning(
                    "Slow request detected: {Method} {Path} took {ElapsedMilliseconds}ms",
                    context.Request.Method,
                    context.Request.Path,
                    elapsedMilliseconds
                );
            }
        }

        private void LogException(HttpContext context, string requestId, long elapsedMilliseconds, Exception ex)
        {
            using (LogContext.PushProperty("RequestId", requestId))
            using (LogContext.PushProperty("ElapsedMilliseconds", elapsedMilliseconds))
            using (LogContext.PushProperty("ExceptionType", ex.GetType().Name))
            {
                _logger.LogError(
                    ex,
                    "HTTP {Method} {Path} failed after {ElapsedMilliseconds}ms with {ExceptionType}",
                    context.Request.Method,
                    context.Request.Path,
                    elapsedMilliseconds,
                    ex.GetType().Name
                );
            }
        }

        private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
        {
            request.EnableBuffering(); // Allow multiple reads

            using var reader = new StreamReader(
                request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0; // Reset position for next middleware

            // Truncate if too large
            return body.Length > 4096 ? body.Substring(0, 4096) + "... (truncated)" : body;
        }

        private static bool ShouldLogRequestBody(HttpRequest request)
        {
            // Only log body for specific content types and reasonable sizes
            if (request.ContentLength > 10 * 1024) // 10KB limit
                return false;

            var contentType = request.ContentType?.ToLowerInvariant() ?? string.Empty;
            return contentType.Contains("application/json") ||
                   contentType.Contains("application/xml") ||
                   contentType.Contains("text/");
        }

        private static bool ShouldLogResponseBody(HttpResponse response)
        {
            // Only log error responses or specific content types
            if (response.ContentLength > 10 * 1024) // 10KB limit
                return false;

            var contentType = response.ContentType?.ToLowerInvariant() ?? string.Empty;
            return (response.StatusCode >= 400 &&
                   (contentType.Contains("application/json") ||
                    contentType.Contains("text/")));
        }

        private static Dictionary<string, string> GetSafeHeaders(IHeaderDictionary headers)
        {
            return headers
                .Where(h => !SensitiveHeaders.Contains(h.Key))
                .ToDictionary(h => h.Key, h => h.Value.ToString());
        }

        private static string GetClientIpAddress(HttpContext context)
        {
            // Check for forwarded headers (when behind proxy/load balancer)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private static LogLevel GetLogLevelForStatusCode(int statusCode)
        {
            return statusCode switch
            {
                >= 500 => LogLevel.Error,
                >= 400 => LogLevel.Warning,
                _ => LogLevel.Information
            };
        }
    }

    // Extension method for easy registration
    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}
