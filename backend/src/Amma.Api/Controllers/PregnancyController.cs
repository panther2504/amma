using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Pregnancy;
using Microsoft.AspNetCore.Mvc;

namespace Amma.Api.Controllers;

[Route("api/v1/pregnancy")]
public class PregnancyController(AuditService audit) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PregnancyDto>> Get(CancellationToken ct)
    {
        var p = await ActivePregnancyAsync(ct);
        return p is null ? NotFound() : p.ToDto(await TodayAsync(ct));
    }

    /// <summary>Creates the active pregnancy profile, or updates it if one exists.</summary>
    [HttpPut]
    public async Task<ActionResult<PregnancyDto>> Save(SavePregnancyRequest req, CancellationToken ct)
    {
        var today = await TodayAsync(ct);
        DateOnly lmp;
        if (req.DatingMethod == DatingMethod.Lmp)
        {
            if (req.Lmp is null) return Problem422("Please enter the first day of your last period.");
            lmp = req.Lmp.Value;
        }
        else
        {
            if (req.Edd is null) return Problem422("Please enter the due date given by your doctor.");
            lmp = PregnancyCalculator.LmpFromEdd(req.Edd.Value);
        }
        if (!PregnancyCalculator.IsPlausibleLmp(lmp, today)) return Problem422("Please check the date. It should fall within a current pregnancy.");

        var p = await ActivePregnancyAsync(ct, track: true);
        var created = p is null;
        p ??= new PregnancyProfile { UserId = UserId };
        p.Lmp = lmp;
        p.Edd = req.DatingMethod == DatingMethod.Lmp ? PregnancyCalculator.EddFromLmp(lmp) : req.Edd!.Value;
        p.DatingMethod = req.DatingMethod;
        p.DoctorConfirmed = req.DoctorConfirmed || req.DatingMethod != DatingMethod.Lmp;
        p.PregnancyType = req.PregnancyType;
        p.Diet = req.Diet;
        p.FoodAllergies = Clean(req.FoodAllergies);
        p.MedicalConditions = Clean(req.MedicalConditions);
        p.BloodGroup = req.BloodGroup; p.RhFactor = req.RhFactor;
        p.DoctorName = req.DoctorName; p.DoctorPhone = req.DoctorPhone;
        p.HospitalName = req.HospitalName; p.HospitalPhone = req.HospitalPhone;
        p.WaterGoalGlasses = req.WaterGoalGlasses;
        if (created) Db.PregnancyProfiles.Add(p);
        audit.Record(UserId, created ? "pregnancy.create" : "pregnancy.update", "PregnancyProfile", p.Id);
        await Db.SaveChangesAsync(ct);
        return p.ToDto(today);
    }

    /// <summary>Marks the pregnancy as delivered (start of postpartum care).</summary>
    [HttpPost("delivered")]
    public async Task<ActionResult<PregnancyDto>> Delivered([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var p = await ActivePregnancyAsync(ct, track: true);
        if (p is null) return NotFound();
        p.Status = PregnancyStatus.Delivered;
        p.DeliveryDate = date ?? await TodayAsync(ct);
        await Db.SaveChangesAsync(ct);
        return p.ToDto(await TodayAsync(ct));
    }

    private static List<string> Clean(List<string>? items) =>
        items?.Select(x => x.Trim()).Where(x => x.Length is > 0 and <= 80).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToList() ?? [];
}
