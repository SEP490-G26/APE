using Application.Common;
using Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IPESubmissionService
    {
        Task<ApiResponse<SubmissionAcceptedDto>> SubmitAsync(
            string studentId,
            PESubmissionInputDto request,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<PESubmissionDetailDto>> GetByIdAsync(
            string studentId,
            string submissionId,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<IReadOnlyCollection<PESubmissionSummaryDto>>> GetBySessionAsync(
            string studentId,
            string sessionId,
            CancellationToken cancellationToken = default);
    }
}
