namespace AuthService.Domain.Models;

public enum BanStatus : byte
{
    MessagesBanned = 1,
    OnDemandPublicationBanned = 2,
    LiveStreamsBanned = 4,
    ProfileEditBanned = 8,
    CannotLogin = 16
}