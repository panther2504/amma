using Amma.Domain.Care;
using Amma.Domain.Common;
using Amma.Domain.Content;
using Amma.Domain.Family;
using Amma.Domain.Pregnancy;
using Amma.Domain.Tracking;
using Amma.Domain.Users;
using Amma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Amma.Infrastructure.Seed;

public class DatabaseInitializer(AmmaDbContext db, ILogger<DatabaseInitializer> log)
{
    public const string DemoPhone = "+919999999999";

    public async Task InitializeAsync(bool seedDemoUser, CancellationToken ct = default)
    {
        if (db.Database.IsRelational())
        {
            log.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync(ct);
        }
        else
        {
            log.LogWarning("No ConnectionStrings:Amma configured - using an IN-MEMORY database. Data is lost on restart.");
            await db.Database.EnsureCreatedAsync(ct);
        }

        if (!await db.PregnancyWeeks.AnyAsync(ct))
        {
            SeedContent();
            await db.SaveChangesAsync(ct);
            log.LogInformation("Seeded starter content (status: InReview - requires medical approval before publishing).");
        }

        if (seedDemoUser && !await db.Users.AnyAsync(u => u.Phone == DemoPhone, ct))
        {
            SeedDemoUser();
            await db.SaveChangesAsync(ct);
            log.LogInformation("Seeded demo account {Phone}", DemoPhone);
        }
    }

    // Starter content is ported from the approved UX prototype. It is marked InReview:
    // every item must be reviewed and approved by a qualified medical reviewer before it is Published.
    private static T Pending<T>(T c) where T : ReviewableContent
    {
        c.Status = ContentStatus.InReview;
        return c;
    }

