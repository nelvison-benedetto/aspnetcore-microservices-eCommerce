using BusinessLogicLayer.DTO;
using Microsoft.Extensions.Caching.Distributed;  //Permette di leggere e scrivere dati in una cache distribuita, ad esempio Redis
using Microsoft.Extensions.Logging;  //Permette di registrare errori e informazioni nei log
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;   //Offre metodi come ReadFromJsonAsync<T>() per leggere risposte HTTP JSON
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Polly.CircuitBreaker;  //Contiene le eccezioni relative al Circuit Breaker
using Polly.Timeout;   //Contiene le eccezioni relative ai timeout gestiti da Polly

namespace BusinessLogicLayer.HttpClients
{
    public class UsersMicroserviceClient
    {
        private readonly HttpClient _httpClient;  //inviare richieste HTTP a un altro servizio
        private readonly ILogger<UsersMicroserviceClient> _logger;
        private readonly IDistributedCache _distributedCache;  //astrazione .NET per una cache distribuita, la puoi implementare successivamenet e.g. with redis

        public UsersMicroserviceClient(HttpClient httpClient, ILogger<UsersMicroserviceClient> logger, IDistributedCache distributedCache)
        {
            _httpClient = httpClient;
            _logger = logger;
            _distributedCache = distributedCache;
        }


        public async Task<UserDTO?> GetUserByUserID(Guid userID)
        {
            try
            {
                string cacheKeyToRead = $"user:{userID}";  //e poi e.g.i products saranno con 'product:...' ect e stesso per order order:...
                string? cachedUser = await _distributedCache.GetStringAsync(cacheKeyToRead);  //cerca il valore associato a questa key 
                if (cachedUser != null)
                {
                    //Deserialized the cached user
                    UserDTO? userFromCache = JsonSerializer.Deserialize<UserDTO>(cachedUser);
                    return userFromCache;
                }

                HttpResponseMessage response = await _httpClient.GetAsync($"/gateway/users/{userID}");  //richiesta http GET, HttpResponseMessage obj rappresenta la risposta http ricevuta (contiene status code, header, body)
                if (!response.IsSuccessStatusCode)  //IsSuccessStatusCode è tru per status code 200--299
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)  //se c'è eventuale fallback nol body
                    {
                        UserDTO? fallbackUser = await response.Content.ReadFromJsonAsync<UserDTO>();
                        if (fallbackUser == null)
                        {
                            throw new NotImplementedException();
                        }
                        return fallbackUser;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return null;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)  //richiesta rifiutata xk non valida
                    {
                        throw new HttpRequestException("Bad request", null, System.Net.HttpStatusCode.BadRequest);
                    }
                    else
                    {
                        //throw new HttpRequestException($"Http request failed with status code {response.StatusCode}");
                        return new UserDTO(
                          PersonName: "Temporarily Unavailable",
                          Email: "Temporarily Unavailable",
                          Gender: "Temporarily Unavailable",
                          UserID: Guid.Empty);
                    }
                }

                UserDTO? user = await response.Content.ReadFromJsonAsync<UserDTO>();
                if (user == null)
                {
                    throw new ArgumentException("Invalid User ID");
                }

                //Store the user data (retrieved from response) into cache
                string cacheKey = $"user:{userID}";
                string userJson = JsonSerializer.Serialize(user);  //c#->json
                DistributedCacheEntryOptions options = new DistributedCacheEntryOptions()
                  .SetAbsoluteExpiration(DateTimeOffset.UtcNow.AddMinutes(5)) //scade tra 5min (prende l'ora di adesso, e aggiunge 5min)
                  .SetAbsoluteExpiration(TimeSpan.FromMinutes(3)); //scade tra 3 min. (un'altro modo per settare la scadenza)
                //
                await _distributedCache.SetStringAsync(cacheKey, userJson, options); //SetStringAsync salva il JSON associandolo alla chiave e applicando le opzioni di scadenza
                return user;
            }
            catch (BrokenCircuitException ex)  //🔥Circuit Breaker (using policy di lib Polly). pattern di resilienza che protegge un'applicazione da chiamate ripetutamente fallite verso un servizio.
            {
                _logger.LogError(ex, "Request failed because of circuit breaker is in Open state. Returning dummy data.");

                return new UserDTO(
                        PersonName: "Temporarily Unavailable (circuit breaker)",
                        Email: "Temporarily Unavailable (circuit breaker)",
                        Gender: "Temporarily Unavailable (circuit breaker)",
                        UserID: Guid.Empty);
            }
            catch (TimeoutRejectedException ex)  //🔥Timeout (using policy di lib Polly)
            {
                _logger.LogError(ex, "Timeout occurred while fetching user data. Returning dummy data");
                return new UserDTO(
                        PersonName: "Temporarily Unavailable (timeout)",
                        Email: "Temporarily Unavailable (timeout)",
                        Gender: "Temporarily Unavailable (timeout)",
                        UserID: Guid.Empty);
            }
        }
    }
}
