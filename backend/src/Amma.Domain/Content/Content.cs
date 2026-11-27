using Amma.Domain.Common;

namespace Amma.Domain.Content;

/// <summary>Baby size comparison and approximate measurements for a gestational week.</summary>
public class PregnancyWeek : ReviewableContent
{
    public int Week { get; set; }
    public string Emoji { get; set; } = "";
    public string SizeName { get; set; } = "";
    public string Length { get; set; } = "";
    public string Weight { get; set; } = "";
}

/// <summary>Development and guidance for a range of weeks.</summary>
public class WeeklyGuide : ReviewableContent
{
    public int FromWeek { get; set; }
    public int ToWeek { get; set; }
    public List<string> Baby { get; set; } = [];
    public List<string> Mother { get; set; } = [];
    public List<string> Tips { get; set; } = [];
}

public enum FoodSafety { Recommended = 0, SafeIfPrepared = 1, Limit = 2, Avoid = 3, ConsultDoctor = 4 }

public class FoodItem : ReviewableContent
{
    public string Name { get; set; } = "";
    public string? NameTa { get; set; }
    public string Emoji { get; set; } = "";
    public string Category { get; set; } = "";
    public FoodSafety Safety { get; set; }
    public string? Nutrients { get; set; }
    public string? Benefits { get; set; }
    public string? Preparation { get; set; }
    public string Precautions { get; set; } = "";
    public string? TrimesterNotes { get; set; }
    public List<string> References { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}

public class MealPlanItem : ReviewableContent
{
    public string Diet { get; set; } = "veg"; // veg | egg | nonveg
    public string Region { get; set; } = "south"; // south | north
    public int Order { get; set; }
    public string Slot { get; set; } = "";
    public string Emoji { get; set; } = "";
    public string Description { get; set; } = "";
}

public class Article : ReviewableContent
{
    public string Slug { get; set; } = "";
    public string Category { get; set; } = "";
    public string Emoji { get; set; } = "";
    public string ColorKey { get; set; } = "blush";
    public int ReadMinutes { get; set; }
    public string Title { get; set; } = "";
    public List<string> Paragraphs { get; set; } = [];
    public int? FromWeek { get; set; }
    public int? ToWeek { get; set; }
}

/// <summary>Short educational items: myths, FAQs, warning signs, milestones, checkups, activities, checklists.</summary>
public enum SnippetKind { Myth = 0, Faq = 1, WarningSign = 2, Milestone = 3, CheckupType = 4, Activity = 5, DailyChecklist = 6, HospitalBag = 7 }

public class ContentSnippet : ReviewableContent
{
    public SnippetKind Kind { get; set; }
    /// <summary>Stable key, e.g. used by checklist/hospital bag tracking.</summary>
    public string Key { get; set; } = "";
    public int Order { get; set; }
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public string? Emoji { get; set; }
    public string? Group { get; set; }
    public int? Week { get; set; }
}
