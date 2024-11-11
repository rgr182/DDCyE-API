using DDEyC_Assistant.Attributes;
using DDEyC_Assistant.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Polly;
using System.Net;
using Xunit;

namespace DDEyC_Assistant.Tests.Attributes
{
    public class RequireAuthAttributeTests
    {
        private readonly Mock<ILogger<RequireAuthAttribute>> _mockLogger;
        private readonly Mock<IAuthenticationPolicy> _mockAuthPolicy;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly IConfiguration _configuration;
        private readonly RequireAuthAttribute _attribute;
        private readonly HttpClient _httpClient;

        public RequireAuthAttributeTests()
        {
            _mockLogger = new Mock<ILogger<RequireAuthAttribute>>();
            _mockAuthPolicy = new Mock<IAuthenticationPolicy>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

            var configurationBuilder = new ConfigurationBuilder();
            var myConfiguration = new Dictionary<string, string>
            {
                {"AppSettings:BackendUrl", "http://test-api.com"}
            };

            configurationBuilder.AddInMemoryCollection(myConfiguration);
            _configuration = configurationBuilder.Build();

            _mockHttpClientFactory
                .Setup(f => f.CreateClient("AuthClient"))
                .Returns(_httpClient);

            _attribute = new RequireAuthAttribute();
        }

        private ActionExecutingContext CreateContext(
            string token = null,
            string path = "/api/test")
        {
            var httpContext = new DefaultHttpContext();
            var serviceProvider = new Mock<IServiceProvider>();

            serviceProvider
                .Setup(s => s.GetService(typeof(ILogger<RequireAuthAttribute>)))
                .Returns(_mockLogger.Object);

            serviceProvider
                .Setup(s => s.GetService(typeof(IAuthenticationPolicy)))
                .Returns(_mockAuthPolicy.Object);

            serviceProvider
                .Setup(s => s.GetService(typeof(IHttpClientFactory)))
                .Returns(_mockHttpClientFactory.Object);

            serviceProvider
                .Setup(s => s.GetService(typeof(IConfiguration)))
                .Returns(_configuration);

            httpContext.RequestServices = serviceProvider.Object;
            httpContext.Request.Path = path;

            if (token != null)
            {
                httpContext.Request.Headers["Authorization"] = $"Bearer {token}";
            }

            return new ActionExecutingContext(
                new ActionContext(
                    httpContext,
                    new RouteData(),
                    new ActionDescriptor()
                ),
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                new Mock<Controller>().Object
            );
        }

        [Fact]
        public async Task OnActionExecutionAsync_MissingToken_ReturnsUnauthorized()
        {
            // Arrange
            var context = CreateContext();
            var nextCalled = false;

            // Act
            await _attribute.OnActionExecutionAsync(
                context,
                () =>
                {
                    nextCalled = true;
                    return Task.FromResult(new ActionExecutedContext(
                        context,
                        new List<IFilterMetadata>(),
                        new Mock<Controller>().Object
                    ));
                });

            // Assert
            Assert.IsType<UnauthorizedResult>(context.Result);
            Assert.False(nextCalled);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Authorization header is missing")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task OnActionExecutionAsync_ValidToken_CallsNext()
        {
            // Arrange
            var token = "valid_token";
            var context = CreateContext(token);
            var nextCalled = false;

            SetupSuccessfulHttpCall();
            _mockAuthPolicy
                .Setup(p => p.RetryPolicy)
                .Returns(Polly.Policy.NoOpAsync<HttpResponseMessage>());

            // Act
            await _attribute.OnActionExecutionAsync(
                context,
                () =>
                {
                    nextCalled = true;
                    return Task.FromResult(new ActionExecutedContext(
                        context,
                        new List<IFilterMetadata>(),
                        new Mock<Controller>().Object
                    ));
                });

            // Assert
            Assert.Null(context.Result);
            Assert.True(nextCalled);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Token successfully validated")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task OnActionExecutionAsync_InvalidToken_ReturnsUnauthorized()
        {
            // Arrange
            var token = "invalid_token";
            var context = CreateContext(token);
            var nextCalled = false;

            SetupFailedHttpCall(HttpStatusCode.Unauthorized);
            _mockAuthPolicy
                .Setup(p => p.RetryPolicy)
                .Returns(Polly.Policy.NoOpAsync<HttpResponseMessage>());

            // Act
            await _attribute.OnActionExecutionAsync(
                context,
                () =>
                {
                    nextCalled = true;
                    return Task.FromResult(new ActionExecutedContext(
                        context,
                        new List<IFilterMetadata>(),
                        new Mock<Controller>().Object
                    ));
                });

            // Assert
            Assert.IsType<UnauthorizedResult>(context.Result);
            Assert.False(nextCalled);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Token validation failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task OnActionExecutionAsync_ApiError_ReturnsServiceUnavailable()
        {
            // Arrange
            var token = "valid_token";
            var context = CreateContext(token);
            var nextCalled = false;

            _mockAuthPolicy
                .Setup(p => p.RetryPolicy)
                .Returns(Polly.Policy.NoOpAsync<HttpResponseMessage>());

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException("API Error"));

            // Act
            await _attribute.OnActionExecutionAsync(
                context,
                () =>
                {
                    nextCalled = true;
                    return Task.FromResult(new ActionExecutedContext(
                        context,
                        new List<IFilterMetadata>(),
                        new Mock<Controller>().Object
                    ));
                });

            // Assert
            Assert.IsType<StatusCodeResult>(context.Result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable,
                ((StatusCodeResult)context.Result).StatusCode);
            Assert.False(nextCalled);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Unrecoverable error")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
        [Fact]
        public async Task OnActionExecutionAsync_UsesRetryPolicy()
        {
            // Arrange
            var token = "valid_token";
            var context = CreateContext(token);
            var retryPolicyCalled = false;
            var callNumber = 0;
            var requestUris = new List<string>();

            // Setup HTTP handler to track requests and fail first attempt
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Returns(() =>
                {
                    callNumber++;

                    if (callNumber == 1)
                    {
                        return Task.FromException<HttpResponseMessage>(
                            new HttpRequestException("Simulated first call failure"));
                    }

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                });

            // Setup retry policy that we can monitor
            var retryPolicy = Policy<HttpResponseMessage>
       .HandleResult(msg => !msg.IsSuccessStatusCode)
       .Or<HttpRequestException>()
       .RetryAsync(1, (exception, _) =>
       {
           retryPolicyCalled = true;
       });

            _mockAuthPolicy
                .Setup(p => p.RetryPolicy)
                .Returns(retryPolicy);

            // Act
            await _attribute.OnActionExecutionAsync(
                context,
                () => Task.FromResult(new ActionExecutedContext(
                    context,
                    new List<IFilterMetadata>(),
                    new Mock<Controller>().Object
                )));

            // Assert
            Assert.True(retryPolicyCalled, "Retry policy should have been triggered");
            Assert.Equal(2, callNumber);

            // Verify the calls were made
            _mockHttpMessageHandler
                .Protected()
                .Verify(
                    "SendAsync",
                    Times.Exactly(2),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                );
        }// Add this helper method to the test class to dump the request details
        private void LogRequestDetails(HttpRequestMessage request)
        {
            Console.WriteLine($"Request URI: {request.RequestUri}");
            Console.WriteLine($"Request method: {request.Method}");
            Console.WriteLine("Request headers:");
            foreach (var header in request.Headers)
            {
                Console.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
        }
        private void SetupSuccessfulHttpCall()
        {
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK
                });
        }

        private void SetupFailedHttpCall(HttpStatusCode statusCode)
        {
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode
                });
        }
    }
}