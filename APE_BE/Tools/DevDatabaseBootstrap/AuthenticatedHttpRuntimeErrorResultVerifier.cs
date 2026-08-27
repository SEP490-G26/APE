using System.Net;
using Application.DTOs;
using Domain.Entities;
using Domain.Enums;

namespace DevDatabaseBootstrap;

public static class AuthenticatedHttpRuntimeErrorResultVerifier
{
    public static AuthenticatedHttpVerificationReport Validate(
        PESubmissionDetailDto? httpSubmission,
        PE_Submission? mongoSubmission,
        PE_Submission? existingAttemptOneSubmission,
        PE_Submission? existingAttemptTwoSubmission,
        PE_Submission? existingAttemptThreeSubmission,
        PE_Submission? existingAttemptFourSubmission,
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

            if (httpSubmission.FinalVerdict != SubmissionVerdict.RuntimeError)
            {
                failures.Add(
                    $"HTTP verdict is {httpSubmission.FinalVerdict?.ToString() ?? "null"}.");
            }

            if (mongoSubmission.FinalVerdict != SubmissionVerdict.RuntimeError)
            {
                failures.Add(
                    $"Mongo verdict is {mongoSubmission.FinalVerdict?.ToString() ?? "null"}.");
            }

            if (httpSubmission.FinalVerdict != mongoSubmission.FinalVerdict)
            {
                failures.Add("HTTP and Mongo verdicts do not agree.");
            }

            if (httpSubmission.AttemptCount != 5)
            {
                failures.Add(
                    $"HTTP AttemptCount is {httpSubmission.AttemptCount}.");
            }

            if (mongoSubmission.AttemptCount != 5)
            {
                failures.Add(
                    $"Mongo AttemptCount is {mongoSubmission.AttemptCount}.");
            }

            if (httpSubmission.TestCasesPassed != 0 ||
                mongoSubmission.TestCasesPassed != 0)
            {
                failures.Add("TestCasesPassed is not 0 in both views.");
            }

            if (httpSubmission.TotalTestCases != 3 ||
                mongoSubmission.TotalTestCases != 3)
            {
                failures.Add("TotalTestCases is not 3 in both views.");
            }

            if (httpSubmission.QuestionScore != 0 ||
                mongoSubmission.QuestionScore != 0)
            {
                failures.Add("QuestionScore is not 0 in both views.");
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

            if (!string.IsNullOrWhiteSpace(httpSubmission.ProcessingError))
            {
                failures.Add("HTTP ProcessingError should be empty for runtime error.");
            }

            if (!string.IsNullOrWhiteSpace(mongoSubmission.ProcessingError))
            {
                failures.Add("Mongo ProcessingError should be empty for runtime error.");
            }

            if (mongoSubmission.LeaseOwner is not null ||
                mongoSubmission.LeaseAcquiredAt is not null ||
                mongoSubmission.LeaseExpiresAt is not null)
            {
                failures.Add("Mongo submission still has an active lease.");
            }

            if (mongoSubmission.TestResultItems.Count != 3)
            {
                failures.Add("Mongo test results count is not 3.");
            }

            if (httpSubmission.TestResults.Count != 3)
            {
                failures.Add("HTTP test results count is not 3.");
            }

            if (mongoSubmission.TestResultItems.Any(
                    result => result.Verdict != SubmissionVerdict.RuntimeError))
            {
                failures.Add("Mongo test result verdicts are not all RuntimeError.");
            }

            if (httpSubmission.TestResults.Any(
                    result => result.Verdict != SubmissionVerdict.RuntimeError))
            {
                failures.Add("HTTP test result verdicts are not all RuntimeError.");
            }

            if (mongoSubmission.TestResultItems.Any(
                    result => !string.IsNullOrWhiteSpace(result.CompileOutput)))
            {
                failures.Add("Mongo compile output should be empty for runtime error.");
            }

            if (httpSubmission.TestResults.Any(
                    result => !string.IsNullOrWhiteSpace(result.CompileOutput)))
            {
                failures.Add("HTTP compile output should be empty for runtime error.");
            }
        }

        if (existingAttemptOneSubmission is null ||
            existingAttemptOneSubmission.AttemptCount != 1 ||
            existingAttemptOneSubmission.Status != SubmissionProcessingStatus.Completed ||
            existingAttemptOneSubmission.FinalVerdict != SubmissionVerdict.Accepted ||
            existingAttemptOneSubmission.QuestionScore != 10)
        {
            failures.Add("AttemptCount 1 Java submission did not remain unchanged.");
        }

        if (existingAttemptTwoSubmission is null ||
            existingAttemptTwoSubmission.AttemptCount != 2 ||
            existingAttemptTwoSubmission.Status != SubmissionProcessingStatus.Completed ||
            existingAttemptTwoSubmission.FinalVerdict != SubmissionVerdict.Accepted ||
            existingAttemptTwoSubmission.QuestionScore != 10)
        {
            failures.Add("AttemptCount 2 Java submission did not remain unchanged.");
        }

        if (existingAttemptThreeSubmission is null ||
            existingAttemptThreeSubmission.AttemptCount != 3 ||
            existingAttemptThreeSubmission.Status != SubmissionProcessingStatus.Completed ||
            existingAttemptThreeSubmission.FinalVerdict != SubmissionVerdict.CompilationError ||
            existingAttemptThreeSubmission.QuestionScore != 0)
        {
            failures.Add("AttemptCount 3 Java submission did not remain unchanged.");
        }

        if (existingAttemptFourSubmission is null ||
            existingAttemptFourSubmission.AttemptCount != 4 ||
            existingAttemptFourSubmission.Status != SubmissionProcessingStatus.Completed ||
            existingAttemptFourSubmission.FinalVerdict != SubmissionVerdict.WrongAnswer ||
            existingAttemptFourSubmission.QuestionScore != 0)
        {
            failures.Add("AttemptCount 4 Java submission did not remain unchanged.");
        }

        if (existingCSubmission is null ||
            existingCSubmission.AttemptCount != 1 ||
            existingCSubmission.LanguageId != BootstrapManifest.CLanguageId ||
            existingCSubmission.Status != SubmissionProcessingStatus.Completed ||
            existingCSubmission.FinalVerdict != SubmissionVerdict.Accepted)
        {
            failures.Add("Existing completed C submission did not remain unchanged.");
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
