using Application.Exceptions;

namespace Infrastructure.Judge0;

internal sealed class Judge0MultiFileProfileResolver
    : IJudge0MultiFileProfileResolver
{
    private const int CLanguageId = 50;
    private const int JavaLanguageId = 62;

    public Judge0MultiFileProfile Resolve(int originalLanguageId)
    {
        return originalLanguageId switch
        {
            CLanguageId => CreateCProfile(),
            JavaLanguageId => CreateJavaProfile(),
            _ => throw new CodeExecutionClientException(
                $"Multi-file execution is not configured for " +
                $"LanguageId {originalLanguageId}.",
                isTransient: false)
        };
    }

    private static Judge0MultiFileProfile CreateCProfile()
    {
        const string compileScript =
            """
            #!/usr/bin/env bash
            set -euo pipefail

            mapfile -d '' sources < <(find . -type f -name '*.c' -print0)

            if [ "${#sources[@]}" -eq 0 ]; then
              echo "No C source files were found." >&2
              exit 1
            fi

            gcc -O2 -std=c11 -Wall -Wextra \
              "${sources[@]}" \
              -o program
            """;

        const string runScript =
            """
            #!/usr/bin/env bash
            set -euo pipefail

            exec ./program
            """;

        return new Judge0MultiFileProfile(
            compileScript,
            runScript,
            RequiredEntryFile: null);
    }

    private static Judge0MultiFileProfile CreateJavaProfile()
    {
        const string compileScript =
            """
            #!/usr/bin/env bash
            set -euo pipefail

            mapfile -d '' sources < <(find . -type f -name '*.java' -print0)

            if [ "${#sources[@]}" -eq 0 ]; then
              echo "No Java source files were found." >&2
              exit 1
            fi

            javac "${sources[@]}"
            """;

        const string runScript =
            """
            #!/usr/bin/env bash
            set -euo pipefail

            exec java Main
            """;

        return new Judge0MultiFileProfile(
            compileScript,
            runScript,
            RequiredEntryFile: "Main.java");
    }
}