using AutoMapper;
using eCommerce.Core.DTO;
using eCommerce.Core.Entities;
using eCommerce.Core.RepositoryContracts;
using eCommerce.Core.ServiceContracts;

namespace eCommerce.Core.Services;

internal class UsersService : IUsersService  //internal cioe visible only in this assembly (prj)
{
    private readonly IUsersRepository _usersRepository;
    private readonly IMapper _mapper;  //questo continene le regole di mapping x fare e.g._mapper.Map<UserDTO>(user);

    public UsersService(IUsersRepository usersRepository, IMapper mapper)
    {
        _usersRepository = usersRepository;
        _mapper = mapper;
    }  //ora i _usersRepository e _mapper di questa classe, sono aggiornati con i valori passati dall'esterno

    public async Task<UserDTO?> GetUserByUserID(Guid userID)
    {
        ApplicationUser? user = await _usersRepository.GetUserByUserID(userID);
        if (user == null)
        {
            return null;
        }
        return _mapper.Map<UserDTO>(user);
    }

    public async Task<AuthenticationResponse?> Login(LoginRequest loginRequest)
    {
        ApplicationUser? user = await _usersRepository.GetUserByEmailAndPassword(loginRequest.Email, loginRequest.Password);
        if (user == null)
        {
            return null;
        }
        //return new AuthenticationResponse(user.UserID, user.Email, user.PersonName, user.Gender, "token", Success: true);
        return _mapper.Map<AuthenticationResponse>(user) with { Success = true, Token = "token" };  //with is used to create a new record with updated properties, in this case, Success and Token
    }


    public async Task<AuthenticationResponse?> Register(RegisterRequest registerRequest)
    {
        //Create a new ApplicationUser object from RegisterRequest
        ApplicationUser user = _mapper.Map<ApplicationUser>(registerRequest);
        ApplicationUser? registeredUser = await _usersRepository.AddUser(user);
        if (registeredUser == null)
        {
            return null;
        }
        //Return success response
        //return new AuthenticationResponse(registeredUser.UserID, registeredUser.Email, registeredUser.PersonName, registeredUser.Gender, "token", Success: true);
        return _mapper.Map<AuthenticationResponse>(registeredUser) with { Success = true, Token = "token" };
    }

}