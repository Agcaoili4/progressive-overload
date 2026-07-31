using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ProgressiveOverload.Application.Users;
using ProgressiveOverload.Application.Users.GetProfile;
using ProgressiveOverload.Domain.Users;
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
            displayName = new string('x', User.MaxDisplayNameLength + 1),
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

    /*
        Two entries for one user, the second backdated. Covers the backfill rule end to end
        and the second insert against a User whose entry collection was never loaded — the
        handler reads only the scalar CurrentBodyweightAt, so it must not need the history.
    */
    [Fact]
    public async Task PostBodyweight_BackdatedEntry_DoesNotOverwriteCurrentWeight()
    {
        var client = await AnAuthenticatedClient();

        await client.PostAsJsonAsync("/api/v1/me/bodyweight", new { weightKg = 84.5m });

        var backdated = await client.PostAsJsonAsync("/api/v1/me/bodyweight", new
        {
            weightKg = 91.0m,
            recordedAt = DateTimeOffset.UtcNow.AddDays(-30)
        });

        backdated.StatusCode.ShouldBe(HttpStatusCode.Created);

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

    /*
        Adversarial by design: Alice puts Bob's id everywhere a handler might wrongly read it
        — query string and request body, on the read and on both writes. Asserting only that
        two ids differ would stay green even if every endpoint honoured the injected id.
    */
    [Fact]
    public async Task OneUsersTokenNeverReachesAnotherUsersProfile()
    {
        var alice = await AnAuthenticatedClient();
        var bob = await AnAuthenticatedClient();

        var aliceProfile = await alice.GetFromJsonAsync<ProfileResponse>("/api/v1/me");
        var bobProfile = await bob.GetFromJsonAsync<ProfileResponse>("/api/v1/me");
        aliceProfile!.Id.ShouldNotBe(bobProfile!.Id);

        var read = await alice.GetFromJsonAsync<ProfileResponse>(
            $"/api/v1/me?userId={bobProfile.Id}&id={bobProfile.Id}");
        read!.Id.ShouldBe(aliceProfile.Id);
        read.Email.ShouldBe(aliceProfile.Email);

        var patch = await alice.PatchAsJsonAsync($"/api/v1/me?userId={bobProfile.Id}", new
        {
            id = bobProfile.Id,
            userId = bobProfile.Id,
            displayName = "Alice Was Here",
            bio = (string?)null,
            sex = (int?)null,
            experienceLevel = (int?)null,
            units = 1
        });
        patch.StatusCode.ShouldBe(HttpStatusCode.OK);

        var bodyweight = await alice.PostAsJsonAsync($"/api/v1/me/bodyweight?userId={bobProfile.Id}", new
        {
            userId = bobProfile.Id,
            weightKg = 70.0m
        });
        bodyweight.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Bob's row must be untouched by either write, and Alice's must carry both.
        var bobAfter = await bob.GetFromJsonAsync<ProfileResponse>("/api/v1/me");
        bobAfter!.DisplayName.ShouldBe(bobProfile.DisplayName);
        bobAfter.CurrentBodyweightKg.ShouldBeNull();

        var aliceAfter = await alice.GetFromJsonAsync<ProfileResponse>("/api/v1/me");
        aliceAfter!.DisplayName.ShouldBe("Alice Was Here");
        aliceAfter.CurrentBodyweightKg.ShouldBe(70.0m);
    }
}
