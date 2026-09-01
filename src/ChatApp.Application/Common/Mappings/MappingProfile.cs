using AutoMapper;
using ChatApp.Application.DTOs;
using ChatApp.Domain.Entities;

namespace ChatApp.Application.Common.Mappings;

/// <summary>
/// [AutoMapper] Centralized mapping configuration.
/// Tách biệt mapping logic khỏi handler → Single Responsibility.
///
/// Lệnh chạy mapping:
///   var dto = _mapper.Map&lt;MessageDto&gt;(message);
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Message → MessageDto
        CreateMap<Message, MessageDto>()
            .ForMember(dest => dest.Sender,
                opt => opt.MapFrom(src => src.Sender.Username))
            .ForMember(dest => dest.AvatarIndex,
                opt => opt.MapFrom(src => src.Sender.AvatarIndex))
            .ForMember(dest => dest.Timestamp,
                opt => opt.MapFrom(src => src.CreatedAt));

        // User → UserDto
        CreateMap<User, UserDto>();

        // Room → RoomDto
        CreateMap<Room, RoomDto>()
            .ForMember(dest => dest.MemberCount,
                opt => opt.MapFrom(src => src.Members.Count));
    }
}
