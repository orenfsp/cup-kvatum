using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Otklik.Application.Security;
using Otklik.Domain.Appeals;

namespace Otklik.Infrastructure.Persistence;

public sealed class OperatorDemoDataSeeder(
    OtklikDbContext database,
    ITrackNumberService trackNumbers,
    IDataProtectionProvider dataProtection,
    ILogger<OperatorDemoDataSeeder> logger)
{
    private const string RecoveryPurpose = "AppealTrackRecovery.v1";
    private static readonly Guid OperatorUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid PrimaryExpertUserId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid MediatorExpertUserId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid[] ExpertUserIds = [PrimaryExpertUserId, MediatorExpertUserId];

    // Stable identities and track numbers make every clean demo environment predictable.
    // Ages remain relative to startup so the queues and crisis waiting timer look realistic.
    private static readonly DemoAppeal[] DemoAppeals =
    [
        Demo(1, "ОТК-TRST-2345", ApplicantType.Student, SubmissionPath.FreeText, null,
            AppealStatus.New, AppealPriority.Standard, 6, null,
            "Несколько ребят постоянно обзывают меня в классе и прячут мои вещи.", "В школе", "Почти каждый день"),
        Demo(2, "ОТК-CARE-6789", ApplicantType.Parent, SubmissionPath.Category, AppealCategory.ConflictId,
            AppealStatus.New, AppealPriority.Low, 3, null,
            "Не получается спокойно обсудить школьную ситуацию с другим родителем.", "В школе", "Несколько раз"),
        Demo(3, "ОТК-SAFE-JKMP", ApplicantType.Teacher, SubmissionPath.Category, AppealCategory.PressureId,
            AppealStatus.New, AppealPriority.Standard, 0.75, null,
            "На ученика оказывают постоянное давление, нужна консультация специалиста.", "В классе", "Регулярно"),
        Demo(4, "ОТК-TAGG-2345", ApplicantType.Parent, SubmissionPath.Category, AppealCategory.ConflictId,
            AppealStatus.Triaged, AppealPriority.Standard, 5, null,
            "Конфликт между детьми продолжается после разговора с классным руководителем.", "В школе", "Каждую неделю"),
        Demo(5, "ОТК-ASGN-3456", ApplicantType.Student, SubmissionPath.FreeText, AppealCategory.BullyingId,
            AppealStatus.Assigned, AppealPriority.Standard, 8, PrimaryExpertUserId,
            "Одноклассники исключают меня из общих дел и пишут неприятные сообщения.", "В классе и в чате", "Несколько раз в неделю"),
        Demo(6, "ОТК-WKRK-4567", ApplicantType.Parent, SubmissionPath.Category, AppealCategory.PressureId,
            AppealStatus.InProgress, AppealPriority.Standard, 12, PrimaryExpertUserId,
            "Ребенок боится отвечать у доски после постоянных насмешек.", "На уроках", "Почти каждый день"),
        Demo(7, "ОТК-ASKD-5678", ApplicantType.Teacher, SubmissionPath.Category, AppealCategory.ConflictId,
            AppealStatus.NeedsClarification, AppealPriority.Low, 18, MediatorExpertUserId,
            "Нужна помощь в спокойном разговоре с двумя участниками затянувшегося конфликта.", "В школе", "Около месяца"),
        Demo(8, "ОТК-RSPN-6789", ApplicantType.Student, SubmissionPath.FreeText, AppealCategory.BullyingId,
            AppealStatus.RecommendationReady, AppealPriority.Standard, 24, PrimaryExpertUserId,
            "Меня дразнят из-за внешности, хочу понять, как безопасно попросить помощи.", "В школе", "Каждый день"),
        Demo(9, "ОТК-RTRN-789A", ApplicantType.Parent, SubmissionPath.Category, AppealCategory.PressureId,
            AppealStatus.Returned, AppealPriority.Standard, 30, null,
            "Предыдущий совет не помог снизить давление на ребенка.", "В школе", "Несколько недель",
            returnCount: 1),
        Demo(10, "ОТК-FNSH-89AB", ApplicantType.Teacher, SubmissionPath.Category, AppealCategory.ConflictId,
            AppealStatus.Closed, AppealPriority.Low, 48, PrimaryExpertUserId,
            "Удалось договориться о безопасном порядке разговора между участниками.", "В школе", "Ситуация завершена"),
        Demo(11, "ОТК-RJCT-9ABC", ApplicantType.Parent, SubmissionPath.FreeText, null,
            AppealStatus.Rejected, AppealPriority.Low, 2, null,
            "Повторяющееся рекламное сообщение без запроса о помощи.", "Не указано", "Повторно"),
        Demo(12, "ОТК-RUSH-DEFG", ApplicantType.Student, SubmissionPath.FreeText, null,
            AppealStatus.New, AppealPriority.Standard, 0.35, null,
            "Мне угрожают прямо сейчас, я боюсь возвращаться один и мне нужна помощь.", "Рядом со школой", "Сейчас",
            crisisFlag: true)
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var groupId in new[] { ExpertGroup.SupportId, ExpertGroup.MediationId })
        {
            foreach (var expertUserId in ExpertUserIds)
            {
                var membershipExists = await database.ExpertGroupMemberships.AnyAsync(
                    item => item.ExpertGroupId == groupId && item.ExpertUserId == expertUserId,
                    cancellationToken);
                if (!membershipExists)
                {
                    database.ExpertGroupMemberships.Add(new ExpertGroupMembership
                    {
                        ExpertGroupId = groupId,
                        ExpertUserId = expertUserId
                    });
                }
            }
        }

        var protector = dataProtection.CreateProtector(RecoveryPurpose);
        var seedStartedAt = DateTimeOffset.UtcNow;
        var createdCount = 0;
        var createdAppeals = new List<Appeal>();
        foreach (var demo in DemoAppeals)
        {
            if (await database.Appeals.AnyAsync(
                    appeal => appeal.ClientRequestId == demo.ClientRequestId,
                    cancellationToken))
            {
                continue;
            }

            var createdAt = seedStartedAt.Subtract(demo.Age);
            var appeal = CreateAppeal(demo, createdAt, protector);
            database.Appeals.Add(appeal);
            createdAppeals.Add(appeal);
            createdCount++;
        }

        await database.SaveChangesAsync(cancellationToken);
        foreach (var appeal in createdAppeals)
        {
            appeal.Thread.CurrentCycleId = appeal.Id;
        }
        if (createdAppeals.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation(
            "Development demo seed is ready; created {AppealCount} appeals across all workflow states",
            createdCount);
    }

    private Appeal CreateAppeal(DemoAppeal demo, DateTimeOffset createdAt, IDataProtector protector)
    {
        DateTimeOffset? assignedAt = demo.AssignedExpertId is null ? null : createdAt.AddMinutes(35);
        var appeal = new Appeal
        {
            Id = demo.Id,
            ThreadId = demo.Id,
            Sequence = 1,
            ClientRequestId = demo.ClientRequestId,
            ApplicantType = demo.ApplicantType,
            SubmissionPath = demo.SubmissionPath,
            CategoryId = demo.CategoryId,
            Narrative = demo.Narrative,
            Status = demo.Status,
            Priority = demo.Priority,
            CrisisFlag = demo.CrisisFlag,
            CrisisDetectedAt = demo.CrisisFlag ? createdAt.AddMinutes(1) : null,
            AssignedExpertId = demo.AssignedExpertId,
            AssignedAt = assignedAt,
            RejectionReason = demo.Status == AppealStatus.Rejected ? AppealRejectionReason.Spam : null,
            RejectionInternalReason = demo.Status == AppealStatus.Rejected ? "Демонстрационная запись: повторяющийся спам." : null,
            PublicResolution = demo.Status == AppealStatus.Closed ? "Обращение закрыто после подтверждения заявителя." : null,
            CompletedAt = demo.Status is AppealStatus.Closed or AppealStatus.Rejected ? createdAt.AddHours(2) : null,
            Version = Math.Max(1, StatusPath(demo.Status).Length),
            ReturnCount = demo.ReturnCount,
            ReturnedAt = demo.Status == AppealStatus.Returned ? createdAt.AddHours(2) : null,
            CreatedAt = createdAt,
            Answers =
            [
                new AppealAnswer { Id = ChildId(demo.Number, 1), QuestionCode = "place", Value = demo.Place },
                new AppealAnswer { Id = ChildId(demo.Number, 2), QuestionCode = "frequency", Value = demo.Frequency }
            ]
        };
        appeal.Thread = new AppealThread
        {
            Id = appeal.ThreadId,
            TrackHash = trackNumbers.Hash(demo.TrackNumber),
            TrackRecoveryCiphertext = protector.Protect(demo.TrackNumber),
            Version = 1,
            CreatedAt = createdAt,
            LastActivityAt = appeal.CompletedAt ?? createdAt
        };

        var path = StatusPath(demo.Status);
        for (var index = 0; index < path.Length; index++)
        {
            appeal.StatusHistory.Add(new AppealStatusChange
            {
                Id = ChildId(demo.Number, 100 + index),
                Status = path[index],
                ChangedAt = createdAt.AddMinutes(index * 20),
                Source = index == 0 ? "Applicant" : StatusSource(path[index])
            });
        }

        if (demo.AssignedExpertId is Guid expertId)
        {
            appeal.ExpertParticipants.Add(new AppealExpertParticipant
            {
                Id = ChildId(demo.Number, 200),
                ExpertUserId = expertId,
                Role = AppealExpertRole.Responsible,
                AddedByUserId = OperatorUserId,
                AddedAt = assignedAt!.Value
            });
            appeal.AssignmentHistory.Add(new AppealAssignmentEvent
            {
                Id = ChildId(demo.Number, 201),
                EventType = "Assigned",
                ExpertUserId = expertId,
                Role = AppealExpertRole.Responsible,
                ActorUserId = OperatorUserId,
                OccurredAt = assignedAt.Value
            });
        }

        if (demo.Status is AppealStatus.InProgress or AppealStatus.NeedsClarification
            or AppealStatus.RecommendationReady or AppealStatus.Closed)
        {
            appeal.InternalNotes.Add(new AppealInternalNote
            {
                Id = ChildId(demo.Number, 300),
                ClientNoteId = ChildId(demo.Number, 301),
                AuthorUserId = demo.AssignedExpertId!.Value,
                Body = "Внутренняя заметка специалиста для проверки разграничения доступа.",
                CreatedAt = createdAt.AddMinutes(65)
            });
        }

        if (demo.Status == AppealStatus.NeedsClarification)
        {
            appeal.Messages.Add(new AppealMessage
            {
                Id = ChildId(demo.Number, 400),
                ClientMessageId = ChildId(demo.Number, 401),
                Author = AppealMessageAuthor.Expert,
                AuthorUserId = demo.AssignedExpertId,
                Body = "Подскажите, пожалуйста, когда ситуация повторилась в последний раз?",
                CreatedAt = createdAt.AddMinutes(80)
            });
        }

        if (demo.Status is AppealStatus.RecommendationReady or AppealStatus.Returned or AppealStatus.Closed)
        {
            var authorId = demo.AssignedExpertId ?? PrimaryExpertUserId;
            appeal.Recommendations.Add(new AppealRecommendation
            {
                Id = ChildId(demo.Number, 500),
                ClientRecommendationId = ChildId(demo.Number, 501),
                AuthorUserId = authorId,
                Version = 1,
                Body = "Выберите взрослого, которому доверяете, сохраните сообщения и договоритесь, как быстро связаться, если ситуация повторится.",
                CreatedAt = createdAt.AddMinutes(100)
            });
        }

        if (demo.Status == AppealStatus.Returned)
        {
            appeal.OutcomeActions.Add(new ApplicantOutcomeAction
            {
                Id = ChildId(demo.Number, 600),
                ClientActionId = ChildId(demo.Number, 601),
                Type = ApplicantOutcomeType.Returned,
                ReturnReason = AppealReturnReason.NeedMoreHelp,
                Details = "Совет попробовали, но давление продолжается.",
                ReturnSequence = 1,
                CreatedAt = appeal.ReturnedAt!.Value
            });
        }

        if (demo.Status == AppealStatus.Closed)
        {
            appeal.OutcomeActions.Add(new ApplicantOutcomeAction
            {
                Id = ChildId(demo.Number, 610),
                ClientActionId = ChildId(demo.Number, 611),
                Type = ApplicantOutcomeType.Helped,
                CreatedAt = createdAt.AddMinutes(130)
            });
        }

        if (demo.CrisisFlag)
        {
            database.AdminAlerts.Add(new AdminAlert
            {
                Id = ChildId(demo.Number, 700),
                AppealId = demo.Id,
                Type = "CrisisDetected:Submission",
                CreatedAt = appeal.CrisisDetectedAt!.Value
            });
        }

        return appeal;
    }

    private static AppealStatus[] StatusPath(AppealStatus status) => status switch
    {
        AppealStatus.New => [AppealStatus.New],
        AppealStatus.Triaged => [AppealStatus.New, AppealStatus.Triaged],
        AppealStatus.Assigned => [AppealStatus.New, AppealStatus.Triaged, AppealStatus.Assigned],
        AppealStatus.InProgress => [AppealStatus.New, AppealStatus.Triaged, AppealStatus.Assigned, AppealStatus.InProgress],
        AppealStatus.NeedsClarification =>
            [AppealStatus.New, AppealStatus.Triaged, AppealStatus.Assigned, AppealStatus.InProgress, AppealStatus.NeedsClarification],
        AppealStatus.RecommendationReady =>
            [AppealStatus.New, AppealStatus.Triaged, AppealStatus.Assigned, AppealStatus.InProgress, AppealStatus.RecommendationReady],
        AppealStatus.Returned =>
            [AppealStatus.New, AppealStatus.Triaged, AppealStatus.Assigned, AppealStatus.InProgress, AppealStatus.RecommendationReady, AppealStatus.Returned],
        AppealStatus.Closed =>
            [AppealStatus.New, AppealStatus.Triaged, AppealStatus.Assigned, AppealStatus.InProgress, AppealStatus.RecommendationReady, AppealStatus.Closed],
        AppealStatus.Rejected => [AppealStatus.New, AppealStatus.Rejected],
        _ => [AppealStatus.New]
    };

    private static string StatusSource(AppealStatus status) => status switch
    {
        AppealStatus.Returned or AppealStatus.Closed => "Applicant",
        AppealStatus.InProgress or AppealStatus.NeedsClarification or AppealStatus.RecommendationReady => "Expert",
        _ => "Operator"
    };

    private static DemoAppeal Demo(
        int number,
        string trackNumber,
        ApplicantType applicantType,
        SubmissionPath submissionPath,
        Guid? categoryId,
        AppealStatus status,
        AppealPriority priority,
        double ageHours,
        Guid? assignedExpertId,
        string narrative,
        string place,
        string frequency,
        int returnCount = 0,
        bool crisisFlag = false) => new(
            number,
            Guid.Parse($"50000000-0000-0000-0000-{number:D12}"),
            Guid.Parse($"51000000-0000-0000-0000-{number:D12}"),
            trackNumber,
            applicantType,
            submissionPath,
            categoryId,
            status,
            priority,
            TimeSpan.FromHours(ageHours),
            assignedExpertId,
            narrative,
            place,
            frequency,
            returnCount,
            crisisFlag);

    private static Guid ChildId(int demoNumber, int recordNumber) =>
        Guid.Parse($"60000000-0000-0000-{demoNumber:D4}-{recordNumber:D12}");

    private sealed record DemoAppeal(
        int Number,
        Guid Id,
        Guid ClientRequestId,
        string TrackNumber,
        ApplicantType ApplicantType,
        SubmissionPath SubmissionPath,
        Guid? CategoryId,
        AppealStatus Status,
        AppealPriority Priority,
        TimeSpan Age,
        Guid? AssignedExpertId,
        string Narrative,
        string Place,
        string Frequency,
        int ReturnCount,
        bool CrisisFlag);
}
