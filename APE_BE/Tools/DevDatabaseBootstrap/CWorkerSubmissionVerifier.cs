using Domain.Entities;
using Domain.Enums;

namespace DevDatabaseBootstrap;

public static class CWorkerSubmissionVerifier
{
    public static CWorkerResultReport Validate(
        PE_Submission? submission,
        int duplicateSubmissionCount,
        User? student,
        PracticeSession? session,
        Exam? exam,
        PEQuestion? question)
    {
        var failures = new List<string>();

        if (submission is null)
        {
            failures.Add("No C fixture submission exists.");
            return new CWorkerResultReport(
                false,
                null,
                duplicateSubmissionCount,
                failures,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false);
        }

        if (duplicateSubmissionCount > 0)
        {
            failures.Add(
                $"Expected exactly one C fixture submission but found {duplicateSubmissionCount + 1}.");
        }

        if (submission.Status != SubmissionProcessingStatus.Completed)
        {
            failures.Add(
                $"C submission is not terminal Completed. Current status: {submission.Status}.");
        }

        if (submission.FinalVerdict != SubmissionVerdict.Accepted)
        {
            failures.Add(
                $"C submission final verdict is {submission.FinalVerdict?.ToString() ?? "null"}.");
        }

        if (submission.TestCasesPassed != 3 ||
            submission.TotalTestCases != 3)
        {
            failures.Add(
                $"C submission testcase counts are {submission.TestCasesPassed}/{submission.TotalTestCases}.");
        }

        if (submission.QuestionScore != submission.MaxScore)
        {
            failures.Add(
                $"C submission score is {submission.QuestionScore}/{submission.MaxScore}.");
        }

        if (submission.LeaseOwner is not null ||
            submission.LeaseAcquiredAt is not null ||
            submission.LeaseExpiresAt is not null)
        {
            failures.Add("C submission still retains lease fields after terminal completion.");
        }

        if (submission.LanguageId != BootstrapManifest.CLanguageId)
        {
            failures.Add(
                $"C submission LanguageId is {submission.LanguageId}.");
        }

        if (submission.AttemptCount != 1)
        {
            failures.Add(
                $"C submission AttemptCount is {submission.AttemptCount}.");
        }

        if (submission.Execution.AttemptCount <= 0)
        {
            failures.Add("C submission execution attempt count was not recorded.");
        }

        if (submission.ProcessingStartedAt is null)
        {
            failures.Add("C submission ProcessingStartedAt was not recorded.");
        }

        if (submission.CompletedAt is null)
        {
            failures.Add("C submission CompletedAt was not recorded.");
        }

        if (submission.SubmittedCode.Count != 1 ||
            !string.Equals(
                submission.SubmittedCode.First().Filename,
                BootstrapManifest.CSourceFilename,
                StringComparison.Ordinal))
        {
            failures.Add("C submission source file metadata is invalid.");
        }

        if (submission.TestResultItems.Count != 3 ||
            submission.TestResultItems.Any(item => item.Verdict != SubmissionVerdict.Accepted))
        {
            failures.Add("C submission test result verdicts are not all Accepted.");
        }

        if (submission.TestResultItems.Count(item => item.IsHidden) != 2)
        {
            failures.Add("C submission hidden testcase count changed unexpectedly.");
        }

        var activeLeaseRetained = submission.LeaseOwner is not null ||
                                  submission.LeaseAcquiredAt is not null ||
                                  submission.LeaseExpiresAt is not null;

        var studentRelationshipOk =
            student is not null &&
            string.Equals(student.Status, "Active", StringComparison.Ordinal) &&
            string.Equals(session?.StudentId, student.Id, StringComparison.Ordinal);

        if (!studentRelationshipOk)
        {
            failures.Add("C submission student relationship is invalid.");
        }

        var sessionRelationshipOk =
            session is not null &&
            session.Status == SessionStatus.InProgress &&
            string.Equals(session.ExamId, BootstrapManifest.CExamId, StringComparison.Ordinal);

        if (!sessionRelationshipOk)
        {
            failures.Add("C submission session relationship is invalid.");
        }

        var examQuestionRelationshipOk =
            exam is not null &&
            exam.PeExamQuestions.Any(item =>
                item.PeQuestionId == BootstrapManifest.CQuestionId &&
                item.AssignedPoints > 0);

        if (!examQuestionRelationshipOk)
        {
            failures.Add("C exam does not contain the expected C question linkage.");
        }

        var questionActiveOk =
            question is not null &&
            string.Equals(question.Status, "Active", StringComparison.Ordinal);

        if (!questionActiveOk)
        {
            failures.Add("C PEQuestion is not Active.");
        }

        var allowedLanguageOk =
            question is not null &&
            question.AllowedLanguageIds.Count == 1 &&
            question.AllowedLanguageIds[0] == BootstrapManifest.CLanguageId;

        if (!allowedLanguageOk)
        {
            failures.Add("C PEQuestion allowed-language configuration is invalid.");
        }

        var cppExcludedOk =
            question is not null &&
            !question.AllowedLanguageIds.Contains(52);

        if (!cppExcludedOk)
        {
            failures.Add("C PEQuestion unexpectedly allows C++ language id 52.");
        }

        var javaExcludedOk =
            submission.LanguageId != BootstrapManifest.JavaLanguageId;

        if (!javaExcludedOk)
        {
            failures.Add("C submission unexpectedly used Java language id 62.");
        }

        var expectedOutputLocalOnlyOk =
            question is not null &&
            question.TestCases.All(item => !string.IsNullOrWhiteSpace(item.ExpectedOutput));

        if (!expectedOutputLocalOnlyOk)
        {
            failures.Add("C PEQuestion no longer retains local ExpectedOutput values.");
        }

        return new CWorkerResultReport(
            failures.Count == 0,
            submission,
            duplicateSubmissionCount,
            failures,
            studentRelationshipOk,
            sessionRelationshipOk,
            examQuestionRelationshipOk,
            questionActiveOk,
            allowedLanguageOk,
            cppExcludedOk,
            javaExcludedOk,
            expectedOutputLocalOnlyOk,
            activeLeaseRetained,
            submission.Status == SubmissionProcessingStatus.Completed);
    }
}

public sealed record CWorkerResultReport(
    bool IsSuccess,
    PE_Submission? Submission,
    int DuplicateSubmissionCount,
    IReadOnlyList<string> Failures,
    bool StudentRelationshipOk,
    bool SessionRelationshipOk,
    bool ExamQuestionRelationshipOk,
    bool QuestionActiveOk,
    bool AllowedLanguageOk,
    bool CppExcludedOk,
    bool JavaExcludedOk,
    bool ExpectedOutputLocalOnlyOk,
    bool ActiveLeaseRetained,
    bool TerminalCompleted);
