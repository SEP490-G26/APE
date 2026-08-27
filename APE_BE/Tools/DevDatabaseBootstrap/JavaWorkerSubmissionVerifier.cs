using Domain.Entities;
using Domain.Enums;

namespace DevDatabaseBootstrap;

public static class JavaWorkerSubmissionVerifier
{
    public static JavaWorkerResultReport Validate(
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
            failures.Add("No Java fixture submission exists.");
            return new JavaWorkerResultReport(
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
                false);
        }

        if (duplicateSubmissionCount > 0)
        {
            failures.Add(
                $"Expected exactly one Java fixture submission but found {duplicateSubmissionCount + 1}.");
        }

        if (submission.Status != SubmissionProcessingStatus.Completed)
        {
            failures.Add(
                $"Java submission is not terminal Completed. Current status: {submission.Status}.");
        }

        if (submission.FinalVerdict != SubmissionVerdict.Accepted)
        {
            failures.Add(
                $"Java submission final verdict is {submission.FinalVerdict?.ToString() ?? "null"}.");
        }

        if (submission.TestCasesPassed != 3 ||
            submission.TotalTestCases != 3)
        {
            failures.Add(
                $"Java submission testcase counts are {submission.TestCasesPassed}/{submission.TotalTestCases}.");
        }

        if (submission.QuestionScore != submission.MaxScore)
        {
            failures.Add(
                $"Java submission score is {submission.QuestionScore}/{submission.MaxScore}.");
        }

        if (submission.LeaseOwner is not null ||
            submission.LeaseAcquiredAt is not null ||
            submission.LeaseExpiresAt is not null)
        {
            failures.Add("Java submission still retains lease fields after terminal completion.");
        }

        if (submission.LanguageId != BootstrapManifest.JavaLanguageId)
        {
            failures.Add(
                $"Java submission LanguageId is {submission.LanguageId}.");
        }

        if (submission.AttemptCount != 1)
        {
            failures.Add(
                $"Java submission AttemptCount is {submission.AttemptCount}.");
        }

        if (submission.Execution.AttemptCount <= 0)
        {
            failures.Add("Java submission execution attempt count was not recorded.");
        }

        if (submission.ProcessingStartedAt is null)
        {
            failures.Add("Java submission ProcessingStartedAt was not recorded.");
        }

        if (submission.CompletedAt is null)
        {
            failures.Add("Java submission CompletedAt was not recorded.");
        }

        if (submission.SubmittedCode.Count != 1 ||
            !string.Equals(
                submission.SubmittedCode.First().Filename,
                BootstrapManifest.JavaSourceFilename,
                StringComparison.Ordinal))
        {
            failures.Add("Java submission source file metadata is invalid.");
        }

        if (submission.TestResultItems.Count != 3 ||
            submission.TestResultItems.Any(item => item.Verdict != SubmissionVerdict.Accepted))
        {
            failures.Add("Java submission test result verdicts are not all Accepted.");
        }

        if (submission.TestResultItems.Count(item => item.IsHidden) != 2)
        {
            failures.Add("Java submission hidden testcase count changed unexpectedly.");
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
            failures.Add("Java submission student relationship is invalid.");
        }

        var sessionRelationshipOk =
            session is not null &&
            session.Status == SessionStatus.InProgress &&
            string.Equals(session.ExamId, BootstrapManifest.JavaExamId, StringComparison.Ordinal);

        if (!sessionRelationshipOk)
        {
            failures.Add("Java submission session relationship is invalid.");
        }

        var examQuestionRelationshipOk =
            exam is not null &&
            exam.PeExamQuestions.Any(item =>
                item.PeQuestionId == BootstrapManifest.JavaQuestionId &&
                item.AssignedPoints > 0);

        if (!examQuestionRelationshipOk)
        {
            failures.Add("Java exam does not contain the expected Java question linkage.");
        }

        var questionActiveOk =
            question is not null &&
            string.Equals(question.Status, "Active", StringComparison.Ordinal);

        if (!questionActiveOk)
        {
            failures.Add("Java PEQuestion is not Active.");
        }

        var allowedLanguageOk =
            question is not null &&
            question.AllowedLanguageIds.Count == 1 &&
            question.AllowedLanguageIds[0] == BootstrapManifest.JavaLanguageId;

        if (!allowedLanguageOk)
        {
            failures.Add("Java PEQuestion allowed-language configuration is invalid.");
        }

        var cppExcludedOk =
            question is not null &&
            !question.AllowedLanguageIds.Contains(52);

        if (!cppExcludedOk)
        {
            failures.Add("Java PEQuestion unexpectedly allows C++ language id 52.");
        }

        var expectedOutputLocalOnlyOk =
            question is not null &&
            question.TestCases.All(item => !string.IsNullOrWhiteSpace(item.ExpectedOutput));

        if (!expectedOutputLocalOnlyOk)
        {
            failures.Add("Java PEQuestion no longer retains local ExpectedOutput values.");
        }

        return new JavaWorkerResultReport(
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
            expectedOutputLocalOnlyOk,
            activeLeaseRetained,
            submission.Status == SubmissionProcessingStatus.Completed);
    }
}
