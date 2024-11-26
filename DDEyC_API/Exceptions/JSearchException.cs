namespace DDEyC_API.Exceptions
{
    public class JSearchException : Exception
    {
        public JSearchException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}