    private void SeedContent()
    {
        (int w, string e, string n, string l, string g)[] weeks =
        [
            (4,"🌱","poppy seed","0.1 cm","< 1 g"),(5,"⚪","sesame seed","0.2 cm","< 1 g"),(6,"🫘","lentil","0.6 cm","< 1 g"),(7,"🫐","blueberry","1 cm","< 1 g"),
            (8,"🍇","grape","1.6 cm","1 g"),(9,"🍒","cherry","2.3 cm","2 g"),(10,"🍓","strawberry","3.1 cm","4 g"),(11,"🍋‍🟩","lime","4.1 cm","7 g"),(12,"🍑","plum","5.4 cm","14 g"),
            (13,"🍋","lemon","7.4 cm","23 g"),(14,"🍑","peach","8.7 cm","43 g"),(15,"🍎","apple","10.1 cm","70 g"),(16,"🥑","avocado","11.6 cm","100 g"),(17,"🍊","pomegranate","13 cm","140 g"),
            (18,"🍠","sweet potato","14.2 cm","190 g"),(19,"🥭","mango","15.3 cm","240 g"),(20,"🍌","banana","25.6 cm","300 g"),(21,"🥕","carrot","26.7 cm","360 g"),(22,"🌽","sweet corn cob","27.8 cm","430 g"),
            (23,"🍊","grapefruit","28.9 cm","500 g"),(24,"🌽","corn","30 cm","600 g"),(25,"🥦","cauliflower","34.6 cm","660 g"),(26,"🥬","lettuce","35.6 cm","760 g"),(27,"🥬","cabbage","36.6 cm","875 g"),
            (28,"🍆","brinjal","37.6 cm","1 kg"),(29,"🎃","butternut squash","38.6 cm","1.15 kg"),(30,"🥒","large cucumber","39.9 cm","1.3 kg"),(31,"🥥","coconut","41.1 cm","1.5 kg"),(32,"🥬","napa cabbage","42.4 cm","1.7 kg"),
            (33,"🍍","pineapple","43.7 cm","1.9 kg"),(34,"🍈","cantaloupe","45 cm","2.1 kg"),(35,"🍈","honeydew melon","46.2 cm","2.4 kg"),(36,"🥭","papaya (ripe)","47.4 cm","2.6 kg"),
            (37,"🍈","winter melon","48.6 cm","2.9 kg"),(38,"🎃","pumpkin","49.8 cm","3.1 kg"),(39,"🍉","watermelon","50.7 cm","3.3 kg"),(40,"🍉","jackfruit","51.2 cm","3.5 kg"),
        ];
        db.PregnancyWeeks.AddRange(weeks.Select(x => Pending(new PregnancyWeek { Week = x.w, Emoji = x.e, SizeName = x.n, Length = x.l, Weight = x.g })));

        db.WeeklyGuides.AddRange(new[]
        {
            Guide(4,7,["The neural tube — which becomes the brain and spinal cord — is forming.","A tiny heart begins to beat and may be seen on an early scan.","Small buds for arms and legs start to appear."],
              ["You may feel tired, have tender breasts and a missed period.","Nausea can start — it can happen any time of day.","Needing to pass urine often is common."],
              ["Take folic acid as advised by your doctor.","Book your first antenatal visit.","Eat small, frequent meals if you feel nauseous."]),
            Guide(8,11,["All major organs have started forming.","Fingers and toes are beginning to separate.","Baby makes small movements you can't feel yet."],
              ["Nausea and food aversions may peak.","Your waist may start to thicken.","Mood swings are common with hormone changes."],
              ["Your doctor may suggest a dating scan.","Sip water through the day to stay hydrated.","Avoid raw or undercooked eggs, meat and fish."]),
            Guide(12,15,["Baby can open and close tiny fingers.","Kidneys begin producing urine.","Facial features become more defined."],
              ["Nausea often eases around now.","Energy levels may start to return.","A small bump may begin to show."],
              ["Ask your doctor about first-trimester screening.","Add iron-rich foods like greens, dal and dates.","Gentle walking is a good habit, if your doctor agrees."]),
            Guide(16,19,["Baby can make sucking movements.","Tiny bones are hardening.","Baby may begin to hear sounds."],
              ["You may feel first flutters of movement between 16–22 weeks.","Some women notice darker patches of skin.","Back aches may begin as your bump grows."],
              ["The anomaly scan is usually done around 18–22 weeks.","Sleep on your side with a pillow for support.","Wear comfortable, low-heeled footwear."]),
            Guide(20,23,["Baby is swallowing and practising breathing movements.","A protective coating called vernix covers the skin.","Sleep and wake cycles are forming."],
              ["You're about halfway there!","Movements become easier to notice.","Mild swelling of the feet can occur."],
              ["Start learning your baby's usual movement pattern.","Keep up calcium — curd, ragi, milk and greens.","Ask your doctor which vaccines are advised in pregnancy."]),
            Guide(24,27,["Lungs are developing branches and surfactant.","Baby responds to your voice and touch.","Eyes may begin to open."],
              ["Heartburn and leg cramps are common.","Practice (Braxton Hicks) tightenings may start.","Stretch marks may appear."],
              ["Glucose screening is commonly done between 24–28 weeks.","Smaller meals can ease heartburn.","Report any reduced movements to your care team promptly."]),
            Guide(28,31,["Baby's brain is growing rapidly.","Baby can blink and has eyelashes.","Baby is storing fat and gaining weight."],
              ["Welcome to the third trimester.","Breathlessness and tiredness may return.","Sleep can become harder."],
              ["Antenatal visits may become more frequent.","Start planning your hospital bag.","Rest with your feet up when you can."]),
            Guide(32,35,["Baby is gaining weight steadily.","Many babies settle head-down by now.","Bones are formed but still soft."],
              ["You may pass urine more often.","Pelvic pressure can increase.","Braxton Hicks may be more frequent."],
              ["Have your hospital bag ready by 35 weeks.","Learn the signs of labour.","Discuss birth preferences with your doctor."]),
            Guide(36,42,["From 37 weeks your baby is considered early term.","Lungs are maturing for life outside.","Baby may move lower into the pelvis."],
              ["Breathing may ease as baby drops.","You may feel more pressure and discomfort.","Watch for labour signs: regular contractions, waters breaking, a show."],
              ["Keep hospital documents ready by the door.","Go to hospital if your waters break or you bleed.","Keep tracking movements — they should not slow down before labour."]),
        });

        db.FoodItems.AddRange(new[]
        {
            Food("Drumstick leaves","முருங்கைக் கீரை","🌿","Vegetables",FoodSafety.Recommended,"Iron · Calcium · Vitamin A","A good plant source of iron, calcium and vitamins.","Wash well and cook thoroughly — as poriyal, in dal or adai.","Eat the leaves as food. Do not take drumstick extracts or supplements without your doctor's advice.", "iron","calcium","drumstick","murungai","moringa"),
            Food("Ragi (finger millet)","கேழ்வரகு","🌾","Grains",FoodSafety.Recommended,"Calcium · Iron · Fibre","Excellent source of calcium; keeps you full for longer.","Ragi dosa, kanji, roti or adai.","Cook well. Pair with a vitamin C food to help iron absorption.","calcium","iron","ragi"),
            Food("Spinach & greens","கீரை வகைகள்","🥬","Vegetables",FoodSafety.Recommended,"Folate · Iron · Fibre","Rich in folate, which supports baby's brain and spine development.","Wash thoroughly in clean water and cook.","Avoid raw, unwashed greens.","folate","iron","keerai"),
            Food("Dal & pulses","பருப்பு வகைகள்","🫘","Pulses and legumes",FoodSafety.Recommended,"Protein · Folate · Iron","Everyday protein for vegetarian mothers.","Sambar, dal, sundal, kootu.","Cook well; soak overnight to reduce gas.","protein","iron","folate"),
            Food("Curd (pasteurised)","தயிர்","🥛","Dairy products",FoodSafety.Recommended,"Calcium · Protein · Probiotics","Supports bones and digestion.","Set from boiled or pasteurised milk.","Avoid curd made from raw milk.","calcium","protein"),
            Food("Coconut water","இளநீர்","🥥","Beverages",FoodSafety.Recommended,"Hydration · Potassium","Refreshing and hydrating.","Fresh, from a clean source.","Drink soon after opening.","hydration"),
            Food("Guava","கொய்யா","🍐","Fruits",FoodSafety.Recommended,"Vitamin C · Fibre","Vitamin C helps your body absorb iron.","Wash well; eat ripe.","Peel or wash carefully.","vitamin c"),
            Food("Dates","பேரீச்சை","🌰","Fruits",FoodSafety.Recommended,"Iron · Fibre · Energy","Quick energy and some iron.","2–3 as a snack.","High in natural sugar — check with your doctor if you have high blood sugar.","iron"),
            Food("Eggs (fully cooked)","முட்டை","🥚","Meat and seafood",FoodSafety.SafeIfPrepared,"Protein · Choline","High-quality protein for baby's growth.","Cook until white and yolk are firm.","Avoid raw or half-boiled eggs.","protein","egg"),
            Food("Chicken (well cooked)","கோழி","🍗","Meat and seafood",FoodSafety.SafeIfPrepared,"Protein · Vitamin B12","Good protein and B vitamins.","Cook until no pink remains.","Avoid street-side undercooked meat.","protein","nonveg"),
            Food("Low-mercury fish","மத்தி / சால்மன்","🐟","Meat and seafood",FoodSafety.SafeIfPrepared,"Omega-3 · Protein","Omega-3 supports baby's brain and eyes.","Choose fish like sardine; cook fully.","Avoid raw fish. Ask your doctor how often to eat fish.","protein","nonveg","fish"),
            Food("Paneer","பனீர்","🧀","Dairy products",FoodSafety.SafeIfPrepared,"Protein · Calcium","Protein-rich vegetarian option.","Made from pasteurised milk; cook before eating.","Avoid paneer of unknown source.","protein","calcium"),
            Food("Ripe papaya","பழுத்த பப்பாளி","🧡","Fruits",FoodSafety.ConsultDoctor,"Vitamin C · Fibre","Fully ripe papaya in small amounts is considered fine by many doctors.","Only fully ripe fruit.","Advice varies — please check with your doctor. Avoid unripe or semi-ripe papaya.","papaya"),
            Food("Pineapple","அன்னாசி","🍍","Fruits",FoodSafety.Limit,"Vitamin C · Manganese","Normal food portions are generally considered safe.","Fresh, in moderate portions.","Avoid very large amounts. Ask your doctor if unsure."),
            Food("Tea & coffee","டீ / காபி","☕","Beverages",FoodSafety.Limit,"Caffeine",null,"Have weaker cups, away from iron tablets.","A commonly advised limit is 200 mg caffeine a day (about 2 mugs of instant coffee).","caffeine"),
            Food("Sesame seeds","எள்","🌰","Nuts and seeds",FoodSafety.Limit,"Calcium · Healthy fats","Small amounts used in cooking are generally fine.","As part of chutney or laddu, occasionally.","Ask your doctor before eating large amounts."),
            Food("Street pani puri","பானி பூரி","🥙","Snacks",FoodSafety.Limit,null,null,"Prefer home-made with boiled water.","Unclean water can cause infections like typhoid or hepatitis."),
            Food("Unripe / raw papaya","பச்சைப் பப்பாளி","🟢","Fruits",FoodSafety.Avoid,null,null,null,"Contains latex, which may trigger contractions. Avoid during pregnancy.","papaya"),
            Food("Unpasteurised milk","காய்ச்சாத பால்","🐄","Dairy products",FoodSafety.Avoid,null,null,"Always boil milk.","May carry Listeria and other harmful bacteria."),
            Food("High-mercury fish","சுறா மீன்","🦈","Meat and seafood",FoodSafety.Avoid,null,null,null,"Shark and swordfish are high in mercury, which can harm baby's nervous system.","fish"),
            Food("Alcohol","மது","🚫","Beverages",FoodSafety.Avoid,null,null,null,"No amount of alcohol is known to be safe in pregnancy."),
        });

        (string diet, string e, string slot, string d)[] meals =
        [
            ("veg","🥞","Breakfast","Ragi dosa, sambar & coconut chutney"),("veg","🍌","Mid-morning","Guava or a banana"),("veg","🍛","Lunch","Rice, dal, keerai poriyal & curd"),("veg","🥥","Evening","Roasted chana & coconut water"),("veg","🫓","Dinner","Chapati, paneer curry & washed salad"),
            ("egg","🍳","Breakfast","Idli, sambar & a boiled egg"),("egg","🍌","Mid-morning","Guava or a banana"),("egg","🍛","Lunch","Rice, dal, keerai poriyal & curd"),("egg","🥜","Evening","Sundal & buttermilk"),("egg","🫓","Dinner","Chapati, egg curry & vegetables"),
            ("nonveg","🍳","Breakfast","Pongal, chutney & a boiled egg"),("nonveg","🍐","Mid-morning","Guava or pomegranate"),("nonveg","🍛","Lunch","Rice, sardine curry, keerai & curd"),("nonveg","🥥","Evening","Roasted peanuts & coconut water"),("nonveg","🍗","Dinner","Chapati, chicken curry & vegetables"),
        ];
        db.MealPlanItems.AddRange(meals.Select((m, i) => Pending(new MealPlanItem { Diet = m.diet, Region = "south", Order = i % 5, Emoji = m.e, Slot = m.slot, Description = m.d })));

        db.Articles.AddRange(new[]
        {
            Art("second-trimester","🌸","Pregnancy basics","blush",4,"Your second trimester: what to expect",14,27,
              "The second trimester (weeks 14–27) is often the most comfortable stretch of pregnancy. Nausea usually eases, energy returns, and you may feel your baby move for the first time.",
              "This is a good time to build healthy routines — regular meals, gentle activity approved by your doctor, and enough rest.",
              "Keep all your antenatal appointments. Your doctor will check your blood pressure, weight and baby's growth, and may arrange the anomaly scan and glucose screening."),
            Art("iron-indian-foods","🥬","Nutrition","sage",5,"Iron in pregnancy: Indian foods that help",null,null,
              "Your blood volume increases a lot during pregnancy, so your need for iron goes up. Low iron (anaemia) is common in India.",
              "Iron-rich foods include greens like murungai keerai and spinach, dals, rajma, chana, ragi, jaggery, dates, and — for non-vegetarians — eggs, chicken and fish.",
              "Vitamin C foods like guava, amla, lemon and oranges help absorption. Tea and coffee reduce it, so keep them away from meals and iron tablets.",
              "Take iron tablets exactly as your doctor prescribes."),
            Art("antenatal-checkups","🩺","Medical checkups","lav",4,"Your antenatal checkups, explained",null,null,
              "Antenatal visits help catch problems early. Typical checks include blood pressure, weight, urine tests, blood tests (haemoglobin, blood group, Rh factor, sugar) and scans.",
              "Common scans include an early dating scan, a first-trimester screening scan and an anomaly scan around 18–22 weeks. Growth scans are done when needed.",
              "Your schedule depends on your health and your doctor's advice — always follow what your care team recommends."),
            Art("baby-movements","👣","Baby development","peach",3,"Getting to know your baby's movements",16,40,
              "Most mothers feel movements between 16 and 24 weeks. Over time, you'll notice your baby's own pattern.",
              "There is no single \"right\" number of kicks — every baby is different. What matters is a change from your baby's usual pattern.",
              "If movements slow down, change, or stop, contact your maternity care team immediately. Do not wait until the next day."),
            Art("mental-wellness","🌿","Mental wellness","sky",4,"Feeling low? Caring for your mind",null,null,
              "Mood changes are common in pregnancy, but sadness or worry that lasts for two weeks or more deserves attention.",
              "Talk to someone you trust and tell your doctor how you feel — support and treatment are available.",
              "If you ever feel unable to cope, please reach out to your doctor or call Tele-MANAS on 14416 (free, 24×7)."),
            Art("labour-signs","🏥","Labour & delivery","red",5,"Signs that labour is starting",32,42,
              "Signs of labour include regular, strengthening contractions, a \"show\" (mucus plug), lower back pain, and your waters breaking.",
              "Call your hospital if your waters break, contractions become regular, or you have bleeding, reduced movements or severe pain.",
              "Keep your hospital bag, documents and transport ready from around 35 weeks."),
        });

        var order = 0;
        Snip(SnippetKind.Myth, "Eating any papaya causes miscarriage.", "Unripe papaya should be avoided. Fully ripe papaya in moderation is considered fine by many doctors — ask yours.", ref order);
        Snip(SnippetKind.Myth, "You must eat for two.", "Extra energy needs are modest — roughly 350 extra kcal a day in the 2nd and 3rd trimesters. Quality matters more than quantity.", ref order);
        Snip(SnippetKind.Myth, "Ghee makes delivery easier.", "There's no evidence for this. Small amounts are fine as part of a balanced diet.", ref order);
        Snip(SnippetKind.Myth, "Exercise is unsafe in pregnancy.", "Gentle, regular activity is usually good for you — unless your doctor advises otherwise.", ref order);
        Snip(SnippetKind.Myth, "Saffron or milk makes the baby fair.", "A baby's skin colour is decided by genes, not by food.", ref order);

        order = 0;
        Snip(SnippetKind.Faq, "How much weight should I gain?", "It depends on your weight before pregnancy. Your doctor will guide you on a healthy range and check your weight at each visit.", ref order);
        Snip(SnippetKind.Faq, "Can I sleep on my back?", "From the third trimester, sleeping on your side is commonly advised. Use pillows to stay comfortable.", ref order);
        Snip(SnippetKind.Faq, "Is travel safe during pregnancy?", "Many women travel safely in the second trimester. Check with your doctor before long trips and carry your medical records.", ref order);
        Snip(SnippetKind.Faq, "When should I call my doctor?", "Any bleeding, fluid leak, severe pain, severe headache, vision changes, or reduced baby movements — call immediately.", ref order);

        order = 0;
        foreach (var (e, t) in new[] { ("🩸","Vaginal bleeding"),("⚡","Severe abdominal pain"),("🤕","Severe headache"),("👁️","Blurred vision or seeing spots"),("👶","Reduced or no baby movements"),("💧","Leaking fluid from the vagina"),("😮‍💨","Difficulty breathing"),("❤️","Chest pain"),("🌀","Fits / seizures"),("🦶","Sudden swelling of face, hands or feet"),("🌡️","High fever") })
            Snip(SnippetKind.WarningSign, t, null, ref order, emoji: e);

        order = 0;
        foreach (var (w, t, b) in new[] { (12,"End of first trimester","Risk of many early complications drops"),(18,"Anomaly scan window","Usually between 18–22 weeks"),(24,"Glucose screening window","Commonly between 24–28 weeks"),(28,"Third trimester begins","Visits may become more frequent"),(35,"Hospital bag ready","Keep documents and essentials packed"),(37,"Early term","Baby is getting ready to meet you"),(40,"Due date","Only a few babies arrive exactly on their due date") })
            Snip(SnippetKind.Milestone, t, b, ref order, week: w);

        order = 0;
        foreach (var (k, t, b, w) in new[] { ("dating-scan","Dating scan","Early pregnancy",8),("nt-scan","First trimester screening","Around 11–14 weeks",11),("anomaly-scan","Anomaly scan","Around 18–22 weeks",18),("glucose","Glucose screening","Around 24–28 weeks",24),("third-tri","Third trimester assessments","From 28 weeks",28) })
            Snip(SnippetKind.CheckupType, t, b, ref order, key: k, week: w);

        order = 0;
        foreach (var (e, t, b, c) in new[] { ("🚶‍♀️","Walking","20–30 min at an easy pace where you can still talk.","sky"),("🧘‍♀️","Prenatal yoga","Gentle stretches with a trained prenatal instructor.","lav"),("🌸","Pelvic floor exercises","Squeeze and hold for a few seconds, several times a day.","blush"),("🐈","Cat–cow stretch","Eases back aches. Move slowly with your breath.","peach"),("😴","Rest & side sleeping","Rest with feet up; sleep on your side in later pregnancy.","sage") })
            Snip(SnippetKind.Activity, t, b, ref order, emoji: e, group: c);

        order = 0;
        foreach (var (k, t) in new[] { ("supplements","Take my prescribed supplements"),("water","Drink 10 glasses of water"),("walk","30-minute gentle walk (if advised)"),("movements","Notice baby's movements"),("fruit-greens","Eat one fruit and greens") })
            Snip(SnippetKind.DailyChecklist, t, null, ref order, key: k);

        order = 0;
        var bag = new Dictionary<string, string[]>
        {
            ["Mother"] = ["Loose nightgowns (2–3)", "Maternity pads", "Nursing bras", "Toiletries & towel", "Slippers", "Phone charger"],
            ["Baby"] = ["Newborn clothes (4–5 sets)", "Diapers", "Soft cotton blankets", "Mittens & caps", "Baby wipes", "Going-home outfit"],
            ["Documents"] = ["Aadhaar / ID proof", "Antenatal records & scans", "Insurance card", "Doctor's prescriptions", "Blood group report"],
        };
        foreach (var (group, items) in bag)
            foreach (var item in items)
                Snip(SnippetKind.HospitalBag, item, null, ref order, key: $"{group.ToLowerInvariant()}-{order}", group: group);
    }

