using Dapper;   //al posto di Entity Framework, dapper è piu veloce, ma è meno completo di strumenti come lo è invece ef.
using eCommerce.Core.Entities;
using eCommerce.Core.RepositoryContracts;
using eCommerce.Infrastructure.DbContext;

namespace eCommerce.Infrastructure.Repositories;

internal class UsersRepository : IUsersRepository  //internal, visibilita only within this assembly 
{
    private readonly DapperDbContext _dbContext;

    public UsersRepository(DapperDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplicationUser?> AddUser(ApplicationUser user)   //async x usare await within this method, return un Task cioe un oggetto che rappresenta un'operazione asincrona, che puo ritornare un valore di tipo ApplicationUser o null. dalla parte del chiamante fai e.g. ApplicationUser? result = await repository.AddUser(user); quindi con await aspetti che questa operazione asincrona sia completata.
    {
        //Generate a new unique user ID for the user
        user.UserID = Guid.NewGuid();

        // SQL Query to insert user data into the "Users" table.
        string query = "INSERT INTO public.\"Users\"(\"UserID\", \"Email\", \"PersonName\", \"Gender\", \"Password\") VALUES(@UserID, @Email, @PersonName, @Gender, @Password)";  //il tutto è gia tra "...", ma se vuoi che e.g."User" allora devi usare '\"'
        int rowCountAffected = await _dbContext.DbConnection.ExecuteAsync(query, user);

        if (rowCountAffected > 0)
        {
            return user;
        }
        else
        {
            return null;
        }
    }

    public async Task<ApplicationUser?> GetUserByEmailAndPassword(string? email, string? password)
    {
        //SQL query to select a user by Email and Password
        string query = "SELECT * FROM public.\"Users\" WHERE \"Email\"=@Email AND \"Password\"=@Password";
        var parameters = new { Email = email, Password = password };

        ApplicationUser? user = await _dbContext.DbConnection.QueryFirstOrDefaultAsync<ApplicationUser>(query, parameters);

        return user;
    }


    public async Task<ApplicationUser?> GetUserByUserID(Guid? userID)
    {
        var query = "SELECT * FROM public.\"Users\" WHERE \"UserID\" = @UserID";
        var parameters = new { UserID = userID };

        ApplicationUser? user = await _dbContext.DbConnection.QueryFirstOrDefaultAsync<ApplicationUser>(query, parameters);

        return user;
    }
}