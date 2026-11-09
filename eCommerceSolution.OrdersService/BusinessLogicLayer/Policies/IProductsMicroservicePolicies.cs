using Polly;

namespace BusinessLogicLayer.Policies
{
    public interface IProductsMicroservicePolicies
    {
        IAsyncPolicy<HttpResponseMessage> GetFallbackPolicy();
        IAsyncPolicy<HttpResponseMessage> GetBulkheadIsolationPolicy();
    }
}