    private static WeeklyGuide Guide(int from, int to, List<string> baby, List<string> mother, List<string> tips) =>
        Pending(new WeeklyGuide { FromWeek = from, ToWeek = to, Baby = baby, Mother = mother, Tips = tips });

    private static FoodItem Food(string n, string ta, string e, string cat, FoodSafety s, string? nut, string? ben, string? prep, string care, params string[] tags) =>
        Pending(new FoodItem { Name = n, NameTa = ta, Emoji = e, Category = cat, Safety = s, Nutrients = nut, Benefits = ben, Preparation = prep, Precautions = care, Tags = [.. tags] });

    private static Article Art(string slug, string e, string cat, string color, int min, string title, int? from, int? to, params string[] body) =>
        Pending(new Article { Slug = slug, Emoji = e, Category = cat, ColorKey = color, ReadMinutes = min, Title = title, FromWeek = from, ToWeek = to, Paragraphs = [.. body] });

    private void Snip(SnippetKind kind, string title, string? body, ref int order, string? key = null, string? emoji = null, string? group = null, int? week = null)
    {
        db.ContentSnippets.Add(Pending(new ContentSnippet
        {
            Kind = kind, Title = title, Body = body, Order = order, Key = key ?? $"{kind.ToString().ToLowerInvariant()}-{order}",
            Emoji = emoji, Group = group, Week = week,
        }));
        order++;
    }

