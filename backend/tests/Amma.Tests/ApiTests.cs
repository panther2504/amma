using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amma.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Amma.Tests;

public class AmmaFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Amma", "");
        builder.UseSetting("Database:InMemoryName", "amma-tests-" + Guid.NewGuid().ToString("N"));
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-0123456789abcdef0123456789");
        builder.UseSetting("Otp:ReturnCodeInResponse", "true");
        builder.UseSetting("Content:IncludeUnreviewed", "true");
        builder.UseSetting("Reminders:Enabled", "false");
        builder.UseSetting("RateLimits:AuthPerMinute", "1000");
        builder.UseSetting("Storage:RootPath", Path.Combine(Path.GetTempPath(), "amma-tests-" + Guid.NewGuid().ToString("N")));
    }
}

public class ApiTests(AmmaFactory factory) : IClassFixture<AmmaFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private static long _phone = 7000000000;

    private async Task<(HttpClient client, AuthResponse auth)> SignInAsync(string? phone = null)
    {
        phone ??= Interlocked.Increment(ref _phone).ToString();
        var c = factory.CreateClient();
        var otp = await (await c.PostAsJsonAsync("/api/v1/auth/otp/request", new { destination = phone })).Content.ReadFromJsonAsync<OtpRequestResponse>(Json);
        Assert.NotNull(otp?.DevCode);
        var res = await c.PostAsJsonAsync("/api/v1/auth/otp/verify", new { destination = phone, code = otp!.DevCode });
        res.EnsureSuccessStatusCode();
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (c, auth);
    }

    private static async Task SetupPregnancyAsync(HttpClient c, int weeksAgo = 20)
    {
        var lmp = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)).AddDays(-weeksAgo * 7);
        var res = await c.PutAsJsonAsync("/api/v1/pregnancy", new { lmp, datingMethod = "lmp", diet = "eggetarian", foodAllergies = new[] { "peanut" } }, Json);
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Protected_endpoints_require_login()
    {
        var res = await factory.CreateClient().GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Wrong_otp_is_rejected()
    {
        var c = factory.CreateClient();
        await c.PostAsJsonAsync("/api/v1/auth/otp/request", new { destination = "6000000001" });
        var res = await c.PostAsJsonAsync("/api/v1/auth/otp/verify", new { destination = "6000000001", code = "000000x" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task New_user_can_onboard_and_see_dashboard()
    {
        var (c, auth) = await SignInAsync();
        Assert.True(auth.IsNewUser);
        Assert.False(auth.User.HasPregnancy);
        (await c.PutAsJsonAsync("/api/v1/me", new { name = "Lakshmi" })).EnsureSuccessStatusCode();
        await SetupPregnancyAsync(c, 20);

        var dash = (await c.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard", Json))!;
        Assert.Equal("Lakshmi", dash.User.Name);
        Assert.Equal(20, dash.Pregnancy!.Gestation.Weeks);
        Assert.Equal("banana", dash.Week!.SizeName);
        Assert.Equal(5, dash.Meals.Count);
        Assert.All(dash.Meals, m => Assert.Equal("egg", m.Diet));
        Assert.Equal(0, dash.Water.Glasses);
    }

    [Fact]
    public async Task Implausible_due_date_is_rejected()
    {
        var (c, _) = await SignInAsync();
        var res = await c.PutAsJsonAsync("/api/v1/pregnancy", new { lmp = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3), datingMethod = "lmp" }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task Medicine_doses_can_be_logged()
    {
        var (c, _) = await SignInAsync();
        await SetupPregnancyAsync(c);
        var med = await (await c.PostAsJsonAsync("/api/v1/medications", new { name = "Iron", dosage = "1 tablet", reminderTimes = new[] { "08:00", "20:00" } }, Json))
            .Content.ReadFromJsonAsync<MedicationDto>(Json);
        Assert.Equal(2, med!.ReminderTimes.Count);

        (await c.PostAsJsonAsync($"/api/v1/medications/{med.Id}/log", new { time = "08:00", status = "taken" }, Json)).EnsureSuccessStatusCode();
        var doses = (await c.GetFromJsonAsync<List<DoseDto>>("/api/v1/medications/doses", Json))!;
        Assert.Equal(2, doses.Count);
        Assert.Equal("taken", doses[0].Status?.ToString().ToLowerInvariant());
        Assert.Null(doses[1].Status);

        var bad = await c.PostAsJsonAsync($"/api/v1/medications/{med.Id}/log", new { time = "09:00", status = "taken" }, Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
    }

    [Fact]
    public async Task Users_cannot_see_each_others_data()
    {
        var (a, _) = await SignInAsync();
        var (b, _) = await SignInAsync();
        var appt = await (await a.PostAsJsonAsync("/api/v1/appointments", new { title = "Scan", date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3), time = "10:00" }, Json))
            .Content.ReadFromJsonAsync<AppointmentDto>(Json);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/appointments/{appt!.Id}")).StatusCode);
        Assert.Empty((await b.GetFromJsonAsync<List<AppointmentDto>>("/api/v1/appointments", Json))!);
    }

    [Fact]
    public async Task High_blood_pressure_returns_advisory_not_diagnosis()
    {
        var (c, _) = await SignInAsync();
        var res = await (await c.PostAsJsonAsync("/api/v1/health/blood-pressure", new { systolic = 150, diastolic = 95 }, Json)).Content.ReadFromJsonAsync<SavedReading<BloodPressureDto>>(Json);
        Assert.Equal("attention", res!.Advisory!.Level);
        Assert.Contains("cannot diagnose", res.Advisory.Message);
    }

    [Fact]
    public async Task Assistant_escalates_emergencies_and_respects_allergies()
    {
        var (c, _) = await SignInAsync();
        await SetupPregnancyAsync(c);
        var emer = await (await c.PostAsJsonAsync("/api/v1/assistant/messages", new { text = "My baby is not moving since morning" })).Content.ReadFromJsonAsync<AskResponse>(Json);
        Assert.True(emer!.Answer.IsEmergency);
        Assert.Contains("108", emer.Answer.Content);

        var allergy = await (await c.PostAsJsonAsync("/api/v1/assistant/messages", new { text = "Can I eat peanut chutney?" })).Content.ReadFromJsonAsync<AskResponse>(Json);
        Assert.Contains("allergy", allergy!.Answer.Content);

        var pine = await (await c.PostAsJsonAsync("/api/v1/assistant/messages", new { text = "Is pineapple safe?" })).Content.ReadFromJsonAsync<AskResponse>(Json);
        Assert.Contains("Pineapple", pine!.Answer.Content);
    }

    [Fact]
    public async Task Mother_controls_family_sharing()
    {
        var (mother, _) = await SignInAsync();
        await SetupPregnancyAsync(mother, 30);
        var partnerPhone = Interlocked.Increment(ref _phone).ToString();
        var invite = await (await mother.PostAsJsonAsync("/api/v1/family", new { name = "Arun", relation = "Husband", phone = partnerPhone, role = "partner" }, Json))
            .Content.ReadFromJsonAsync<FamilyMemberDto>(Json);
        Assert.False(invite!.ShareHealth);

        var (partner, pAuth) = await SignInAsync(partnerPhone);
        Assert.Equal("partner", pAuth.User.Role.ToString().ToLowerInvariant());
        var shared = (await partner.GetFromJsonAsync<List<SharedJourneyDto>>("/api/v1/family/shared-with-me", Json))!;
        Assert.Single(shared);
        Assert.Equal(30, shared[0].Gestation!.Weeks);

        (await mother.PatchAsJsonAsync($"/api/v1/family/{invite.Id}", new { shareBabyUpdates = false, shareAppointments = false })).EnsureSuccessStatusCode();
        shared = (await partner.GetFromJsonAsync<List<SharedJourneyDto>>("/api/v1/family/shared-with-me", Json))!;
        Assert.Null(shared[0].Gestation);
        Assert.Null(shared[0].UpcomingAppointments);
    }

    [Fact]
    public async Task Documents_upload_download_and_delete()
    {
        var (c, _) = await SignInAsync();
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "cbc.pdf");
        form.Add(new StringContent("CBC report"), "title");
        form.Add(new StringContent("Blood"), "category");
        var doc = await (await c.PostAsync("/api/v1/documents", form)).Content.ReadFromJsonAsync<DocumentDto>(Json);
        Assert.Equal("cbc.pdf", doc!.FileName);

        var bytes = await c.GetByteArrayAsync($"/api/v1/documents/{doc.Id}/download");
        Assert.Equal("%PDF-1.4 test", System.Text.Encoding.UTF8.GetString(bytes));
        var explain = await c.GetFromJsonAsync<DocumentExplanation>($"/api/v1/documents/{doc.Id}/explain", Json);
        Assert.Contains(explain!.Terms, t => t.Key == "Hb");

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/v1/documents/{doc.Id}")).StatusCode);
    }

    [Fact]
    public async Task Account_can_be_deleted()
    {
        var (c, auth) = await SignInAsync();
        await SetupPregnancyAsync(c);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/me")).StatusCode);
        var refresh = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_tokens_rotate()
    {
        var (_, auth) = await SignInAsync();
        var c = factory.CreateClient();
        var next = await (await c.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken })).Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.NotEqual(auth.RefreshToken, next!.RefreshToken);
        // The old token can't be reused.
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task Content_bundle_is_public()
    {
        var bundle = await factory.CreateClient().GetFromJsonAsync<ContentBundle>("/api/v1/content/bundle", Json);
        Assert.Equal(37, bundle!.Weeks.Count);
        Assert.Contains(bundle.Snippets, s => s.Kind == "WarningSign");
    }
}

public class RoutingTests(AmmaFactory factory) : IClassFixture<AmmaFactory>
{
    [Fact]
    public async Task Unknown_api_routes_are_404_not_the_spa()
    {
        var c = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/does-not-exist")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/api/v1/me"))).StatusCode);
    }
}

public class PrivateAccessTests
{
    [Fact]
    public async Task Only_allowed_numbers_can_sign_in()
    {
        await using var f = new AmmaFactory().WithWebHostBuilder(b => b.UseSetting("Otp:AllowedDestinations:0", "9800000001"));
        var c = f.CreateClient();
        var ok = await c.PostAsJsonAsync("/api/v1/auth/otp/request", new { destination = "+91 98000 00001" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var blocked = await c.PostAsJsonAsync("/api/v1/auth/otp/request", new { destination = "9800000002" });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
    }
}
