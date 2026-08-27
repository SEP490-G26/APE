using System.Net;
using Application.DTOs;
using Domain.Entities;
using Domain.Enums;

namespace DevDatabaseBootstrap;

public static class AuthenticatedHttpResultVerifier
{
    public static AuthenticatedHttpVerificationReport Validate(
        PESubmissionDetailDto? httpSubmission,
        PE_Submission? mongoSubmission,
        PE_Submission? existingAttemptOneSubmission,
        PE_Submission? existingCSubmission,
        HttpStatusCode submitStatusCode,
        IReadOnlyCollection<SubmissionProcessingStatus> observedStatuses,
        HttpStatusCode noTokenStatusCode,
        HttpStatusCode malformedTokenStatusCode)
    {
        var failures = new List<string>();

        if (submitStatusCode != HttpStatusCode.Accepted)
        {
            failures.Add(
                $"Submission HTTP status was {(int)submitStatusCode}.");
        }

        if (httpSubmission is null)
        {
            failures.Add("HTTP submission result was null.");
        }

        if (mongoSubmission is null)
        {
            failures.Add("Mongo submission result was null.");
        }

        if (httpSubmission is not null &&
            mongoSubmission is not null)
        {
            if (httpSubmission.Id != mongoSubmission.Id)
            {
                failures.Add("HTTP and Mongo submission ids differ.");
            }

            if (httpSubmission.Status != SubmissionProcessingStatus.Completed)
            {
                failures.Add(
                    $"HTTP terminal status is {httpSubmission.Status}.");
            }

            if (mongoSubmission.Status != SubmissionProcessingStatus.Completed)
            {
                failures.Add(
                    $"Mongo terminal status is {mongoSubmission.Status}.");
            }

            if (httpSubmission.Status != mongoSubmission.Status)
            {
                failures.Add("HTTP and Mongo terminal status do not agree.");
            }

            if (httpSubmission.AttemptCount != 2)
            {
                failures.Add(
                    $"HTTP AttemptCount is {httpSubmission.AttemptCount}.");
            }

            if (mongoSubmission.AttemptCount != 2)
            {
                failures.Add(
                    $"Mongo AttemptCount is {mongoSubmission.AttemptCount}.");
            }

            if (httpSubmission.TestCasesPassed != 3 ||
                mongoSubmission.TestCasesPassed != 3)
            {
                failures.Add("TestCasesPassed is not 3 in both views.");
            }

            if (httpSubmission.TotalTestCases != 3 ||
                mongoSubmission.TotalTestCases != 3)
            {
                failures.Add("TotalTestCases is not 3 in both views.");
            }

            if (httpSubmission.QuestionScore != 10 ||
                mongoSubmission.QuestionScore != 10)
            {
                failures.Add("QuestionScore is not 10 in both views.");
            }

            if (httpSubmission.MaxScore != 10 ||
                mongoSubmission.MaxScore != 10)
            {
                failures.Add("MaxScore is not 10 in both views.");
            }

            if (httpSubmission.LanguageId != BootstrapManifest.JavaLanguageId ||
                mongoSubmission.LanguageId != BootstrapManifest.JavaLanguageId)
            {
                failures.Add("LanguageId is not 62 in both views.");
            }

            if (mongoSubmission.LeaseOwner is not null ||
                mongoSubmission.LeaseAcquiredAt is not null ||
                mongoSubmission.LeaseExpiresAt is not null)
            {
                failures.Add("Mongo submission still has an active lease.");
            }
        }

        if (existingAttemptOneSubmission is null)
        {
            failures.Add("AttemptCount 1 Java submission was not found.");
        }
        else if (existingAttemptOneSubmission.AttemptCount != 1)
        {
            failures.Add(
                $"AttemptCount 1 Java submission unexpectedly has AttemptCount {existingAttemptOneSubmission.AttemptCount}.");
        }

        if (existingCSubmission is null)
        {
            failures.Add("Existing completed C submission was not found.");
        }
        else if (existingCSubmission.LanguageId != BootstrapManifest.CLanguageId)
        {
            failures.Add("Existing C submission no longer uses language 50.");
        }

        if (!observedStatuses.Any())
        {
            failures.Add("No HTTP-visible statuses were observed.");
        }

        if (noTokenStatusCode != HttpStatusCode.Unauthorized)
        {
            failures.Add(
                $"No-token result status was {(int)noTokenStatusCode}.");
        }

        if (malformedTokenStatusCode != HttpStatusCode.Unauthorized)
        {
            failures.Add(
                $"Malformed-token result status was {(int)malformedTokenStatusCode}.");
        }

        return new AuthenticatedHttpVerificationReport(
            failures.Count == 0,
            failures);
    }
}

public sealed record AuthenticatedHttpVerificationReport(
    bool IsSuccess,
    IReadOnlyList<string> Failures);