    /// <summary>A demo mother account (Development only) mirroring the prototype's sample data.</summary>
    private void SeedDemoUser()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var user = new User { Name = "Priya", Phone = DemoPhone, Language = "en" };
        db.Users.Add(user);
        var lmp = today.AddDays(-(24 * 7 + 3));
        db.PregnancyProfiles.Add(new PregnancyProfile
        {
            UserId = user.Id, Lmp = lmp, Edd = PregnancyCalculator.EddFromLmp(lmp), Diet = DietPreference.Vegetarian,
            DoctorName = "Dr. Meena Krishnan", HospitalName = "Lotus Maternity Centre",
        });
        foreach (var t in Enum.GetValues<ConsentType>().Where(c => c != ConsentType.LocationSharing))
            db.UserConsents.Add(new UserConsent { UserId = user.Id, Type = t, Granted = true });

        var folic = new Medication { UserId = user.Id, Name = "Folic acid", Dosage = "1 tablet", Instructions = "After breakfast", Category = "FolicAcid", ReminderTimes = [new(8, 0)], StartDate = lmp, ColorKey = "sage" };
        db.Medications.AddRange(folic,
            new Medication { UserId = user.Id, Name = "Iron + Folic acid", Dosage = "1 tablet", Instructions = "After lunch · avoid tea/milk for 1 hour", Category = "Iron", ReminderTimes = [new(14, 0)], StartDate = lmp, ColorKey = "red" },
            new Medication { UserId = user.Id, Name = "Calcium + Vitamin D3", Dosage = "1 tablet", Instructions = "After dinner · keep a gap from iron", Category = "Calcium", ReminderTimes = [new(20, 30)], StartDate = lmp, ColorKey = "sky" });
        db.MedicationLogs.Add(new MedicationLog { UserId = user.Id, MedicationId = folic.Id, Date = today, ScheduledTime = new(8, 0), Status = MedicationLogStatus.Taken });

