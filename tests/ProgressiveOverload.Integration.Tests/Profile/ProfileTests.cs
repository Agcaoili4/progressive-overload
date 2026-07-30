using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ProgressiveOverload.Application.Users;
using ProgressiveOverload.Application.Users.GetProfile;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Profile;

[Collection(nameof(PostgresCollection))]
public sealed class ProfileTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> AnAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"{Guid.NewGuid():N}@example.com",
            password = "correct horse battery staple",
            displayName = "Jansen"
        });

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/me");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_ReturnsTheAuthenticatedUser()
    {
        var client = await AnAuthenticatedClient();

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/v1/me");

        profile.ShouldNotBeNull();
        profile.DisplayName.ShouldBe("Jansen");
        profile.CurrentBodyweightKg.ShouldBeNull();
    }

    [Fact]
    public async Task PatchMe_UpdatesProfileFields()
    {
        var client = await AnAuthenticatedClient();

        var response = await client.PatchAsJsonAsync("/api/v1/me", new
        {
            displayName = "Jansen A",
            bio = "Chasing a 200kg squat.",
            sex = 1,
            experienceLevel = 3,
            units = 2
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/v1/me");
        profile!.DisplayName.ShouldBe("Jansen A");
        profile.Bio.ShouldBe("Chasing a 200kg squat.");
    }

    [Fact]
    public async Task PatchMe_RejectsOverlongDisplayName()
    {
        var client = await AnAuthenticatedClient();

        var response = await client.PatchAsJsonAsync("/api/v1/me", new
        {
            displayName = new string('x', 31),
            bio = (string?)null,
            sex = (int?)null,
            experienceLevel = (int?)null,
            units = 1
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostBodyweight_UpdatesCurrentWeight()
    {
        var client = await AnAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/v1/me/bodyweight", new { weightKg = 84.5m });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/v1/me");
        profile!.CurrentBodyweightKg.ShouldBe(84.5m);
    }

    [Fact]
    public async Task PostBodyweight_RejectsImplausibleValue()
    {
        var client = await AnAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/v1/me/bodyweight", new { weightKg = 900m });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OneUsersTokenNeverReturnsAnotherUsersProfile()
    {
        var alice = await AnAuthenticatedClient();
        var bob = await AnAuthenticatedClient();

        var aliceProfile = await alice.GetFromJsonAsync<ProfileResponse>("/api/v1/me");
        var bobProfile = await bob.GetFromJsonAsync<ProfileResponse>("/api/v1/me");

        aliceProfile!.Id.ShouldNotBe(bobProfile!.Id);
    }
}
