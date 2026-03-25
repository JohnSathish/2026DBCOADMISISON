using System.Text.Json;
using ERP.Application.Admissions;
using ERP.Application.Admissions.DTOs;
using ERP.Application.Admissions.Helpers;
using ERP.Application.Admissions.Interfaces;
using ERP.Domain.Admissions.Entities;
using MediatR;

namespace ERP.Application.Admissions.Commands.SaveApplicantApplicationDraft;

public sealed class SaveApplicantApplicationDraftCommandHandler : IRequestHandler<SaveApplicantApplicationDraftCommand, ApplicantApplicationDraftResponse>
{
    private readonly IApplicantApplicationRepository _applicationRepository;
    private readonly IApplicantAccountRepository _accountRepository;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    public SaveApplicantApplicationDraftCommandHandler(
        IApplicantApplicationRepository applicationRepository,
        IApplicantAccountRepository accountRepository)
    {
        _applicationRepository = applicationRepository;
        _accountRepository = accountRepository;
    }

    public async Task<ApplicantApplicationDraftResponse> Handle(SaveApplicantApplicationDraftCommand request, CancellationToken cancellationToken)
    {
        var payload = request.Payload;

        // Debug: Log the totalMarks value being saved
        System.Diagnostics.Debug.WriteLine($"Saving draft - TotalMarks: '{payload.Academics.BoardExamination.TotalMarks}'");

        var existing = await _applicationRepository.GetDraftByAccountIdAsync(request.AccountId, cancellationToken);
        if (existing is not null)
        {
            var existingPayload = JsonSerializer.Deserialize<ApplicantApplicationDraftDto>(existing.Data, _serializerOptions)
                                ?? ApplicantApplicationDraftDto.Empty;

            // Check if existing courses are empty
            var existingCoursesEmpty = string.IsNullOrWhiteSpace(existingPayload.Courses.MajorSubject) &&
                                      string.IsNullOrWhiteSpace(existingPayload.Courses.MinorSubject) &&
                                      string.IsNullOrWhiteSpace(existingPayload.Courses.MultidisciplinaryChoice) &&
                                      string.IsNullOrWhiteSpace(existingPayload.Courses.AbilityEnhancementChoice) &&
                                      string.IsNullOrWhiteSpace(existingPayload.Courses.SkillEnhancementChoice);

            if (existingPayload.CoursesLocked)
            {
                // If existing courses are empty, unlock to allow user to fill in missing data
                if (existingCoursesEmpty)
                {
                    payload.CoursesLocked = false;
                    // Allow the new courses to be saved
                }
                else
                {
                    // If courses are locked and existing courses are not empty, preserve the lock and existing courses
                    payload.CoursesLocked = true;
                    payload.Courses = existingPayload.Courses;
                }
            }
        }

        ApplicantCourseSubjectNormalizer.Normalize(payload);
        AcademicSectionDraftSync.PushToLegacy(payload.Academics);

        AdmissionPreferredShiftPolicy.EnsureAllowedForNewApplicationOrThrow(payload.Courses.Shift);

        var serialized = JsonSerializer.Serialize(payload, _serializerOptions);
        // Debug: Log the serialized JSON to verify totalMarks is included
        System.Diagnostics.Debug.WriteLine($"Serialized JSON contains totalMarks: {serialized.Contains("totalMarks", StringComparison.OrdinalIgnoreCase)}");
        
        var draft = new ApplicantApplicationDraft(request.AccountId, serialized);
        draft.Update(serialized, DateTime.UtcNow);

        await _applicationRepository.UpsertDraftAsync(draft, cancellationToken);

        var shift = payload.Courses.Shift;
        if (!string.IsNullOrWhiteSpace(shift))
        {
            await _accountRepository.UpdateShiftAsync(request.AccountId, shift, cancellationToken);
        }

        var pct = AcademicPercentageParser.ParseClassXiiPercentage(payload.Academics?.BoardExamination?.Percentage);
        await _accountRepository.UpdateClassXiPercentageAsync(
            request.AccountId,
            pct > 0 ? pct : null,
            cancellationToken);

        return new ApplicantApplicationDraftResponse
        {
            Payload = payload,
            UpdatedOnUtc = draft.UpdatedOnUtc
        };
    }
}

