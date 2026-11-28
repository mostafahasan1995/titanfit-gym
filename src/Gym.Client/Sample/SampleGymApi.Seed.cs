using Gym.Contracts;
using Gym.Domain;

namespace Gym.Client.Sample;

/// <summary>The sample data. Deterministic: the same <see cref="Today"/> gives the same data.</summary>
public sealed partial class SampleGymApi
{
    // Statuses are relative to today: (plan months, days until the end date; negative = expired that many days ago).
    // A null plan means "registered, never paid". Photo = has a placeholder photo.
    private static readonly SampleMember[] SampleMembers =
    [
        new("عمر", "خالد", "الخطيب", "سلمى", 1994, "دمشق", "01020045871", "0944123456", 1, 25, Photo: true),
        new("لينا", "سمير", "حداد", "ريم", 1998, "حمص", "01120078342", "0933558201", 1, 2, Photo: true),
        new("يزن", "محمود", "درويش", "هدى", 2001, "دمشق", "01030091265", "0988410762", 1, -6, Photo: true),
        new("رامي", "فؤاد", "صالح", "منى", 1990, "حلب", "02010034519", "0955902318", 3, 80, Photo: false),
        new("سارة", "أحمد", "العلي", "ليلى", 1996, "اللاذقية", "06020011457", "0944771203", 1, 0, Photo: true, StartsToday: true),
        new("محمد", "علي", "الحلبي", "فاطمة", 1988, "حلب", "02030056214", "0991234567", 1, 0, Photo: true),
        new("نور", "حسن", "الشامي", "سعاد", 1999, "دمشق", "01040087123", "0936665544", 6, 160, Photo: true),
        new("أحمد", "ياسر", "قباني", "نادية", 1985, "دمشق", null, "0947001122", 1, -40, Photo: false),
        new("Maya", "George", "Khoury", "Rita", 1997, "Damascus", "01050023987", "0932445566", 1, 20, Photo: true),
        new("ماهر", "عادل", "السيد", "وفاء", 1987, "دمشق", "01060012345", "0957889900", 12, 300, Photo: true, Trainer: true),
        new("باسل", "نزار", "العمر", "رولا", 1992, "حماة", "05010067891", "0998112233", 3, 45, Photo: true, Trainer: true),
        new("هبة", "مازن", "الأيوبي", "سهى", 2000, "دمشق", "01070045612", "0943334455", 1, 28, Photo: true, ClientOf: 9),
        new("كريم", "وليد", "حمدان", "ديما", 1995, "طرطوس", "07010023456", "0935556677", 1, 3, Photo: false, ClientOf: 9),
        new("جود", "فادي", "عيسى", "رنا", 2003, "دمشق", "01080034567", "0949998877", 3, 55, Photo: true, ClientOf: 9),
        new("طارق", "سليم", "الجابي", "غادة", 1993, "حمص", "03010078912", "0992223344", 1, 9, Photo: false, ClientOf: 10),
        new("ريم", "بشار", "نجار", "هالة", 1997, "حلب", "02040012398", "0934447788", 1, -2, Photo: true, ClientOf: 10),
        new("علي", "حسين", "مراد", "زينب", 1989, "درعا", "12010045678", "0951112299", 6, 100, Photo: true),
        new("دانا", "ربيع", "الحمصي", "لمى", 2002, "حمص", "03020067845", "0946665511", 3, 0, Photo: true, StartsToday: true),
        new("Omar", "Sami", "Haddad", "Lina", 1991, "Aleppo", "02050098761", "0939990011", 1, -15, Photo: false),
        new("فراس", "عماد", "الزعبي", "سمر", 1994, "درعا", null, "0954443322", null, 0, Photo: false),
        new("حلا", "جميل", "الأسعد", "مها", 1998, "السويداء", "13010023417", "0942228899", 3, 70, Photo: true),
        new("خالد", "منير", "البيطار", "عبير", 1986, "دمشق", "01090056734", "0997776655", 1, 1, Photo: true),
        new("يارا", "وسيم", "الصباغ", "نسرين", 2001, "دمشق", "01100078123", "0931237788", 1, 5, Photo: true),
        new("مصطفى", "رياض", "الحسن", "أمل", 1983, "إدلب", "11010034125", "0958884411", 1, -90, Photo: false),
    ];

