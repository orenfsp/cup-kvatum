using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Otklik.Application.Analytics;
using Otklik.Domain.Analytics;
using Otklik.Domain.Appeals;
using Otklik.Infrastructure.Identity;

namespace Otklik.Infrastructure.Persistence;

public sealed class OtklikDbContext(DbContextOptions<OtklikDbContext> options)
    : IdentityDbContext<StaffUser, IdentityRole<Guid>, Guid>(options)
{
    private static readonly JsonSerializerOptions AnalyticsJson = new(JsonSerializerDefaults.Web);

    public DbSet<Appeal> Appeals => Set<Appeal>();

    public DbSet<AppealThread> AppealThreads => Set<AppealThread>();

    public DbSet<AppealCategory> AppealCategories => Set<AppealCategory>();

    public DbSet<AppealAnswer> AppealAnswers => Set<AppealAnswer>();

    public DbSet<AppealStatusChange> AppealStatusChanges => Set<AppealStatusChange>();

    public DbSet<AppealAttachment> AppealAttachments => Set<AppealAttachment>();

    public DbSet<ExpertGroup> ExpertGroups => Set<ExpertGroup>();

    public DbSet<ExpertGroupMembership> ExpertGroupMemberships => Set<ExpertGroupMembership>();

    public DbSet<AppealRoutingRule> AppealRoutingRules => Set<AppealRoutingRule>();

    public DbSet<OperatorActionLog> OperatorActionLogs => Set<OperatorActionLog>();

    public DbSet<AdminAlert> AdminAlerts => Set<AdminAlert>();

    public DbSet<AppealMessage> AppealMessages => Set<AppealMessage>();

    public DbSet<AppealInternalNote> AppealInternalNotes => Set<AppealInternalNote>();

    public DbSet<AppealRecommendation> AppealRecommendations => Set<AppealRecommendation>();

    public DbSet<ExpertWorkflowRequest> ExpertWorkflowRequests => Set<ExpertWorkflowRequest>();

    public DbSet<AppealExpertParticipant> AppealExpertParticipants => Set<AppealExpertParticipant>();

    public DbSet<AppealAssignmentEvent> AppealAssignmentEvents => Set<AppealAssignmentEvent>();

    public DbSet<ApplicantOutcomeAction> ApplicantOutcomeActions => Set<ApplicantOutcomeAction>();

    public DbSet<AppealResultFeedback> AppealResultFeedbacks => Set<AppealResultFeedback>();

    public DbSet<AppealComplaint> AppealComplaints => Set<AppealComplaint>();

    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();

    public DbSet<CrisisMarker> CrisisMarkers => Set<CrisisMarker>();

    public DbSet<CrisisSupportContact> CrisisSupportContacts => Set<CrisisSupportContact>();

    public DbSet<AppealCrisisContact> AppealCrisisContacts => Set<AppealCrisisContact>();

    public DbSet<CrisisContactAccessLog> CrisisContactAccessLogs => Set<CrisisContactAccessLog>();

    public DbSet<AdministrativeAuditEvent> AdministrativeAuditEvents => Set<AdministrativeAuditEvent>();

    public DbSet<AnalyticsOutboxMessage> AnalyticsOutboxMessages => Set<AnalyticsOutboxMessage>();

    public DbSet<AppealAnalyticsProjection> AppealAnalyticsProjections => Set<AppealAnalyticsProjection>();

    public DbSet<ProcessedAnalyticsEvent> ProcessedAnalyticsEvents => Set<ProcessedAnalyticsEvent>();

    public DbSet<AnalyticsExport> AnalyticsExports => Set<AnalyticsExport>();

    public DbSet<AppealDeviceSession> AppealDeviceSessions => Set<AppealDeviceSession>();

    public DbSet<AppealDeviceGrant> AppealDeviceGrants => Set<AppealDeviceGrant>();

    public DbSet<AppealPushSubscription> AppealPushSubscriptions => Set<AppealPushSubscription>();

    public DbSet<AppealNotificationOutbox> AppealNotificationOutboxes => Set<AppealNotificationOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("otklik");
        modelBuilder.Entity<StaffUser>(user =>
        {
            user.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            user.Property(x => x.CreatedAt).IsRequired();
            user.Property(x => x.UpdatedAt).IsRequired();
            user.Property(x => x.IsAvailable).HasDefaultValue(true);
            user.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<AppealCategory>(category =>
        {
            category.ToTable("AppealCategories");
            category.HasKey(x => x.Id);
            category.Property(x => x.Code).HasMaxLength(64).IsRequired();
            category.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            category.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1);
            category.Property(x => x.UpdatedAt).IsRequired();
            category.HasIndex(x => x.Code).IsUnique();
            category.HasData(AppealCategory.Initial);
        });

        modelBuilder.Entity<AppealThread>(thread =>
        {
            thread.ToTable("AppealThreads");
            thread.HasKey(x => x.Id);
            thread.Property(x => x.TrackHash).HasMaxLength(32).IsRequired();
            thread.Property(x => x.TrackRecoveryCiphertext).HasMaxLength(2_048).IsRequired();
            thread.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1);
            thread.Property(x => x.CreatedAt).IsRequired();
            thread.Property(x => x.LastActivityAt).IsRequired();
            thread.HasIndex(x => x.TrackHash).IsUnique();
            thread.HasOne(x => x.CurrentCycle)
                .WithMany()
                .HasForeignKey(x => x.CurrentCycleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Appeal>(appeal =>
        {
            appeal.ToTable("Appeals");
            appeal.HasKey(x => x.Id);
            appeal.Property(x => x.ApplicantType).HasConversion<string>().HasMaxLength(32).IsRequired();
            appeal.Property(x => x.SubmissionPath).HasConversion<string>().HasMaxLength(32).IsRequired();
            appeal.Property(x => x.Status).HasConversion<string>().HasMaxLength(48).IsRequired();
            appeal.Property(x => x.Priority)
                .HasConversion<string>()
                .HasMaxLength(24)
                .HasDefaultValue(AppealPriority.Standard)
                .IsRequired();
            appeal.Property(x => x.RejectionReason).HasConversion<string>().HasMaxLength(32);
            appeal.Property(x => x.RejectionInternalReason).HasMaxLength(1_000);
            appeal.Property(x => x.PublicResolution).HasMaxLength(4_000);
            appeal.Property(x => x.Narrative).HasMaxLength(10_000);
            appeal.Property(x => x.Sequence).HasDefaultValue(1).IsRequired();
            appeal.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1);
            appeal.Property(x => x.ReturnCount).HasDefaultValue(0);
            appeal.HasIndex(x => x.ClientRequestId).IsUnique();
            appeal.HasIndex(x => new { x.ThreadId, x.Sequence }).IsUnique();
            appeal.HasIndex(x => new { x.ThreadId, x.ClientContinuationId }).IsUnique();
            appeal.HasIndex(x => new { x.Status, x.CrisisFlag, x.Priority, x.CreatedAt });
            appeal.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            appeal.HasOne(x => x.Thread)
                .WithMany(x => x.Cycles)
                .HasForeignKey(x => x.ThreadId)
                .OnDelete(DeleteBehavior.Restrict);
            appeal.HasOne<StaffUser>()
                .WithMany()
                .HasForeignKey(x => x.AssignedExpertId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppealAnswer>(answer =>
        {
            answer.ToTable("AppealAnswers");
            answer.HasKey(x => x.Id);
            answer.Property(x => x.QuestionCode).HasMaxLength(64).IsRequired();
            answer.Property(x => x.Value).HasMaxLength(1_000).IsRequired();
            answer.HasIndex(x => new { x.AppealId, x.QuestionCode }).IsUnique();
            answer.HasOne(x => x.Appeal)
                .WithMany(x => x.Answers)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppealStatusChange>(change =>
        {
            change.ToTable("AppealStatusChanges");
            change.HasKey(x => x.Id);
            change.Property(x => x.Status).HasConversion<string>().HasMaxLength(48).IsRequired();
            change.Property(x => x.Source).HasMaxLength(32).IsRequired();
            change.HasIndex(x => new { x.AppealId, x.ChangedAt });
            change.HasOne(x => x.Appeal)
                .WithMany(x => x.StatusHistory)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppealAttachment>(attachment =>
        {
            attachment.ToTable("AppealAttachments");
            attachment.HasKey(x => x.Id);
            attachment.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            attachment.Property(x => x.ContentType).HasMaxLength(64).IsRequired();
            attachment.Property(x => x.StorageKey).HasMaxLength(160).IsRequired();
            attachment.HasIndex(x => new { x.AppealId, x.ClientUploadId }).IsUnique();
            attachment.HasIndex(x => x.StorageKey).IsUnique();
            attachment.HasOne(x => x.Appeal)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppealMessage>(message =>
        {
            message.ToTable("AppealMessages");
            message.HasKey(x => x.Id);
            message.Property(x => x.Author).HasConversion<string>().HasMaxLength(24).IsRequired();
            message.Property(x => x.Body).HasMaxLength(4_000).IsRequired();
            message.HasIndex(x => new { x.AppealId, x.ClientMessageId }).IsUnique();
            message.HasIndex(x => new { x.AppealId, x.CreatedAt });
            message.HasOne(x => x.Appeal)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
            message.HasOne<StaffUser>()
                .WithMany()
                .HasForeignKey(x => x.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppealInternalNote>(note =>
        {
            note.ToTable("AppealInternalNotes");
            note.HasKey(x => x.Id);
            note.Property(x => x.Body).HasMaxLength(4_000).IsRequired();
            note.HasIndex(x => new { x.AppealId, x.ClientNoteId }).IsUnique();
            note.HasIndex(x => new { x.AppealId, x.CreatedAt });
            note.HasOne(x => x.Appeal)
                .WithMany(x => x.InternalNotes)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
            note.HasOne<StaffUser>()
                .WithMany()
                .HasForeignKey(x => x.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppealRecommendation>(recommendation =>
        {
            recommendation.ToTable("AppealRecommendations");
            recommendation.HasKey(x => x.Id);
            recommendation.Property(x => x.Body).HasMaxLength(10_000).IsRequired();
            recommendation.HasIndex(x => new { x.AppealId, x.ClientRecommendationId }).IsUnique();
            recommendation.HasIndex(x => new { x.AppealId, x.Version }).IsUnique();
            recommendation.HasOne(x => x.Appeal)
                .WithMany(x => x.Recommendations)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
            recommendation.HasOne<StaffUser>()
                .WithMany()
                .HasForeignKey(x => x.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExpertWorkflowRequest>(request =>
        {
            request.ToTable("ExpertWorkflowRequests");
            request.HasKey(x => x.Id);
            request.Property(x => x.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
            request.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
            request.Property(x => x.Reason).HasMaxLength(1_000).IsRequired();
            request.Property(x => x.SelectedPriority).HasConversion<string>().HasMaxLength(24);
            request.Property(x => x.DecisionReason).HasMaxLength(1_000);
            request.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1);
            request.HasIndex(x => x.ClientRequestId).IsUnique();
            request.HasIndex(x => new { x.Status, x.RequestedAt });
            request.HasOne(x => x.Appeal)
                .WithMany(x => x.WorkflowRequests)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
            request.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            request.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.SelectedExpertUserId).OnDelete(DeleteBehavior.Restrict);
            request.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppealExpertParticipant>(participant =>
        {
            participant.ToTable("AppealExpertParticipants");
            participant.HasKey(x => x.Id);
            participant.Property(x => x.Role).HasConversion<string>().HasMaxLength(24).IsRequired();
            participant.HasIndex(x => new { x.AppealId, x.ExpertUserId, x.RemovedAt });
            participant.HasOne(x => x.Appeal)
                .WithMany(x => x.ExpertParticipants)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
            participant.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.ExpertUserId).OnDelete(DeleteBehavior.Restrict);
            participant.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.AddedByUserId).OnDelete(DeleteBehavior.Restrict);
            participant.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.RemovedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppealAssignmentEvent>(assignmentEvent =>
        {
            assignmentEvent.ToTable("AppealAssignmentEvents");
            assignmentEvent.HasKey(x => x.Id);
            assignmentEvent.Property(x => x.EventType).HasMaxLength(32).IsRequired();
            assignmentEvent.Property(x => x.Role).HasConversion<string>().HasMaxLength(24).IsRequired();
            assignmentEvent.HasIndex(x => new { x.AppealId, x.OccurredAt });
            assignmentEvent.HasOne(x => x.Appeal)
                .WithMany(x => x.AssignmentHistory)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
            assignmentEvent.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.ExpertUserId).OnDelete(DeleteBehavior.Restrict);
            assignmentEvent.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            assignmentEvent.HasOne<ExpertWorkflowRequest>().WithMany().HasForeignKey(x => x.WorkflowRequestId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApplicantOutcomeAction>(action =>
        {
            action.ToTable("ApplicantOutcomeActions");
            action.HasKey(x => x.Id);
            action.Property(x => x.Type).HasConversion<string>().HasMaxLength(24).IsRequired();
            action.Property(x => x.ReturnReason).HasConversion<string>().HasMaxLength(32);
            action.Property(x => x.Details).HasMaxLength(2_000);
            action.HasIndex(x => x.ClientActionId).IsUnique();
            action.HasIndex(x => new { x.AppealId, x.CreatedAt });
            action.HasOne(x => x.Appeal).WithMany(x => x.OutcomeActions)
                .HasForeignKey(x => x.AppealId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppealResultFeedback>(feedback =>
        {
            feedback.ToTable("AppealResultFeedbacks");
            feedback.HasKey(x => x.Id);
            feedback.Property(x => x.Comment).HasMaxLength(2_000);
            feedback.HasIndex(x => x.ClientFeedbackId).IsUnique();
            feedback.HasIndex(x => x.AppealId).IsUnique();
            feedback.HasOne(x => x.Appeal).WithOne(x => x.ResultFeedback)
                .HasForeignKey<AppealResultFeedback>(x => x.AppealId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppealComplaint>(complaint =>
        {
            complaint.ToTable("AppealComplaints");
            complaint.HasKey(x => x.Id);
            complaint.Property(x => x.Body).HasMaxLength(2_000).IsRequired();
            complaint.HasIndex(x => x.ClientComplaintId).IsUnique();
            complaint.HasIndex(x => new { x.ResolvedAt, x.CreatedAt });
            complaint.HasOne(x => x.Appeal).WithMany(x => x.Complaints)
                .HasForeignKey(x => x.AppealId).OnDelete(DeleteBehavior.Cascade);
            complaint.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlatformSetting>(setting =>
        {
            setting.ToTable("PlatformSettings");
            setting.HasKey(x => x.Key);
            setting.Property(x => x.Key).HasMaxLength(160);
            setting.Property(x => x.Value).HasMaxLength(2_000).IsRequired();
            setting.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
            setting.HasData(new PlatformSetting
            {
                Key = PlatformSetting.AutoCloseDaysKey,
                Value = "7",
                UpdatedAt = DateTimeOffset.UnixEpoch
            });
        });

        modelBuilder.Entity<CrisisMarker>(marker =>
        {
            marker.ToTable("CrisisMarkers");
            marker.HasKey(x => x.Id);
            marker.Property(x => x.Pattern).HasMaxLength(160).IsRequired();
            marker.Property(x => x.RiskType).HasMaxLength(48).IsRequired();
            marker.HasIndex(x => x.Pattern).IsUnique();
            marker.HasIndex(x => new { x.IsActive, x.SortOrder });
            marker.HasData(CrisisMarker.Initial);
        });

        modelBuilder.Entity<CrisisSupportContact>(contact =>
        {
            contact.ToTable("CrisisSupportContacts");
            contact.HasKey(x => x.Id);
            contact.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            contact.Property(x => x.DisplayNumber).HasMaxLength(40).IsRequired();
            contact.Property(x => x.DialNumber).HasMaxLength(40).IsRequired();
            contact.Property(x => x.Description).HasMaxLength(240).IsRequired();
            contact.HasIndex(x => new { x.IsActive, x.SortOrder });
            contact.HasData(CrisisSupportContact.Initial);
        });

        modelBuilder.Entity<AppealCrisisContact>(contact =>
        {
            contact.ToTable("AppealCrisisContacts");
            contact.HasKey(x => x.AppealId);
            contact.Property(x => x.Ciphertext).HasMaxLength(2_048).IsRequired();
            contact.HasOne(x => x.Appeal)
                .WithOne(x => x.CrisisContact)
                .HasForeignKey<AppealCrisisContact>(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CrisisContactAccessLog>(access =>
        {
            access.ToTable("CrisisContactAccessLogs");
            access.HasKey(x => x.Id);
            access.HasIndex(x => new { x.AppealId, x.AccessedAt });
            access.HasOne(x => x.Appeal)
                .WithMany(x => x.CrisisContactAccesses)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
            access.HasOne<StaffUser>()
                .WithMany()
                .HasForeignKey(x => x.OperatorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExpertGroup>(group =>
        {
            group.ToTable("ExpertGroups");
            group.HasKey(x => x.Id);
            group.Property(x => x.Code).HasMaxLength(64).IsRequired();
            group.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            group.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1);
            group.Property(x => x.UpdatedAt).IsRequired();
            group.HasIndex(x => x.Code).IsUnique();
            group.HasData(
                new ExpertGroup
                {
                    Id = ExpertGroup.SupportId,
                    Code = "support",
                    DisplayName = "Психологическая поддержка",
                    ActiveAppealLimit = 5,
                    IsActive = true
                },
                new ExpertGroup
                {
                    Id = ExpertGroup.MediationId,
                    Code = "mediation",
                    DisplayName = "Медиация конфликтов",
                    ActiveAppealLimit = 5,
                    IsActive = true
                });
        });

        modelBuilder.Entity<ExpertGroupMembership>(membership =>
        {
            membership.ToTable("ExpertGroupMemberships");
            membership.HasKey(x => new { x.ExpertGroupId, x.ExpertUserId });
            membership.HasOne(x => x.ExpertGroup)
                .WithMany()
                .HasForeignKey(x => x.ExpertGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            membership.HasOne<StaffUser>()
                .WithMany()
                .HasForeignKey(x => x.ExpertUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppealRoutingRule>(rule =>
        {
            rule.ToTable("AppealRoutingRules");
            rule.HasKey(x => x.Id);
            rule.HasIndex(x => x.CategoryId).IsUnique();
            rule.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1);
            rule.Property(x => x.IsActive).HasDefaultValue(true);
            rule.Property(x => x.UpdatedAt).IsRequired();
            rule.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            rule.HasOne(x => x.ExpertGroup)
                .WithMany()
                .HasForeignKey(x => x.ExpertGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            rule.HasData(
                new { Id = Guid.Parse("40000000-0000-0000-0000-000000000001"), CategoryId = AppealCategory.BullyingId, ExpertGroupId = ExpertGroup.SupportId, Version = 1, IsActive = true, UpdatedAt = DateTimeOffset.UnixEpoch },
                new { Id = Guid.Parse("40000000-0000-0000-0000-000000000002"), CategoryId = AppealCategory.PressureId, ExpertGroupId = ExpertGroup.SupportId, Version = 1, IsActive = true, UpdatedAt = DateTimeOffset.UnixEpoch },
                new { Id = Guid.Parse("40000000-0000-0000-0000-000000000003"), CategoryId = AppealCategory.UnsureId, ExpertGroupId = ExpertGroup.SupportId, Version = 1, IsActive = true, UpdatedAt = DateTimeOffset.UnixEpoch },
                new { Id = Guid.Parse("40000000-0000-0000-0000-000000000004"), CategoryId = AppealCategory.ConflictId, ExpertGroupId = ExpertGroup.MediationId, Version = 1, IsActive = true, UpdatedAt = DateTimeOffset.UnixEpoch });
        });

        modelBuilder.Entity<OperatorActionLog>(action =>
        {
            action.ToTable("OperatorActionLogs");
            action.HasKey(x => x.Id);
            action.Property(x => x.Action).HasMaxLength(64).IsRequired();
            action.Property(x => x.FromValue).HasMaxLength(160);
            action.Property(x => x.ToValue).HasMaxLength(160);
            action.Property(x => x.Reason).HasMaxLength(1_000);
            action.HasIndex(x => new { x.AppealId, x.OccurredAt });
            action.HasOne(x => x.Appeal)
                .WithMany(x => x.OperatorActions)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
            action.HasOne<StaffUser>()
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdminAlert>(alert =>
        {
            alert.ToTable("AdminAlerts");
            alert.HasKey(x => x.Id);
            alert.Property(x => x.Type).HasMaxLength(64).IsRequired();
            alert.HasIndex(x => new { x.AppealId, x.Type, x.ResolvedAt });
            alert.HasOne(x => x.Appeal)
                .WithMany()
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdministrativeAuditEvent>(audit =>
        {
            audit.ToTable("AdministrativeAuditEvents");
            audit.HasKey(x => x.Id);
            audit.Property(x => x.ActorDisplayName).HasMaxLength(160).IsRequired();
            audit.Property(x => x.Action).HasMaxLength(80).IsRequired();
            audit.Property(x => x.TargetType).HasMaxLength(48).IsRequired();
            audit.Property(x => x.BeforeMetadataJson).HasColumnType("jsonb");
            audit.Property(x => x.AfterMetadataJson).HasColumnType("jsonb");
            audit.Property(x => x.Reason).HasMaxLength(1_000);
            audit.HasIndex(x => new { x.OccurredAt, x.Action });
            audit.HasIndex(x => new { x.TargetType, x.TargetId });
            audit.HasOne<StaffUser>()
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AnalyticsOutboxMessage>(message =>
        {
            message.ToTable("AnalyticsOutboxMessages");
            message.HasKey(x => x.Id);
            message.Property(x => x.EventType).HasMaxLength(96).IsRequired();
            message.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            message.Property(x => x.LastPublishError).HasMaxLength(1_000);
            message.HasIndex(x => new { x.PublishedAt, x.OccurredAt });
            message.HasIndex(x => new { x.AppealId, x.AppealVersion });
        });

        modelBuilder.Entity<AppealAnalyticsProjection>(projection =>
        {
            projection.ToTable("AppealAnalyticsProjections");
            projection.HasKey(x => x.AppealId);
            projection.Property(x => x.ApplicantType).HasMaxLength(32).IsRequired();
            projection.Property(x => x.Status).HasMaxLength(48).IsRequired();
            projection.Property(x => x.Priority).HasMaxLength(24).IsRequired();
            projection.HasIndex(x => x.CreatedAt);
            projection.HasIndex(x => x.OperatorUserId);
            projection.HasIndex(x => x.LastAssignedExpertId);
            projection.HasIndex(x => new { x.Status, x.Priority });
        });

        modelBuilder.Entity<ProcessedAnalyticsEvent>(processed =>
        {
            processed.ToTable("ProcessedAnalyticsEvents");
            processed.HasKey(x => x.EventId);
            processed.HasIndex(x => new { x.AppealId, x.ProcessedAt });
        });

        modelBuilder.Entity<AnalyticsExport>(export =>
        {
            export.ToTable("AnalyticsExports");
            export.HasKey(x => x.Id);
            export.Property(x => x.RequestedByRole).HasMaxLength(32).IsRequired();
            export.Property(x => x.Format).HasMaxLength(8).IsRequired();
            export.Property(x => x.FilePath).HasMaxLength(1_024).IsRequired();
            export.HasIndex(x => new { x.RequestedByUserId, x.CreatedAt });
            export.HasOne<StaffUser>()
                .WithMany()
                .HasForeignKey(x => x.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppealDeviceSession>(session =>
        {
            session.ToTable("AppealDeviceSessions");
            session.HasKey(x => x.Id);
            session.Property(x => x.TokenHash).HasMaxLength(32).IsRequired();
            session.HasIndex(x => x.TokenHash).IsUnique();
            session.HasIndex(x => new { x.RevokedAt, x.ExpiresAt });
        });

        modelBuilder.Entity<AppealDeviceGrant>(grant =>
        {
            grant.ToTable("AppealDeviceGrants");
            grant.HasKey(x => new { x.DeviceSessionId, x.ThreadId });
            grant.HasIndex(x => new { x.ThreadId, x.RevokedAt });
            grant.HasOne(x => x.DeviceSession)
                .WithMany(x => x.Grants)
                .HasForeignKey(x => x.DeviceSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            grant.HasOne(x => x.Thread)
                .WithMany()
                .HasForeignKey(x => x.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppealPushSubscription>(subscription =>
        {
            subscription.ToTable("AppealPushSubscriptions");
            subscription.HasKey(x => x.Id);
            subscription.Property(x => x.EndpointHash).HasMaxLength(32).IsRequired();
            subscription.Property(x => x.EndpointCiphertext).HasMaxLength(4_096).IsRequired();
            subscription.Property(x => x.P256dhCiphertext).HasMaxLength(2_048).IsRequired();
            subscription.Property(x => x.AuthCiphertext).HasMaxLength(2_048).IsRequired();
            subscription.HasIndex(x => x.EndpointHash).IsUnique();
            subscription.HasIndex(x => new { x.ThreadId, x.RevokedAt });
            subscription.HasOne(x => x.DeviceSession)
                .WithMany()
                .HasForeignKey(x => x.DeviceSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            subscription.HasOne(x => x.Thread)
                .WithMany()
                .HasForeignKey(x => x.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppealNotificationOutbox>(notification =>
        {
            notification.ToTable("AppealNotificationOutboxes");
            notification.HasKey(x => x.Id);
            notification.HasIndex(x => new { x.ProcessedAt, x.OccurredAt });
            notification.HasOne<Appeal>()
                .WithMany()
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        QueueAppealAnalyticsEvents();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        QueueAppealAnalyticsEvents();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void QueueAppealAnalyticsEvents()
    {
        ChangeTracker.DetectChanges();
        var appealEntries = ChangeTracker.Entries<Appeal>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .ToList();
        if (appealEntries.Count == 0) return;

        var statusChanges = ChangeTracker.Entries<AppealStatusChange>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();
        var operatorActions = ChangeTracker.Entries<OperatorActionLog>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();
        var expertMessages = ChangeTracker.Entries<AppealMessage>()
            .Where(entry => entry.State == EntityState.Added
                && entry.Entity.Author == AppealMessageAuthor.Expert)
            .Select(entry => entry.Entity)
            .ToList();
        var recommendations = ChangeTracker.Entries<AppealRecommendation>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();

        foreach (var entry in appealEntries)
        {
            var appeal = entry.Entity;
            if (entry.State == EntityState.Modified)
            {
                AppealNotificationOutboxes.Add(new AppealNotificationOutbox
                {
                    Id = Guid.NewGuid(),
                    AppealId = appeal.Id,
                    OccurredAt = DateTimeOffset.UtcNow
                });
            }
            var eventId = Guid.NewGuid();
            var appealStatusChanges = statusChanges.Where(item => item.AppealId == appeal.Id).ToList();
            var operatorAction = operatorActions
                .Where(item => item.AppealId == appeal.Id)
                .OrderBy(item => item.OccurredAt)
                .FirstOrDefault();
            var firstExpertResponseAt = expertMessages
                .Where(item => item.AppealId == appeal.Id)
                .Select(item => (DateTimeOffset?)item.CreatedAt)
                .Concat(recommendations
                    .Where(item => item.AppealId == appeal.Id)
                    .Select(item => (DateTimeOffset?)item.CreatedAt))
                .Min();
            var operatorAcceptedAt = appealStatusChanges
                .Where(item => item.Source == "Operator")
                .Select(item => (DateTimeOffset?)item.ChangedAt)
                .Min();
            var occurredAt = appealStatusChanges.Select(item => item.ChangedAt)
                .Concat(operatorAction is null ? [] : [operatorAction.OccurredAt])
                .Concat(firstExpertResponseAt is null ? [] : [firstExpertResponseAt.Value])
                .DefaultIfEmpty(entry.State == EntityState.Added && appeal.CreatedAt != default
                    ? appeal.CreatedAt
                    : DateTimeOffset.UtcNow)
                .Max();
            var analyticsEvent = new AppealAnalyticsEvent
            {
                EventId = eventId,
                AppealId = appeal.Id,
                AppealVersion = appeal.Version,
                OccurredAt = occurredAt,
                ApplicantType = appeal.ApplicantType.ToString(),
                CategoryId = appeal.CategoryId,
                ExpertGroupId = appeal.AppliedExpertGroupId,
                Status = appeal.Status.ToString(),
                Priority = appeal.Priority.ToString(),
                CreatedAt = appeal.CreatedAt,
                OperatorAcceptedAt = operatorAcceptedAt,
                FirstExpertResponseAt = firstExpertResponseAt,
                ClosedAt = appeal.CompletedAt,
                OperatorUserId = operatorAction?.ActorUserId,
                AssignedExpertId = appeal.AssignedExpertId,
                CrisisFlag = appeal.CrisisFlag,
                ReturnCount = appeal.ReturnCount
            };
            AnalyticsOutboxMessages.Add(new AnalyticsOutboxMessage
            {
                Id = eventId,
                AppealId = appeal.Id,
                AppealVersion = appeal.Version,
                EventType = AppealAnalyticsEvent.Type,
                PayloadJson = JsonSerializer.Serialize(analyticsEvent, AnalyticsJson),
                OccurredAt = occurredAt
            });
        }
    }
}
