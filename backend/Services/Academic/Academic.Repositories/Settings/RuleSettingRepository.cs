using Academic.Repositories.Context;
using Microsoft.EntityFrameworkCore;

namespace Academic.Repositories.Settings;

public interface IRuleSettingRepository
{
    /// <summary>Dòng cấu hình duy nhất (id = 1), không theo dõi thay đổi; null khi DB chưa seed.</summary>
    Task<RuleSetting?> GetAsync(CancellationToken ct);

    /// <summary>Có theo dõi thay đổi, để sửa rồi gọi <see cref="SaveAsync"/>.</summary>
    Task<RuleSetting?> FindAsync(CancellationToken ct);

    void Add(RuleSetting setting);

    Task SaveAsync(CancellationToken ct);
}

public sealed class RuleSettingRepository(AcademicDbContext db) : IRuleSettingRepository
{
    public Task<RuleSetting?> GetAsync(CancellationToken ct) =>
        db.RuleSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == RuleSetting.SingletonId, ct);

    public Task<RuleSetting?> FindAsync(CancellationToken ct) =>
        db.RuleSettings.SingleOrDefaultAsync(s => s.Id == RuleSetting.SingletonId, ct);

    public void Add(RuleSetting setting) => db.RuleSettings.Add(setting);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