    private void Seed()
    {
        var random = new Random(20261126);

        plans.AddRange([new(1, 1, 20m, true), new(2, 3, 55m, true), new(3, 6, 100m, true), new(4, 12, 180m, true)]);

        var created = At(Today.AddDays(-400), 9, 0);
        staff.AddRange(
        [
            new(1, "خالد الأحمد", "0944000001", StaffType.Admin, true, "admin", StaffRole.Admin, true, created, At(Today.AddDays(-1), 21, 10)),
            new(2, "سارة منصور", "0944000002", StaffType.Reception, true, "sara", StaffRole.Reception, true, created, At(Today, 7, 2)),
            new(3, "رنا يوسف", "0944000003", StaffType.Reception, true, "rana", StaffRole.Reception, true, created, At(Today.AddDays(-1), 14, 0)),
            new(4, "أحمد قاسم", "0944000004", StaffType.Cashier, true, "ahmad", StaffRole.Cashier, true, created, At(Today, 7, 55)),
            new(5, "هبة ناصر", "0944000005", StaffType.Cashier, true, "hiba", StaffRole.Cashier, true, created, At(Today.AddDays(-1), 15, 0)),
            new(6, "سامر حداد", "0944000006", StaffType.ClubTrainer, true, null, null, false, created, null),
            new(7, "فادي خوري", "0944000007", StaffType.ClubTrainer, true, null, null, false, created, null),
            new(8, "مايا صالح", "0944000008", StaffType.ClubTrainer, true, null, null, false, created, null),
            new(9, "وائل عبدو", "0944000009", StaffType.Reception, false, "wael", StaffRole.Reception, true, created, At(Today.AddDays(-90), 9, 0)),
        ]);
        var admin = staff[0];
        var reception = staff[1];
        SeedRates();

        // Club trainer ratings: the last 3 months (the current month is not rated yet for one trainer).
        string[] notes = ["ممتاز مع المشتركين الجدد", "ملتزم بالمواعيد", "يحتاج متابعة في النظافة", "Great energy in the evening classes", ""];
        for (var back = 0; back < 3; back++)
        {
            var month = Today.AddMonths(-back);
            foreach (var trainer in staff.Where(s => s.Type == StaffType.ClubTrainer))
            {
                if (back == 0 && trainer.Id == 8)
                {
                    continue;
                }

                var note = notes[random.Next(notes.Length)];
                ratings.Add(new RatingDto(
                    NextId(), trainer.Id, trainer.FullName, month.Year, month.Month, random.Next(3, 6), note.Length == 0 ? null : note,
                    admin.FullName, At(month.AddDays(-month.Day + 1), 12, 0)));
            }
        }

        // Members, their payments and the private trainer links.
        for (var i = 0; i < SampleMembers.Length; i++)
        {
            var s = SampleMembers[i];
            var row = new MemberRow
            {
                Id = new Guid(i + 1, 0x7f17, 0x4e3a, [0x9b, 0x1c, 0, 0, 0, 0, 0, (byte)(i + 1)]),
                MemberNo = 231 + i,
                Type = s.Trainer ? MemberType.PrivateTrainer : MemberType.Regular,
                FirstName = s.First,
                FatherName = s.Father,
                LastName = s.Last,
                MotherName = s.Mother,
                BirthDate = new DateOnly(s.BirthYear, 1 + (i * 5 % 12), 1 + (i * 7 % 28)),
                BirthPlace = s.BirthPlace,
                NationalNo = s.NationalNo,
                Phone = s.Phone,
                Notes = i == 6 ? "تفضّل التمرين المسائي" : null,
                HasPhoto = s.Photo,
                QrCode = "TF1-SAMPLE" + (i + 1).ToString("D14", System.Globalization.CultureInfo.InvariantCulture),
                CreatedAt = At(Today.AddDays(-(s.Months ?? 0) * 31 - 20 - i), 10, 15),
            };
            members.Add(row);

            if (s.Months is not { } months)
            {
                continue;
            }

            var plan = plans.First(p => p.Months == months);
            var start = s.StartsToday ? Today : Today.AddDays(s.EndInDays).AddMonths(-months);
            if (i % 3 == 0)
            {
                // An earlier month before the current period, for the payment history.
                var earlier = start.AddMonths(-1).AddDays(-10);
                subscriptions.Add(new SubscriptionDto(
                    NextId(), row.Id, SubscriptionKind.Membership, plans[0].Id, 1, earlier, SubscriptionRules.CalculateEndDate(earlier, 1),
                    plans[0].Price, reception.Id, reception.FullName, At(earlier, 11, 30), Currency.Usd, plans[0].Price));
            }

            // Every third payment in Syrian pounds at that day's rate (when the sample has one).
            var paid = i % 3 == 1 && RateOn(start) is { } rate
                ? new PaymentAmount(Currency.Syp, CurrencyRules.ToSyp(plan.Price, rate, settings.SypRoundingStep), rate)
                : new PaymentAmount(Currency.Usd, plan.Price, RateOn(start));
            subscriptions.Add(new SubscriptionDto(
                NextId(), row.Id, SubscriptionKind.Membership, plan.Id, months, start, SubscriptionRules.CalculateEndDate(start, months),
                plan.Price, reception.Id, reception.FullName, At(start, s.StartsToday ? 9 + i % 8 : 11, 30), paid.Currency, paid.Amount, paid.SypPerUsd));
        }

        foreach (var (s, i) in SampleMembers.Select((s, i) => (s, i)).Where(x => x.s.ClientOf is not null))
        {
            members[i].PrivateTrainerId = members[s.ClientOf!.Value].Id;
        }

        // Private training service: one trainer active, the other one expires tomorrow.
        AddService(members[9], 1, Today.AddDays(18).AddMonths(-1), reception);
        AddService(members[10], 1, Today.AddDays(1).AddMonths(-1), reception);
        AddService(members[10], 1, Today.AddDays(1).AddMonths(-2), reception);

        // Check-ins: today in detail, the 13 days before for the reports.
        var stations = new[] { "Reception 1", "Reception 2" };
        for (var back = 13; back >= 0; back--)
        {
            var day = Today.AddDays(-back);
            var count = back == 0 ? 34 : 18 + random.Next(16);
            for (var n = 0; n < count; n++)
            {
                var hour = PickHour(random);
                var at = At(day, hour, random.Next(60));
                var member = random.Next(30) == 0 ? null : members[random.Next(members.Count)];
                var method = random.Next(6) == 0 ? CheckInMethod.MemberNo : CheckInMethod.QrCode;
                checkIns.Add(NewCheckIn(member, at, stations[random.Next(stations.Length)], method));
            }
        }

        checkIns.Sort((a, b) => a.At.CompareTo(b.At));

        SeedCafeteria(random);
    }

