using Polly;

namespace DDEyC_Assistant.Policies
{
    public interface IAuthenticationPolicy
    {
        IAsyncPolicy<HttpResponseMessage> RetryPolicy { get; }
    }
}