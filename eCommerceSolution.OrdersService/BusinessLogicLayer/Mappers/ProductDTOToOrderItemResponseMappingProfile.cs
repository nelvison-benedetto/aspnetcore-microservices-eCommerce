using AutoMapper;
using BusinessLogicLayer.DTO;

namespace BusinessLogicLayer.Mappers
{
    public class ProductDTOToOrderItemResponseMappingProfile : Profile
    {
        public ProductDTOToOrderItemResponseMappingProfile()
        {
            CreateMap<ProductDTO, OrderItemResponse>()
              .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.ProductName))
              .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Category))
              //Keep the values stored in the order: only ProductName and Category come from the Products microservice
              .ForMember(dest => dest.ProductID, opt => opt.Ignore())
              .ForMember(dest => dest.UnitPrice, opt => opt.Ignore())
              .ForMember(dest => dest.Quantity, opt => opt.Ignore())
              .ForMember(dest => dest.TotalPrice, opt => opt.Ignore());
        }
    }
}
