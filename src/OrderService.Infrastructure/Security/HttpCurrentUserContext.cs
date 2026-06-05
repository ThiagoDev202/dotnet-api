using Microsoft.AspNetCore.Http;

namespace OrderService.Infrastructure.Security;

public sealed class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUserContext(IHttpContextAccessor accessor)
        => _accessor = accessor;

    public Guid? CustomerId
    {
        get
        {
            var claim = _accessor.HttpContext?.User.FindFirst("customerId")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool IsAdmin
        => _accessor.HttpContext?.User.IsInRole("Admin") is true
           || _accessor.HttpContext?.User.FindFirst("role")?.Value == "Admin";
}
