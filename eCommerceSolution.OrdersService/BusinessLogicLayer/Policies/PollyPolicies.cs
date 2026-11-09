using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace BusinessLogicLayer.Policies
{
    //x resilience policies di Polly (lib), i piu usati CircuitBreaker,Timeout,Dealbreaker
    public class PollyPolicies : IPollyPolicies
    {
        private readonly ILogger<UsersMicroservicePolicies> _logger;

        public PollyPolicies(ILogger<UsersMicroservicePolicies> logger)
        {
            _logger = logger;
        }

        //ur policy Retry
        public IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount)  //IAsyncPolicy<HttpResponseMessage> restituisce una policy asincrona che opera su risultati di tipo HttpResponseMessage
        {
            AsyncRetryPolicy<HttpResponseMessage> policy = Policy.HandleResult<HttpResponseMessage>(r => !  r.IsSuccessStatusCode)  //! you know. r è la risposta http ricevuta, .IsSuccessStatusCode è true per status code 200-299
          .WaitAndRetryAsync(
             retryCount: retryCount, //Number of retries
             sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Delay between retries
             onRetry: (outcome, timespan, retryAttempt, context) =>
             {
                 _logger.LogInformation($"Retry {retryAttempt} after {timespan.TotalSeconds} seconds");
             });  //onRetry questo callback viene eseguito quando Polly sta per effetturare un retry. outcome(il risultato HTTP che ha attivato il retry), timespan(quanto tempo polly attendera prima di riprovare), retryAttempt(numero del retry), context(contesto associato all'esecuzione della policy).
            return policy;
        }

        //ur policy CircuitBreaker
        public IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(int handledEventsAllowedBeforeBreaking, TimeSpan durationOfBreak)
        {
            AsyncCircuitBreakerPolicy<HttpResponseMessage> policy = Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
          .CircuitBreakerAsync(  //dopo 3 risultati falliti consecutivi, il circuito si apre x 30 secs
             handledEventsAllowedBeforeBreaking: handledEventsAllowedBeforeBreaking, //Threshold for failed requests
             durationOfBreak: durationOfBreak, // Waiting time to be in "Open" state
             onBreak: (outcome, timespan) =>
             {
                 _logger.LogInformation($"Circuit breaker opened for {timespan.TotalMinutes} minutes due to consecutive 3 failures. The subsequent requests will be blocked");
             },
             onReset: () => {
                 _logger.LogInformation($"Circuit breaker closed. The subsequent requests will be allowed.");
             });
            return policy;
        }

        //ur policy Timeout
        public IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(TimeSpan timeout)
        {
            AsyncTimeoutPolicy<HttpResponseMessage> policy = Policy.TimeoutAsync<HttpResponseMessage>(timeout);
            return policy;
        }


    }
}
