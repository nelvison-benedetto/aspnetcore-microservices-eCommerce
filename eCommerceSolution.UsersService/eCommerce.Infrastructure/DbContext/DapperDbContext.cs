using Microsoft.Extensions.Configuration;  //x read the app configurations using IConfiguration 
using Npgsql;  //Npgsql is a .NET data provider for PostgreSQL, e.g. NpgsqlConnection
using System.Data;  //contiene le astrazioni ADO.NET, tra cui l'interfaccia IDbConnection

namespace eCommerce.Infrastructure.DbContext;

public class DapperDbContext
{
    private readonly IConfiguration _configuration;  //x read the app configurations
    private readonly IDbConnection _connection;  //x astrazioni ADO.NET, IDbConnection è un'interfaccia che rappresenta una connessione a un database. NpgsqlConnection implementa questa interfaccia.

    public DapperDbContext(IConfiguration configuration)
    {
        _configuration = configuration;
        string connectionStringTemplate = _configuration.GetConnectionString("PostgresConnection")!;  //get connection str (cha ha ancora i placeholder) from appsettings.json 
        string connectionString = connectionStringTemplate  //sostituisci tutti i placeholders
          .Replace("$POSTGRES_HOST", Environment.GetEnvironmentVariable("POSTGRES_HOST"))
          .Replace("$POSTGRES_PASSWORD", Environment.GetEnvironmentVariable("POSTGRES_PASSWORD"))
          .Replace("$POSTGRES_DATABASE", Environment.GetEnvironmentVariable("POSTGRES_DATABASE"))
          .Replace("$POSTGRES_PORT", Environment.GetEnvironmentVariable("POSTGRES_PORT"))
          .Replace("$POSTGRES_USER", Environment.GetEnvironmentVariable("POSTGRES_USER"));

        //Create a new NpgsqlConnection with the retrieved connection string
        _connection = new NpgsqlConnection(connectionString);  //ora hai str connection giusta, la implementi (crei l'oggetto) con NpgsqlConnection
    }


    public IDbConnection DbConnection => _connection;  //expression-bodied property, equivale a 
    //public IDbConnection DbConnection
    //{
    //    get
    //    {
    //        return _connection;
    //    }
    //}


}