        db.Appointments.AddRange(
            new Appointment { UserId = user.Id, Title = "Antenatal check-up", DoctorName = "Dr. Meena Krishnan", Location = "Lotus Maternity Centre", Date = today.AddDays(4), Time = new(10, 30), Purpose = "BP, weight, baby growth & heartbeat" },
            new Appointment { UserId = user.Id, Title = "Glucose screening (OGTT)", DoctorName = "Lab · Lotus Maternity", Location = "Lotus Maternity Centre", Date = today.AddDays(16), Time = new(8, 0), Purpose = "Ask your clinic if fasting is needed", Category = CheckupCategory.GlucoseScreening },
            new Appointment { UserId = user.Id, Title = "Anomaly scan", DoctorName = "Dr. Meena Krishnan", Location = "Lotus Scan Centre", Date = today.AddDays(-27), Time = new(11, 0), Purpose = "Detailed scan of baby's anatomy", Category = CheckupCategory.AnomalyScan, Status = AppointmentStatus.Completed },
            new Appointment { UserId = user.Id, Title = "Blood & urine tests", DoctorName = "Lab · Lotus Maternity", Location = "Lotus Maternity Centre", Date = today.AddDays(-45), Time = new(9, 0), Purpose = "CBC, urine routine", Category = CheckupCategory.BloodTest, Status = AppointmentStatus.Completed });

