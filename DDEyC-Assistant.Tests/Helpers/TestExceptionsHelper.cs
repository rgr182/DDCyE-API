// TestHelpers/TestExceptions.cs
using DDEyC_Assistant.Exceptions;

namespace DDEyC_Assistant.Tests.TestHelpers
{
    public static class TestExceptions
    {
        public static HttpRequestException CreateNetworkError()
        {
            return new HttpRequestException("Network error occurred");
        }

        public static OpenAIServiceException CreateTimeoutError()
        {
            return OpenAIServiceException.Timeout();
        }

        public static OpenAIServiceException CreateRunFailedError()
        {
            return new OpenAIServiceException(
                "Run failed with status: Failed", 
                "RUN_FAILED");
        }

        public static OpenAIServiceException CreateRateLimitError()
        {
            return OpenAIServiceException.RateLimit();
        }
    }
}