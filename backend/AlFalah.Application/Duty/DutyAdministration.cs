using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;

namespace AlFalah.Application.Duty;

public sealed record DutyAdministratorAccount(string UserId,string Name,IReadOnlyList<string> Roles,bool Eligible,bool Inherited,bool Enabled,DateTimeOffset? Version,string? Username=null);
public sealed record DutyAdministratorChange(bool Enabled,DateTimeOffset? ExpectedVersion=null);
public interface IDutyAdministrationRepository
{
    Task<bool> CanDelegateAsync(int school,string user,CancellationToken ct);
    Task<IReadOnlyList<DutyAdministratorAccount>> AccountsAsync(int school,CancellationToken ct);
    Task<UserSchoolRole?> GrantAsync(int school,string user,CancellationToken ct);
    Task ProvisionRoleAsync(CancellationToken ct);
    void Audit(int school,string actor,string target,bool before,bool after);
}
public interface IDutyAdministrationService
{
    Task<IReadOnlyList<DutyAdministratorAccount>> AccountsAsync(CancellationToken ct);
    Task<IReadOnlyList<DutyAdministratorAccount>> ChangeAsync(string user,DutyAdministratorChange change,CancellationToken ct);
}
public sealed class DutyAdministrationService(IDutyRepository duty,IDutyAdministrationRepository repo,ICurrentUserService current):IDutyAdministrationService
{
    private int School=>current.ActiveSchoolId??throw Denied();
    private string Actor=>current.UserId??throw Denied();
    private static DutyException Denied()=>new("DutyDelegationDenied","إدارة صلاحيات المناوبة متاحة لمدير المدرسة فقط. / Only the school manager can manage duty administrators.",403);
    private async Task Access(CancellationToken ct){if(!await repo.CanDelegateAsync(School,Actor,ct))throw Denied();}
    public async Task<IReadOnlyList<DutyAdministratorAccount>> AccountsAsync(CancellationToken ct){await Access(ct);var rows=await repo.AccountsAsync(School,ct);await Access(ct);return rows;}
    public async Task<IReadOnlyList<DutyAdministratorAccount>> ChangeAsync(string user,DutyAdministratorChange change,CancellationToken ct)
    {
        await Access(ct);
        await duty.AtomicAsync(School,async()=>{
            await Access(ct);
            var target=(await repo.AccountsAsync(School,ct)).SingleOrDefault(x=>x.UserId==user)??throw new DutyException("DutyAccountUnavailable","الحساب غير متاح في هذه المدرسة. / Account unavailable in this school.",404);
            if(target.Inherited)throw new DutyException("DutyInheritedAccess","هذه الصلاحية مرتبطة بدور الحساب. / This account has role-based duty access.",409);
            if(change.Enabled&&!target.Eligible)throw new DutyException("DutyAccountUnavailable","اختر حسابًا نشطًا في المدرسة. / Choose an active school account.",409);
            var grant=await repo.GrantAsync(School,user,ct);var before=grant is{IsActive:true,IsDeleted:false};
            if(before==change.Enabled)return true;
            if(grant?.UpdatedAt!=change.ExpectedVersion)throw new DutyException("DutyDelegationConflict","تغيّرت الصلاحية. حدّث القائمة ثم أعد المحاولة. / Access changed. Refresh the list and try again.",409);
            if(change.Enabled)await repo.ProvisionRoleAsync(ct);
            if(grant is null){grant=new(){SchoolId=School,UserId=user,RoleId=DutyPermissions.AdministratorRole,CreatedByUserId=Actor};duty.Add(grant);}
            grant.IsActive=change.Enabled;grant.IsDeleted=false;grant.UpdatedAt=DateTimeOffset.UtcNow;grant.UpdatedByUserId=Actor;
            repo.Audit(School,Actor,user,before,change.Enabled);await Access(ct);return true;
        },ct);
        return await AccountsAsync(ct);
    }
}
