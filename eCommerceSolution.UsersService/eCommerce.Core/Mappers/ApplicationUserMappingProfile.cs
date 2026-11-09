using AutoMapper;  
using eCommerce.Core.DTO;
using eCommerce.Core.Entities;

namespace eCommerce.Core.Mappers;

//questo x mappare in USCITA
public class ApplicationUserMappingProfile : Profile  //Profile è una classe di AutoMapper che serve per definire le regole di mappatura tra oggetti di tipo diverso.
{
    public ApplicationUserMappingProfile()  //x mappare auto e.g. ApplicationUser (utente nel dominio nell'app) -> AuthenticationResponse (cio che devi sputare fuori dall'app e darlo al frontend)
    {
        CreateMap<ApplicationUser, AuthenticationResponse>()
          .ForMember(
            dest => dest.UserID, //dest con automapper significa l'obj di destinazione (e.g.AuthenticationResponse)
            opt => opt.MapFrom(src => src.UserID)  //src con automapper significa l'obj sorgente (e.g.ApplicationUser). opt serve solo x usare le opzioni di configurazione (cioè .MapFrom())
           )  //quindi fa copia ApplicationUser.UserID in AuthenticationResponse.UserID
          .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
          .ForMember(dest => dest.PersonName, opt => opt.MapFrom(src => src.PersonName))
          .ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.Gender))
          .ForMember(dest => dest.Success, opt => opt.Ignore())
          .ForMember(dest => dest.Token, opt => opt.Ignore())
          ;
    }
}