using System.Text.RegularExpressions;
using Amma.Domain.Content;
using Amma.Domain.Pregnancy;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Services;

public record AssistantContext(string Name, int Week, DietPreference Diet, IReadOnlyList<string> Allergies, string Language);
public record AssistantReply(string Html, bool IsEmergency, string Topic);

/// <summary>
/// Safety-first pregnancy assistant.
/// 1. Emergency warning signs are always checked FIRST and short-circuit any other answer.
/// 2. Food questions are answered from the (medically reviewed) food database.
/// 3. Remaining questions use curated educational answers.
/// A generative model can later be plugged in behind <see cref="IAssistantResponder"/>; the safety
/// layer stays in front of it and never lets a conversation delay emergency care.
/// </summary>
public interface IAssistantResponder
{
    Task<AssistantReply> ReplyAsync(string question, AssistantContext ctx, CancellationToken ct);
}

public static partial class EmergencyDetector
{
    // English keywords are matched at the start of a word ("bleed" matches "bleeding", but "fits" does not match "benefits").
    private static readonly string[] English =
    [
        "bleed", "blood loss", "heavy spotting", "severe pain", "severe cramp", "severe abdominal", "headache", "vision", "blur", "seeing spots",
        "fluid leak", "leaking fluid", "leaking water", "water broke", "waters broke", "water broken", "waters broken", "watery discharge", "leak",
        "not moving", "no movement", "less movement", "reduced movement", "fewer movement", "baby stopped moving", "stopped kicking",
        "breathless", "can't breathe", "cannot breathe", "difficulty breathing", "short of breath", "chest pain", "seizure", "fits", "convulsion",
        "faint", "unconscious", "sudden swelling", "swollen face", "fever", "suicid", "kill myself", "harm myself", "end my life",
    ];

    // Tamil keywords are matched as substrings (word boundaries do not apply to Tamil script).
    private static readonly string[] Tamil = ["இரத்தப்போக்கு", "ரத்தப்போக்கு", "அசைவு இல்லை", "மூச்சு விட", "வலிப்பு", "காய்ச்சல்", "தலைவலி", "மயக்கம்"];

    private static readonly Regex EnglishPattern = new(
        @"\b(" + string.Join("|", English.Select(Regex.Escape)) + ")", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsEmergency(string text) =>
        EnglishPattern.IsMatch(text) || Tamil.Any(text.Contains);

    public static bool IsMentalHealthCrisis(string text)
    {
        var s = text.ToLowerInvariant();
        return s.Contains("suicid") || s.Contains("kill myself") || s.Contains("harm myself") || s.Contains("end my life");
    }
}

public partial class RuleBasedResponder(ContentQuery content) : IAssistantResponder
{
    private const string Disclaimer = "<span class=\"disc\">Educational only · Please confirm with your doctor.</span>";

    public async Task<AssistantReply> ReplyAsync(string question, AssistantContext ctx, CancellationToken ct)
    {
        var s = question.ToLowerInvariant();
        bool Has(params string[] k) => k.Any(s.Contains);

        if (EmergencyDetector.IsMentalHealthCrisis(question))
            return new("<b>You are not alone, and help is available right now.</b><br>Please call <b>Tele-MANAS 14416</b> (free, 24×7) or <b>112</b>, or tell someone near you. Your doctor can also help.", true, "crisis");

        if (EmergencyDetector.IsEmergency(question))
            return new("<b>⚠️ This could need urgent care.</b><br>Please don't wait — contact your doctor or go to the nearest hospital now. Call <b>108</b> for an ambulance.", true, "emergency");

        // Allergy check against the user's own profile.
        var allergy = ctx.Allergies.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a) && s.Contains(a.ToLowerInvariant()));
        if (allergy is not null)
            return new($"You've noted an allergy to <b>{Enc(allergy)}</b> in your profile, so please avoid it and ask your doctor about safe alternatives.{Disclaimer}", false, "allergy");

        // Food lookup from the reviewed database.
        if (!Has("iron", "calcium", "protein") || Has("can i eat", "safe", "good", "is it ok"))
        {
            var foods = await content.Visible<FoodItem>().ToListAsync(ct);
            var match = foods
                .Select(f => new { f, score = Score(f, s) })
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .ThenBy(x => x.f.Safety == FoodSafety.Avoid ? 0 : 1) // prefer the cautious entry on ties
                .Select(x => x.f)
                .FirstOrDefault();
            if (match is not null && !(Has("papaya") && !Has("raw", "unripe", "ripe")))
                return new(FoodAnswer(match), false, "food");
        }

