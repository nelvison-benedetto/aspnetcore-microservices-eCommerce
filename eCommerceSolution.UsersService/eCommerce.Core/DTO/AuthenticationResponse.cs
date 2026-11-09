namespace eCommerce.Core.DTO;  

//DTO(Data Transfer Object), ottimo per scambiare pezzi di dati all'interno dell'applicazione
public record AuthenticationResponse(  //record, a differza di una class , è immutabile e fornisce un'implementazione predefinita di Equals e GetHashCode e anche code piu compatto
  Guid UserID,
  string? Email,
  string? PersonName,
  string? Gender,
  string? Token,
  bool Success
  )
{
    public AuthenticationResponse() : this(default, default, default, default, default, default)  //runna il costructor di default, passandogli valorui default per ogni param
    {
    }

}