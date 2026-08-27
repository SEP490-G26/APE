using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Enums
{
    public enum SubmissionVerdict
    {
        Accepted = 0,
        WrongAnswer = 1,
        CompilationError = 2,
        RuntimeError = 3,
        TimeLimitExceeded = 4,
        MemoryLimitExceeded = 5,
        OutputLimitExceeded = 6,
        SystemError = 7
    }
}