        if (Has("papaya"))
            return new("<b>Unripe papaya: avoid.</b> It contains latex, which may trigger contractions.<br><br><b>Fully ripe papaya:</b> many doctors consider small amounts fine, but advice varies — please check with your doctor." + Disclaimer, false, "food");
        if (Has("iron", "anaemia", "anemia", "hemoglobin", "haemoglobin"))
            return new("Iron-rich foods:<ul><li>Greens: murungai keerai, spinach</li><li>Dals, rajma, chana, ragi</li><li>Dates, jaggery</li>" +
                       (ctx.Diet != DietPreference.Vegetarian ? "<li>Eggs" + (ctx.Diet == DietPreference.NonVegetarian ? ", chicken, fish" : "") + "</li>" : "") +
                       "</ul>Add vitamin C (guava, amla, lemon) and keep tea/coffee away from meals. Take iron tablets exactly as prescribed." + Disclaimer, false, "nutrition");
        if (Has("calcium"))
            return new("Calcium sources: curd, milk, paneer, ragi, sesame (small amounts) and green leafy vegetables. Keep a gap between calcium and iron tablets, as your doctor advises." + Disclaimer, false, "nutrition");
        if (Has("nausea", "vomit", "morning sickness", "sick"))
            return new("Nausea is common, especially in early pregnancy, and is linked to hormone changes. These may help:<ul><li>Small, frequent meals</li><li>Dry snacks like biscuits in the morning</li><li>Ginger, lemon water, sipping fluids</li></ul>Contact your doctor if you can't keep fluids down, feel dizzy, or pass very little urine." + Disclaimer, false, "symptom");
        if (Has("afi", "ultrasound", "scan", "efw", "placenta", "terminology", "report", "crl", "bpd", "fhr"))
            return new("Common scan terms:<ul><li><b>CRL</b> – crown-rump length, used for early dating</li><li><b>BPD</b> – width of baby's head</li><li><b>EFW</b> – estimated fetal weight</li><li><b>AFI</b> – amniotic fluid index, the amount of fluid around baby</li><li><b>Placenta position</b> – where the placenta is attached</li><li><b>FHR</b> – fetal heart rate</li></ul>Your doctor will explain what your own values mean for you." + Disclaimer, false, "report");
        if (Has("doctor", "question", "ask"))
            return new($"Questions you could ask at week {ctx.Week}:<ul><li>Is my baby growing well?</li><li>Are my BP and weight okay?</li><li>Which tests or scans are due next?</li><li>Are my supplements right for me?</li><li>Which vaccines do I need?</li><li>When should I call you urgently?</li></ul>" + Disclaimer, false, "doctor");
        if (Has("exercise", "walk", "yoga", "activity"))
            return new("Gentle activity like walking, prenatal yoga and pelvic floor exercises is usually good for you — if your doctor agrees. Stop and call your doctor if you feel dizzy, have chest pain, bleeding, fluid leak or painful contractions." + Disclaimer, false, "activity");
        if (Has("sleep", "insomnia"))
            return new("Sleeping on your side (especially later in pregnancy) with a pillow between your knees can help. Keep a regular bedtime and limit tea/coffee after afternoon." + Disclaimer, false, "lifestyle");
        if (Has("eat", "food", "diet", "week", "meal"))
        {
            var protein = "dal, paneer" + (ctx.Diet != DietPreference.Vegetarian ? ", eggs" : "") + (ctx.Diet == DietPreference.NonVegetarian ? ", fish, chicken" : "");
            return new($"For week {ctx.Week}, focus on:<ul><li>Iron & folate: greens, dal, ragi</li><li>Calcium: curd, milk, ragi</li><li>Protein: {protein}</li><li>Plenty of water and fruit</li></ul>Check today's meal plan on your home screen." + Disclaimer, false, "nutrition");
        }
        return new("Thanks for asking! I can help with food safety, common symptoms, scan terms and questions for your doctor. Could you tell me a bit more? For anything about your own health, your doctor is the best guide." + Disclaimer, false, "general");
    }

    private static int Score(FoodItem f, string q)
    {
        var score = 0;
        foreach (var word in Words().Split(f.Name.ToLowerInvariant()).Where(w => w.Length > 3))
            if (q.Contains(word)) score += 2;
        foreach (var tag in f.Tags)
            if (tag.Length > 2 && q.Contains(tag.ToLowerInvariant())) score += 1;
        if (!string.IsNullOrEmpty(f.NameTa) && q.Contains(f.NameTa)) score += 3;
        // Ripe and unripe papaya have opposite guidance, so pick the one the question is about.
        if (score > 0 && f.Name.Contains("papaya", StringComparison.OrdinalIgnoreCase))
        {
            var unripeQ = q.Contains("raw") || q.Contains("unripe") || q.Contains("green");
            var unripeF = f.Name.Contains("Unripe", StringComparison.OrdinalIgnoreCase);
            score += unripeQ == unripeF ? 2 : -10;
        }
        return score;
    }

    private static string FoodAnswer(FoodItem f)
    {
        var verdict = f.Safety switch
        {
            FoodSafety.Recommended => "is generally <b>recommended</b> in pregnancy",
            FoodSafety.SafeIfPrepared => "is generally <b>safe when prepared properly</b>",
            FoodSafety.Limit => "is best <b>limited</b> during pregnancy",
            FoodSafety.Avoid => "should be <b>avoided</b> during pregnancy",
            _ => "is one to <b>check with your doctor</b> about",
        };
        var html = $"{f.Emoji} <b>{Enc(f.Name)}</b> {verdict}.";
        if (!string.IsNullOrWhiteSpace(f.Benefits)) html += $"<br><br>{Enc(f.Benefits)}";
        if (!string.IsNullOrWhiteSpace(f.Preparation)) html += $"<br><b>How:</b> {Enc(f.Preparation)}";
        html += $"<br><b>Take care:</b> {Enc(f.Precautions)}";
        return html + Disclaimer;
    }

    private static string Enc(string s) => System.Net.WebUtility.HtmlEncode(s);

    [GeneratedRegex(@"[^a-z]+")] private static partial Regex Words();
}