        foreach (var (w, kg) in new[] { (12, 54.2m), (14, 54.8m), (16, 55.6m), (18, 56.4m), (20, 57.3m), (22, 58.2m), (24, 59.0m) })
            db.WeightLogs.Add(new WeightLog { UserId = user.Id, GestationalWeek = w, Kg = kg, Date = lmp.AddDays(w * 7) });
        foreach (var (d, s, di) in new[] { (-14, 110, 72), (-7, 112, 74), (-1, 108, 70) })
            db.BloodPressureLogs.Add(new BloodPressureLog { UserId = user.Id, MeasuredAt = DateTimeOffset.UtcNow.AddDays(d), Systolic = s, Diastolic = di });
        foreach (var (d, n, m) in new[] { (-1, 10, 22), (-2, 10, 31), (-3, 10, 26) })
        {
            var start = DateTimeOffset.UtcNow.AddDays(d).AddHours(-2);
            db.KickSessions.Add(new KickSession { UserId = user.Id, StartedAt = start, EndedAt = start.AddMinutes(m), Movements = n });
        }
        db.WaterLogs.Add(new WaterLog { UserId = user.Id, Date = today, Glasses = 5 });
        db.DailyLogs.Add(new DailyLog { UserId = user.Id, Date = today, Mood = 3, SleepHours = 7.5m, Symptoms = ["Back pain"], ChecklistDone = ["supplements"] });
        db.FamilyMembers.AddRange(
            new FamilyMember { UserId = user.Id, Name = "Arun", Relation = "Husband", Role = FamilyRole.Partner, Status = FamilyMemberStatus.Active, InviteCode = "DEMO-ARUN", ShareMedicines = true },
            new FamilyMember { UserId = user.Id, Name = "Kamala", Relation = "Mother", Role = FamilyRole.FamilyViewer, Status = FamilyMemberStatus.Active, InviteCode = "DEMO-KAMALA" });
        db.EmergencyContacts.AddRange(
            new EmergencyContact { UserId = user.Id, Name = "Arun", Relation = "Husband", Phone = "+910000000001", IsPrimary = true },
            new EmergencyContact { UserId = user.Id, Name = "Dr. Meena Krishnan", Relation = "Doctor", Phone = "+910000000000" });
    }
}
