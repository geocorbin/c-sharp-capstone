using UserService.Dtos;
using UserService.Validators;

namespace UserService.Tests;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    private static RegisterRequest ValidRequest() => new()
    {
        Email = "test@example.com",
        Password = "Test123!@#",
        FirstName = "Test",
        LastName = "User",
        PhoneNumber = "+1-555-0123"
    };

    [Fact]
    public async Task Validate_AllFieldsValid_Passes()
    {
        var result = await _validator.ValidateAsync(ValidRequest());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoDigitsHere!")]
    [InlineData("NoSpecial123")]
    [InlineData("Sh0rt!")]
    public async Task Validate_PasswordFailsAComplexityRule_Fails(string weakPassword)
    {
        var request = ValidRequest();
        request.Password = weakPassword;

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_InvalidEmailFormat_Fails()
    {
        var request = ValidRequest();
        request.Email = "not-an-email";

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
    }
}