    private void SeedCafeteria(Random random)
    {
        categories.AddRange(
        [
            new(1, "Drinks", "مشروبات", 1, true),
            new(2, "Shakes", "مخفوقات", 2, true),
            new(3, "Snacks", "وجبات خفيفة", 3, true),
            new(4, "Supplements", "مكملات", 4, true),
        ]);

        // (id, English, Arabic, category, barcode, price, min stock, stock at the end, active)
        (int Id, string En, string Ar, int Category, string? Barcode, decimal Price, int Min, int Stock, bool Active)[] items =
        [
            (1, "Water 500 ml", "مياه 500 مل", 1, "6291001000011", 0.25m, 24, 60, true),
            (2, "Water 1.5 L", "مياه 1.5 لتر", 1, "6291001000028", 0.50m, 12, 8, true),
            (3, "Energy drink", "مشروب طاقة", 1, "9002490100070", 1.50m, 12, 30, true),
            (4, "Orange juice", "عصير برتقال", 1, "6291001000042", 1.00m, 10, 14, true),
            (5, "Iced coffee", "قهوة باردة", 1, "6291001000059", 1.25m, 6, 0, true),
            (6, "Chocolate protein shake", "شيك بروتين بالشوكولا", 2, null, 3.00m, 0, 40, true),
            (7, "Banana shake", "شيك موز", 2, null, 2.50m, 0, 25, true),
            (8, "Berry shake", "شيك توت", 2, null, 2.75m, 5, 3, true),
            (9, "Protein bar", "لوح بروتين", 3, "5060495110124", 1.75m, 10, 22, true),
            (10, "Oat cookies", "كوكيز الشوفان", 3, "6291001000103", 0.75m, 10, 6, true),
            (11, "Mixed nuts", "مكسرات مشكلة", 3, "6291001000110", 1.50m, 5, 12, true),
            (12, "Rice cakes", "كعك الأرز", 3, "6291001000127", 0.60m, 5, 18, true),
            (13, "Whey protein 1 kg", "واي بروتين 1 كغ", 4, "5060469988254", 38.00m, 2, 5, true),
            (14, "Creatine 300 g", "كرياتين 300 غ", 4, "5060469988261", 22.00m, 2, 1, true),
            (15, "BCAA 250 g", "أحماض أمينية BCAA 250 غ", 4, "5060469988278", 19.00m, 2, 4, true),
            (16, "Shaker bottle", "مطرة خلط", 4, "6291001000165", 2.00m, 3, 9, true),
            (17, "Chocolate wafer", "ويفر شوكولا", 3, "6291001000172", 0.40m, 0, 0, false),
        ];
        foreach (var item in items)
        {
            products.Add(new ProductRow
            {
                Id = item.Id,
                NameEn = item.En,
                NameAr = item.Ar,
                CategoryId = item.Category,
                Barcode = item.Barcode,
                Price = item.Price,
                MinStock = item.Min,
                IsActive = item.Active,
            });
        }

        // Sales for the last 7 days with one shift per day (today's shift is still open).
        var cashiers = staff.Where(s => s.Role == StaffRole.Cashier && s.IsActive).ToList();
        var sellable = products.Where(p => p.IsActive && p.Id is not 5).ToList();
        var planned = new List<(DateTime At, StaffDto Cashier, ShiftDto Shift, string Station, List<(ProductRow Product, int Quantity)> Lines)>();
        for (var back = 6; back >= 0; back--)
        {
            var day = Today.AddDays(-back);
            var cashier = cashiers[back % cashiers.Count];
            var station = back % 2 == 0 ? "Cafeteria 1" : "Cafeteria 2";
            var shift = OpenShift(cashier, station, 20m, At(day, 8, 0));
            var count = back == 0 ? 14 : 9 + random.Next(9);
            for (var n = 0; n < count; n++)
            {
                var lines = new List<(ProductRow Product, int Quantity)>();
                var lineCount = 1 + random.Next(3);
                for (var l = 0; l < lineCount; l++)
                {
                    var product = sellable[random.Next(sellable.Count)];
                    if (product.CategoryId == 4 && random.Next(4) != 0)
                    {
                        product = sellable[random.Next(8)];
                    }

                    if (lines.All(x => x.Product.Id != product.Id))
                    {
                        lines.Add((product, 1 + random.Next(2)));
                    }
                }

                planned.Add((At(day, 8 + n * 13 / Math.Max(1, count), random.Next(60)), cashier, shift, station, lines));
            }

            if (back > 0)
            {
                var index = shifts.IndexOf(shift);
                shifts[index] = shift with { ClosedAt = At(day, 22, 5) };
            }
        }

        // Stock: one delivery two weeks ago, sized so that the stock left after the sales is the planned stock.
        var sold = planned.SelectMany(p => p.Lines).GroupBy(l => l.Product.Id).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        var delivery = At(Today.AddDays(-14), 9, 30);
        foreach (var item in items)
        {
            var product = products.First(p => p.Id == item.Id);
            var broken = item.Id == 3 ? 1 : 0;
            var quantity = item.Stock + sold.GetValueOrDefault(item.Id) + broken;
            if (quantity > 0)
            {
                AddMovement(product, quantity, StockMovementType.StockIn, null, "توريد أسبوعي", delivery, cashiers[0]);
            }

            if (broken > 0)
            {
                AddMovement(product, -broken, StockMovementType.Adjustment, null, "عبوة تالفة", delivery.AddDays(3), cashiers[0]);
            }
        }

        var saleNo = 0;
        foreach (var sale in planned.OrderBy(p => p.At))
        {
            // Every third sale is paid in Syrian pounds (cash up to the next 5,000), the others in dollars (up to the next $5).
            var total = sale.Lines.Sum(l => l.Product.Price * l.Quantity);
            var rate = RateOn(DateOnly.FromDateTime(sale.At.ToLocalTime()));
            if (saleNo++ % 3 == 2 && rate is { } sypPerUsd)
            {
                var due = new PaymentAmount(Currency.Syp, CurrencyRules.ToSyp(total, sypPerUsd, settings.SypRoundingStep), sypPerUsd);
                var pounds = Math.Ceiling(due.Amount / 5_000m) * 5_000m;
                AddSale(sale.Cashier, shifts.First(s => s.Id == sale.Shift.Id), sale.Station, sale.At, sale.Lines, pounds, due);
            }
            else
            {
                var dollars = Math.Ceiling(total / 5m) * 5m;
                AddSale(sale.Cashier, shifts.First(s => s.Id == sale.Shift.Id), sale.Station, sale.At, sale.Lines, dollars, new PaymentAmount(Currency.Usd, total, rate));
            }
        }

        // The closed shifts counted their cash (one was $2 short).
        for (var i = 0; i < shifts.Count; i++)
        {
            if (shifts[i].ClosedAt is not null)
            {
                var expected = shifts[i].OpeningCash + sales.Where(s => s.ShiftId == shifts[i].Id).Sum(s => s.Total);
                shifts[i] = shifts[i] with { ClosingCash = i == 2 ? expected - 2m : expected };
            }
        }
    }

