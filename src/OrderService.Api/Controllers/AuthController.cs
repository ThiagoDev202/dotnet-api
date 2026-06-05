using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Security;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(IJwtTokenService jwtTokenService)
        => _jwtTokenService = jwtTokenService;

    /// <summary>
    /// Emite um token JWT para o customerId e role informados.
    /// Role aceita: "Customer" ou "Admin".
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GenerateToken([FromBody] TokenRequest request)
    {
        if (request.CustomerId == Guid.Empty)
            return ValidationProblem(new ValidationProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dados inválidos",
                Detail = "O campo 'customerId' é obrigatório e não pode ser o GUID vazio.",
                Errors = { ["customerId"] = ["O customerId não pode ser um GUID vazio (all-zeros)."] }
            });

        if (string.IsNullOrWhiteSpace(request.Role))
            return ValidationProblem(new ValidationProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dados inválidos",
                Detail = "O campo 'role' é obrigatório. Valores aceitos: 'Customer', 'Admin'.",
                Errors = { ["role"] = ["O campo role é obrigatório."] }
            });

        var token = _jwtTokenService.GenerateToken(request.CustomerId, request.Role);
        return Ok(new TokenResponse(token));
    }
}

public sealed record TokenRequest(Guid CustomerId, string Role);
public sealed record TokenResponse(string Token);
