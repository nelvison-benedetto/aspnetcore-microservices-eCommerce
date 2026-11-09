using AutoMapper;
using BusinessLogicLayer.DTO;

namespace BusinessLogicLayer.Mappers
{
    public class UserDTOToOrderResponseMappingProfile : Profile
    {
        public UserDTOToOrderResponseMappingProfile()
        {
            CreateMap<UserDTO, OrderResponse>()
              .ForMember(dest => dest.UserPersonName, opt => opt.MapFrom(src => src.PersonName))
              .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
              //Keep the values stored in the order: only UserPersonName and Email come from the Users microservice
              .ForMember(dest => dest.UserID, opt => opt.Ignore())
              .ForMember(dest => dest.OrderID, opt => opt.Ignore())
              .ForMember(dest => dest.OrderDate, opt => opt.Ignore())
              .ForMember(dest => dest.TotalBill, opt => opt.Ignore())
              .ForMember(dest => dest.OrderItems, opt => opt.Ignore());
        }
    }
}
