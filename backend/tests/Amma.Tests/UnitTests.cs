using Amma.Api.Auth;
using Amma.Api.Services;
using Amma.Domain.Pregnancy;
using Amma.Domain.Tracking;

namespace Amma.Tests;

public class PregnancyCalculatorTests
{
    [Fact]
    public void Edd_is_280_days_after_lmp()
    {
        Assert.Equal(new DateOnly(2026, 10, 8), PregnancyCalculator.EddFromLmp(new DateOnly(2026, 1, 1)));
        Assert.Equal(new DateOnly(2026, 1, 1), PregnancyCalculator.LmpFromEdd(new DateOnly(2026, 10, 8)));
    }

    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(13 * 7 + 6, 13, 6, 1)]
    [InlineData(14 * 7, 14, 0, 2)]
    [InlineData(27 * 7 + 6, 27, 6, 2)]
    [InlineData(28 * 7, 28, 0, 3)]
    public void Calculates_weeks_days_and_trimester(int daysSinceLmp, int weeks, int days, int trimester)
    {
        var lmp = new DateOnly(2026, 1, 1);
        var ga = PregnancyCalculator.Calculate(lmp, lmp.AddDays(daysSinceLmp));
        Assert.Equal(weeks, ga.Weeks);
        Assert.Equal(days, ga.Days);
        Assert.Equal(trimester, ga.Trimester);
        Assert.Equal(280 - daysSinceLmp, ga.DaysToGo);
    }

    [Fact]
    public void Progress_and_days_to_go_are_clamped_after_due_date()
    {
        var lmp = new DateOnly(2026, 1, 1);
        var ga = PregnancyCalculator.Calculate(lmp, lmp.AddDays(290));
        Assert.Equal(0, ga.DaysToGo);
        Assert.Equal(100, ga.ProgressPercent);
    }

    [Theory]
    [InlineData(13, false)]
    [InlineData(14, true)]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void Rejects_implausible_dates(int days, bool ok)
    {
        var today = new DateOnly(2026, 11, 27);
        Assert.Equal(ok, PregnancyCalculator.IsPlausibleLmp(today.AddDays(-days), today));
    }
}

public class DestinationTests
{
    [Theory]
    [InlineData("9876543210", "+919876543210")]
    [InlineData("+91 98765 43210", "+919876543210")]
    [InlineData("919876543210", "+919876543210")]
    [InlineData("Priya@Example.com", "priya@example.com")]
    [InlineData("12345", null)]
    [InlineData("5876543210", null)] // Indian mobiles start with 6-9
    [InlineData("not-an-email@", null)]
    public void Normalizes_phone_and_email(string input, string? expected) => Assert.Equal(expected, Destination.Normalize(input));
}

public class SafetyTests
{
    [Theory]
    [InlineData("I have some bleeding since morning")]
    [InlineData("My baby is not moving today")]
    [InlineData("Reduced movements since yesterday")]
    [InlineData("I think my waters broke")]
    [InlineData("severe headache and blurry vision")]
    [InlineData("chest pain while walking")]
    [InlineData("எனக்கு இரத்தப்போக்கு உள்ளது")]
    public void Detects_emergencies(string text) => Assert.True(EmergencyDetector.IsEmergency(text));

    [Theory]
    [InlineData("What are the benefits of ragi?")]
    [InlineData("Can I eat papaya?")]
    [InlineData("What does AFI mean on my scan?")]
    [InlineData("Is television bad for the baby?")]
    public void Does_not_flag_ordinary_questions(string text) => Assert.False(EmergencyDetector.IsEmergency(text));

    [Theory]
    [InlineData(120, 80, null)]
    [InlineData(139, 89, null)]
    [InlineData(140, 85, "attention")]
    [InlineData(130, 90, "attention")]
    [InlineData(160, 100, "urgent")]
    [InlineData(150, 110, "urgent")]
    public void Blood_pressure_advisory(int s, int d, string? level) => Assert.Equal(level, HealthAdvisor.ForBloodPressure(s, d)?.Level);

    [Theory]
    [InlineData(85, SugarContext.Fasting, null)]
    [InlineData(95, SugarContext.Fasting, "attention")]
    [InlineData(130, SugarContext.AfterMeal, null)]
    [InlineData(145, SugarContext.AfterMeal, "attention")]
    [InlineData(55, SugarContext.Random, "urgent")]
    public void Blood_sugar_advisory(int mg, SugarContext ctx, string? level) => Assert.Equal(level, HealthAdvisor.ForBloodSugar(mg, ctx)?.Level);
}

public class ConnectionStringTests
{
    [Fact]
    public void Converts_neon_style_uri()
    {
        var cs = Amma.Infrastructure.ConnectionStrings.ToNpgsql("postgresql://amma_user:p%40ss@ep-cool-name-123.ap-southeast-1.aws.neon.tech/amma?sslmode=require");
        var b = new Npgsql.NpgsqlConnectionStringBuilder(cs);
        Assert.Equal("ep-cool-name-123.ap-southeast-1.aws.neon.tech", b.Host);
        Assert.Equal(5432, b.Port);
        Assert.Equal("amma", b.Database);
        Assert.Equal("amma_user", b.Username);
        Assert.Equal("p@ss", b.Password);
        Assert.Equal(Npgsql.SslMode.Require, b.SslMode);
    }

    [Fact]
    public void Leaves_keyword_strings_alone() =>
        Assert.Equal("Host=localhost;Database=amma", Amma.Infrastructure.ConnectionStrings.ToNpgsql("Host=localhost;Database=amma"));
}
