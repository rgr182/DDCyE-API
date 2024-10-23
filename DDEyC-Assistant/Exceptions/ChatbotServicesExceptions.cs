namespace DDEyC_Assistant.Exceptions
{
    public class ChatServiceException : Exception
    {
        public string ErrorCode { get; }

        public ChatServiceException(string message, string errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        public ChatServiceException(string message, string errorCode, Exception innerException) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }

    public class OpenAIServiceException : ChatServiceException
    {
        public OpenAIServiceException(string message, string errorCode) 
            : base(message, errorCode)
        {
        }

        public OpenAIServiceException(string message, string errorCode, Exception innerException) 
            : base(message, errorCode, innerException)
        {
        }

        public static OpenAIServiceException RateLimit() =>
            new OpenAIServiceException("OpenAI API rate limit exceeded", "RATE_LIMIT");

        public static OpenAIServiceException NetworkError(Exception innerException) =>
            new OpenAIServiceException("Network error occurred while communicating with OpenAI API", "NETWORK_ERROR", innerException);

        public static OpenAIServiceException Timeout() =>
            new OpenAIServiceException("OpenAI API request timed out", "TIMEOUT");
    }
}