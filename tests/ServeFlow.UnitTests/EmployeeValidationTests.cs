using ServeFlow.Application.Accounts;
using ServeFlow.Application.Security;

namespace ServeFlow.UnitTests;

public sealed class EmployeeValidationTests
{
    [Theory]
    [InlineData("Administrator")]
    [InlineData("Waiter")]
    [InlineData("KitchenStaff")]
    public async Task StaffRolesAreAccepted(string role)
    {
        var result = await new CreateEmployeeValidator().ValidateAsync(
            new CreateEmployeeRequest("employee@example.test", "Employee", "Test-only-password42!", role), TestContext.Current.CancellationToken);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("administrator")]
    [InlineData("Superuser")]
    public async Task UnsupportedEmployeeRolesAreRejected(string role)
    {
        var result = await new CreateEmployeeValidator().ValidateAsync(
            new CreateEmployeeRequest("employee@example.test", "Employee", "Test-only-password42!", role), TestContext.Current.CancellationToken);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task InvalidEmailEmptyNameAndShortPasswordAreRejected()
    {
        var result = await new CreateEmployeeValidator().ValidateAsync(
            new CreateEmployeeRequest("not-email", "", "short", StaffRoles.Waiter), TestContext.Current.CancellationToken);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public async Task EditingRequiresIdentityAndConcurrencyVersion()
    {
        var result = await new EditEmployeeValidator().ValidateAsync(
            new EditEmployeeRequest(Guid.Empty, "Employee", StaffRoles.Waiter, false, ""), TestContext.Current.CancellationToken);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == "Id");
        Assert.Contains(result.Errors, x => x.PropertyName == "ConcurrencyStamp");
    }
}
