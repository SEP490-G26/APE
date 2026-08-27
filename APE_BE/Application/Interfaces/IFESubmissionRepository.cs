using Domain.Entities;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface IFESubmissionRepository
{
    Task CreateAsync(FE_Submission submission);
    Task<FE_Submission?> GetByIdAsync(string id);
    Task<List<FE_Submission>> GetBySessionIdAsync(string sessionId);
    Task<List<FE_Submission>> GetByUserIdAsync(string userId);   
    Task<int> CountByUserIdAsync(string userId);      
    
}