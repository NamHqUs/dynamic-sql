using Demo.Contracts.Application;

namespace Demo.WebApi.Application.Middlewares;

public class UserSectionManager : IUserSectionManager
{
    public string GetSectionId()
    {
        return "123";
    }
}
