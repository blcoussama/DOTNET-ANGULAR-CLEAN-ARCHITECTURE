using CreditFlow.Application.Auth;
using CreditFlow.Application.Profiles;
using CreditFlow.Application.Review;
using CreditFlow.Application.Users;
using CreditFlow.Domain.Clients;

namespace CreditFlow.Application.Tests;

public sealed class ValidatorTests
{
    private readonly RegisterRequestValidator _register = new();

    [Theory]
    [InlineData("Motdepasse1!", true)]
    [InlineData("Motdepasse1", false)]
    [InlineData("Court1!", false)]
    [InlineData("motdepasse1!", false)]
    [InlineData("MOTDEPASSE1!", false)]
    [InlineData("Motdepasse!", false)]
    [InlineData("", false)]
    public void Register_password_policy(string password, bool valid) =>
        _register.Validate(new RegisterRequest("a@b.ma", password, "Salma", "Alaoui")).IsValid.ShouldBe(valid);

    [Theory]
    [InlineData("pas-un-email")]
    [InlineData("")]
    [InlineData(null)]
    public void Register_requires_a_valid_email(string? email) =>
        _register.Validate(new RegisterRequest(email, "Motdepasse1!", "Salma", "Alaoui")).Errors.ShouldContain(e => e.PropertyName == "Email");

    [Fact]
    public void Register_requires_names() =>
        _register.Validate(new RegisterRequest("a@b.ma", "Motdepasse1!", " ", new string('x', 101)))
            .Errors.Select(e => e.PropertyName).ShouldBe(["FirstName", "LastName"], ignoreOrder: true);

    [Fact]
    public void Login_requires_both_fields() =>
        new LoginRequestValidator().Validate(new LoginRequest(null, null)).Errors.Count.ShouldBe(2);

    [Theory]
    [InlineData(2008, 10, 15, true)]
    [InlineData(2008, 10, 16, false)]
    [InlineData(1926, 10, 15, true)]
    [InlineData(1925, 10, 14, false)]
    public void Profile_age_must_be_between_18_and_100(int year, int month, int day, bool valid)
    {
        var validator = new UpdateClientProfileRequestValidator(new FixedTimeProvider(TestData.Now));
        validator.Validate(new UpdateClientProfileRequest(new DateOnly(year, month, day), null, null, null, null, null, null, null))
            .IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData("+212 600 000 000", true)]
    [InlineData("0600000000", true)]
    [InlineData("06-00-00-00-00", true)]
    [InlineData("abc", false)]
    [InlineData("123", false)]
    public void Profile_phone_format(string phone, bool valid)
    {
        var validator = new UpdateClientProfileRequestValidator(new FixedTimeProvider(TestData.Now));
        validator.Validate(new UpdateClientProfileRequest(null, phone, null, null, null, null, null, null)).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Profile_amount_rules()
    {
        var validator = new UpdateClientProfileRequestValidator(new FixedTimeProvider(TestData.Now));
        var result = validator.Validate(new UpdateClientProfileRequest(null, null, null, EmploymentType.Cdi, null, 0m, -1m, 61));
        result.Errors.Select(e => e.PropertyName).ShouldBe(["MonthlyIncome", "MonthlyCharges", "YearsAtCurrentJob"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("Analyst", true)]
    [InlineData("Admin", true)]
    [InlineData("Client", false)]
    [InlineData("root", false)]
    public void Admin_can_only_create_staff_accounts(string role, bool valid) =>
        new CreateUserRequestValidator().Validate(new CreateUserRequest("staff@b.ma", "Youssef", "Idrissi", role, "Temporaire1!"))
            .IsValid.ShouldBe(valid);

    [Fact]
    public void Update_user_requires_at_least_one_field() =>
        new UpdateUserRequestValidator().Validate(new UpdateUserRequest(null, null)).Errors.Single().PropertyName.ShouldBe("request");

    [Theory]
    [InlineData("Superuser", false)]
    [InlineData("Client", false)]
    [InlineData("Analyst", true)]
    [InlineData("Admin", true)]
    public void Update_user_only_switches_between_staff_roles(string role, bool valid) =>
        new UpdateUserRequestValidator().Validate(new UpdateUserRequest(role, null)).IsValid.ShouldBe(valid);

    [Theory]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    [InlineData(1, 100, true)]
    public void Queue_pagination_bounds(int page, int pageSize, bool valid) =>
        new ReviewQueueQueryValidator().Validate(new ReviewQueueQuery(page, pageSize)).IsValid.ShouldBe(valid);

    [Fact]
    public void Decision_comment_is_limited_to_1000_characters()
    {
        var validator = new DecisionRequestValidator();
        validator.Validate(new DecisionRequest(new string('a', 1000))).IsValid.ShouldBeTrue();
        validator.Validate(new DecisionRequest(new string('a', 1001))).IsValid.ShouldBeFalse();
    }
}
