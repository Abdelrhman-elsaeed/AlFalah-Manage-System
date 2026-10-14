using AlFalah.Application.Duty;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class DutyAdministrationRepository(AlFalahDbContext db):IDutyAdministrationRepository
{
    public async Task<bool> CanDelegateAsync(int school,string user,CancellationToken ct)=>
        (await new DutyRepository(db).AccessAsync(school,user,ct))?.Manager==true&&
        await db.UserSchoolRoles.AnyAsync(x=>x.SchoolId==school&&x.UserId==user&&x.IsActive&&x.Role.Name==RoleNames.SchoolManager,ct);
    public async Task<IReadOnlyList<DutyAdministratorAccount>> AccountsAsync(int school,CancellationToken ct)
    {
        var memberships=await (from m in db.UserSchoolRoles.IgnoreQueryFilters().AsNoTracking()
            join u in db.Users.IgnoreQueryFilters().AsNoTracking() on m.UserId equals u.Id
            where m.SchoolId==school
            select new{m.UserId,Name=u.FirstName+" "+u.LastName,Username=u.UserName,Role=m.Role.Name,m.RoleId,m.IsActive,m.IsDeleted,m.UpdatedAt,RoleActive=m.Role.IsActive,UserActive=u.IsActive&&!u.IsDeleted}).ToListAsync(ct);
        var administrationRoles=await db.RolePermissions.AsNoTracking().Where(x=>x.Permission.Name==DutyPermissions.Manage&&x.RoleId!=DutyPermissions.AdministratorRole).Select(x=>x.RoleId).ToListAsync(ct);
        return memberships.GroupBy(x=>x.UserId).Select(g=>{
            var normal=g.Where(x=>x.RoleId!=DutyPermissions.AdministratorRole&&x.IsActive&&!x.IsDeleted&&x.RoleActive).ToArray();
            var grant=g.SingleOrDefault(x=>x.RoleId==DutyPermissions.AdministratorRole);
            return new DutyAdministratorAccount(g.Key,g.First().Name,normal.Select(x=>x.Role??"").Distinct().Order().ToArray(),g.First().UserActive&&normal.Length>0,
                normal.Any(x=>administrationRoles.Contains(x.RoleId)),grant is{IsActive:true,IsDeleted:false},grant?.UpdatedAt,g.First().Username);
        }).Where(x=>x.Eligible||x.Enabled).OrderBy(x=>x.Name).ThenBy(x=>x.UserId).ToArray();
    }
    public Task<UserSchoolRole?> GrantAsync(int school,string user,CancellationToken ct)=>db.UserSchoolRoles.IgnoreQueryFilters().AsTracking().SingleOrDefaultAsync(x=>x.SchoolId==school&&x.UserId==user&&x.RoleId==DutyPermissions.AdministratorRole,ct);
    public async Task ProvisionRoleAsync(CancellationToken ct)
    {
        // All school transactions use the same reserved-role lock before provisioning.
        if(db.Database.IsSqlServer())await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='AlFalah:DutyAdministratorRole',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Duty role lock timeout',1;",ct);
        var role=await db.Roles.SingleOrDefaultAsync(x=>x.Id==DutyPermissions.AdministratorRole,ct);
        if(role is null){db.Roles.Add(new(){Id=DutyPermissions.AdministratorRole,Name=DutyPermissions.AdministratorRole,NormalizedName=DutyPermissions.AdministratorRole.ToUpperInvariant(),DescriptionAr="مسؤول المناوبة",DescriptionEn="School duty administrator"});await db.SaveChangesAsync(ct);}
        else if(role.Name!=DutyPermissions.AdministratorRole||!role.IsActive)throw new DutyException("DutyDelegationSetup","إعداد صلاحيات المناوبة غير متاح. / Duty administration setup unavailable.",409);
        var permissions=await db.Permissions.Where(x=>x.Name==DutyPermissions.Manage||x.Name==DutyPermissions.View).Select(x=>x.Id).ToListAsync(ct);
        if(permissions.Count!=2)throw new DutyException("DutyDelegationSetup","إعداد صلاحيات المناوبة غير متاح. / Duty administration setup unavailable.",409);
        var existing=await db.RolePermissions.Where(x=>x.RoleId==DutyPermissions.AdministratorRole).Select(x=>x.PermissionId).ToListAsync(ct);
        if(existing.Except(permissions).Any())throw new DutyException("DutyDelegationSetup","راجع إعداد صلاحيات المناوبة. / Review duty administration setup.",409);
        foreach(var permission in permissions.Except(existing))db.RolePermissions.Add(new(){RoleId=DutyPermissions.AdministratorRole,PermissionId=permission});
        await db.SaveChangesAsync(ct);
    }
    public void Audit(int school,string actor,string target,bool before,bool after)=>db.AuditLogs.Add(new(){SchoolId=school,UserId=actor,Action=after?"DutyAdministratorGranted":"DutyAdministratorRevoked",EntityName="DutyAdministrator",EntityId=target,OldValues=$"{{\"enabled\":{before.ToString().ToLowerInvariant()}}}",NewValues=$"{{\"enabled\":{after.ToString().ToLowerInvariant()}}}"});
}