    private void AddService(MemberRow trainer, int months, DateOnly start, StaffDto by) =>
        subscriptions.Add(new SubscriptionDto(
            NextId(), trainer.Id, SubscriptionKind.PrivateTraining, null, months, start, SubscriptionRules.CalculateEndDate(start, months),
            months * settings.PrivateTrainingMonthlyFee, by.Id, by.FullName, At(start, 12, 0), Currency.Usd, months * settings.PrivateTrainingMonthlyFee));

    /// <summary>Busy in the morning and the evening, like a real gym.</summary>
    private static int PickHour(Random random)
    {
        int[] weights = [0, 0, 0, 0, 0, 0, 2, 5, 6, 4, 3, 2, 2, 2, 2, 3, 5, 8, 10, 9, 6, 3, 1, 0];
        var pick = random.Next(weights.Sum());
        for (var h = 0; h < weights.Length; h++)
        {
            pick -= weights[h];
            if (pick < 0)
            {
                return h;
            }
        }

        return 18;
    }

    private static DateTime At(DateOnly day, int hour, int minute) =>
        day.ToDateTime(new TimeOnly(Math.Clamp(hour, 0, 23), Math.Clamp(minute, 0, 59)), DateTimeKind.Local).ToUniversalTime();

    private sealed record SampleMember(
        string First,
        string Father,
        string Last,
        string Mother,
        int BirthYear,
        string BirthPlace,
        string? NationalNo,
        string Phone,
        int? Months,
        int EndInDays,
        bool Photo,
        bool Trainer = false,
        int? ClientOf = null,
        bool StartsToday = false);
}
