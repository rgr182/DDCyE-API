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
}