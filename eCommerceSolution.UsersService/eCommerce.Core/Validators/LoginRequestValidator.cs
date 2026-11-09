using eCommerce.Core.DTO;
using FluentValidation;  //comodo x creare le regole di validazione dei DTO, in questo caso LoginRequest

namespace eCommerce.Core.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>  //AbstractValidator is a class from FluentValidation that provides a base for creating validators for specific types, in this case, LoginRequest
{
    public LoginRequestValidator()  //constructor
    {
        //Email
        RuleFor(temp => temp.Email)
          .NotEmpty().WithMessage("Email is required")   //flunetvalidator considara empty nulls, "" e "   "
          .EmailAddress().WithMessage("Invalid email address format")
          ;

        //Password
        RuleFor(temp => temp.Password)
          .NotEmpty().WithMessage("Password is required")
          ;
    }
}