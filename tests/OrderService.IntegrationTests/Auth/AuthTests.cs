using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OrderService.IntegrationTests.Infrastructure;

namespace OrderService.IntegrationTests.Auth;

[Collection("Integration")]
public sealed class AuthTests
{
    private readonly OrderApiFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private record TokenResponse(string Token);

    public AuthTests(OrderApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GET_orders_sem_token_retorna_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_auth_token_com_dados_validos_retorna_200_com_access_token()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var customerId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/auth/token",
            new { CustomerId = customerId, Role = "Customer" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        data.Should().NotBeNull();
        data!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task POST_auth_token_com_customerId_vazio_retorna_400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/token",
            new { CustomerId = Guid.Empty, Role = "Customer" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_auth_token_com_role_vazio_retorna_400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/token",
            new { CustomerId = Guid.NewGuid(), Role = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_orders_com_token_valido_retorna_200()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId);

        var response = await client.GetAsync("/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task POST_auth_refresh_sem_cookie_retorna_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/auth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ciclo_completo_token_refresh_logout_revoga_acesso()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        var customerId = Guid.NewGuid();

        // Obtém access token + refresh cookie
        var tokenResp = await client.PostAsJsonAsync("/auth/token",
            new { CustomerId = customerId, Role = "Customer" });
        tokenResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenData = await tokenResp.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        var accessToken = tokenData!.Token;

        // Usa o token
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        (await client.GetAsync("/orders")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Renova via cookie (rotaciona refresh token)
        client.DefaultRequestHeaders.Authorization = null;
        var refreshResp = await client.PostAsync("/auth/refresh", null);
        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTokenData = await refreshResp.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        var newToken = newTokenData!.Token;

        // Usa o novo token
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
        (await client.GetAsync("/orders")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Faz logout (revoga jti do access token)
        var logoutResp = await client.PostAsync("/auth/logout", null);
        logoutResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Token revogado → 401
        var afterLogout = await client.GetAsync("/orders");
        afterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_auth_refresh_com_token_valido_retorna_novo_access_token()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        var customerId = Guid.NewGuid();

        // Obtém par inicial
        var tokenResp = await client.PostAsJsonAsync("/auth/token",
            new { CustomerId = customerId, Role = "Customer" });
        tokenResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await tokenResp.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);

        // Renova
        var refreshResp = await client.PostAsync("/auth/refresh", null);
        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await refreshResp.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);

        second!.Token.Should().NotBeNullOrWhiteSpace();
        second.Token.Should().NotBe(first!.Token);
    }
}
