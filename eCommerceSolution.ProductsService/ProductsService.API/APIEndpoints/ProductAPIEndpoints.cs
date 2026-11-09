using BusinessLogicLayer.DTO;
using BusinessLogicLayer.ServiceContracts;
using FluentValidation;
using FluentValidation.Results;

namespace eCommerce.ProductsMicroService.API.APIEndpoints;

public static class ProductAPIEndpoints
{
    public static IEndpointRouteBuilder MapProductAPIEndpoints(this IEndpointRouteBuilder app)  //extension method, return type IEndpointRouteBuilder
    {
        //GET /api/products
        app.MapGet("/api/products", async (IProductsService productsService) =>   //=> cioè lambda function
        {
            List<ProductResponse?> products = await productsService.GetProducts();
            return Results.Ok(products);
        });


        //GET /api/products/search/product-id/00000000-0000-0000-0000-000000000000
        app.MapGet("/api/products/search/product-id/{ProductID:guid}", async (IProductsService productsService, Guid ProductID) =>
        {
            //await Task.Delay(100);
            //throw new NotImplementedException();


            ProductResponse? product = await productsService.GetProductByCondition(temp => temp.ProductID == ProductID);
            if (product == null)
                return Results.NotFound();

            return Results.Ok(product);
        });


        //GET /api/products/search/xxxxxxxxxxxxxxxxxx
        app.MapGet("/api/products/search/{SearchString}", async (IProductsService productsService, string SearchString) =>
        {
            //StringComparison overloads can't be translated to SQL by EF Core: use ToLower() for a case-insensitive search
            string search = SearchString.ToLower();

            List<ProductResponse?> productsByProductName = await productsService.GetProductsByCondition(temp => temp.ProductName != null && temp.ProductName.ToLower().Contains(search));

            List<ProductResponse?> productsByCategory = await productsService.GetProductsByCondition(temp => temp.Category != null && temp.Category.ToLower().Contains(search));

            var products = productsByProductName.Union(productsByCategory);

            return Results.Ok(products);
        });


        //POST /api/products
        app.MapPost("/api/products", async (IProductsService productsService, IValidator<ProductAddRequest> productAddRequestValidator, ProductAddRequest productAddRequest) =>
        {
            //Validate the ProductAddRequest object using Fluent Validation
            ValidationResult validationResult = await productAddRequestValidator.ValidateAsync(productAddRequest);

            //Check the validation result
            if (!validationResult.IsValid)
            {
                Dictionary<string, string[]> errors = validationResult.Errors
                  .GroupBy(temp => temp.PropertyName)  //raggruppa gli errori in base al nome della proprietà
                  .ToDictionary(grp => grp.Key, grp => grp.Select(err => err.ErrorMessage).ToArray());  //trasforma i gruppi in un dizionario
                return Results.ValidationProblem(errors);  //restituisce una risposta HTTP di tipo 400 Bad Request, con un corpo conforme al formato standard Problem Details per gli errori di validazione.
            }

            var addedProductResponse = await productsService.AddProduct(productAddRequest);
            if (addedProductResponse != null)
                return Results.Created($"/api/products/search/product-id/{addedProductResponse.ProductID}", addedProductResponse);  //risorsa creata
            else
                return Results.Problem("Error in adding product");
            //Result è metodo di ASP.NET Core che utilizzi per restituire una risposta HTTP contenente gli errori prodotti da FluentValidation.
            //Result.Ok(data) (200 operazione riuscita), .Created(url, data), .NotFound(), .ValidationProblem(errors), .Problem(...) (errore interno).
        });


        //PUT /api/products
        app.MapPut("/api/products", async (IProductsService productsService, IValidator<ProductUpdateRequest> productUpdateRequestValidator, ProductUpdateRequest productUpdateRequest) =>
        {
            //Validate the ProductUpdateRequest object using Fluent Validation
            ValidationResult validationResult = await productUpdateRequestValidator.ValidateAsync(productUpdateRequest);

            //Check the validation result
            if (!validationResult.IsValid)
            {
                Dictionary<string, string[]> errors = validationResult.Errors
                  .GroupBy(temp => temp.PropertyName)
                  .ToDictionary(grp => grp.Key,
                    grp => grp.Select(err => err.ErrorMessage).ToArray());
                return Results.ValidationProblem(errors);
            }


            var updatedProductResponse = await productsService.UpdateProduct(productUpdateRequest);
            if (updatedProductResponse != null)
                return Results.Ok(updatedProductResponse);
            else
                return Results.Problem("Error in updating product");
        });


        //DELETE /api/products/xxxxxxxxxxxxxxxxxxx
        app.MapDelete("/api/products/{ProductID:guid}", async (IProductsService productsService, Guid ProductID) =>
        {
            bool isDeleted = await productsService.DeleteProduct(ProductID);
            if (isDeleted)
                return Results.Ok(true);
            else
                return Results.Problem("Error in deleting product");
        });
        return app;
    }
}