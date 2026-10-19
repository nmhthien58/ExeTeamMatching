using ExeTeamMatching.Protos.Matching.V1;

namespace Matching.Grpc.Services;

// Khung ban đầu (BE-04). Các RPC chưa override sẽ trả StatusCode.Unimplemented;
// SuggestMembers, SuggestTeams, ValidateComposition, PlanAutoAssignment làm ở BE-36..BE-38.
public class MatchingGrpcService : MatchingService.MatchingServiceBase
{
}
