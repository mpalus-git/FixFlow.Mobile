using System.Text.Json.Serialization;

namespace FixFlow.Mobile.Core.Features.Auth;

public sealed record SessionUser(Guid Id, string Email, string FullName, string Role)
{
    public const string TechnicianRole = "Technician";

    [JsonIgnore]
    public bool IsTechnician => Role == TechnicianRole;
}
