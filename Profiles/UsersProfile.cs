using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Profiles
{
    public class UsersProfile : Profile
    {
        public UsersProfile()
        {
            // Source -> Target
            CreateMap<User, UserReadDTO>();
            CreateMap<UserCreateDTO, User>();
            CreateMap<UserReadDTO, UserQueryPublishedDto>();
            CreateMap<User, GrpcUserModel>()
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.Created, opt => opt.MapFrom(src => Timestamp.FromDateTime(src.Created.ToUniversalTime())));
            
            CreateMap<UserReadDTO, UserCommandPublishedDto>();
        }
    }
}