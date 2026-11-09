using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Context;

public class ApplicationDbContext : DbContext  //erediti da DbContext di ef
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)  //DbContextOptions<ApplicationDbContext> options contiene tutto quello che ti serve, e passa le opzioni al costruttore della classe base DbContext
    {
    }

    public DbSet<Product> Products { get; set; }  //cosi puoi fare e.g.dbContext.Products.Add(new Product{ Name = "Keyboard", Price = 49.99m});

    protected override void OnModelCreating(ModelBuilder modelBuilder) 
    {
        base.OnModelCreating(modelBuilder);  //esegue quello della classe base

    }
}