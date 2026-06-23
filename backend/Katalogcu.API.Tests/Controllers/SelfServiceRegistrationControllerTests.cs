using Katalogcu.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Katalogcu.API.Tests.Controllers;

public sealed class SelfServiceRegistrationControllerTests
{
    [Fact]
    public void OwnerRegister_ReturnsForbidden()
    {
        var controller = new AuthController(null!);

        var result = Assert.IsType<ObjectResult>(controller.Register());

        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void PublicCustomerRegister_ReturnsForbidden()
    {
        var controller = new CustomersController(null!, null!);

        var result = Assert.IsType<ObjectResult>(controller.PublicRegister());

        Assert.Equal(403, result.StatusCode);
    }
}
