using AlFalah.Application.Duty;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.Duty;
[Collection("Duty SQL")]
public sealed class DutyAdministrationSqlTests(DutySqlFixture fixture)
{
 private async Task<DutyExecutionSqlTests.Scene> Seed()=>await new DutyExecutionSqlTests(fixture).Seed();
 private static DutyAdministrationService Administration(AlFalah.Infrastructure.Data.AlFalahDbContext db,DutyExecutionSqlTests.Scene s,string? actor=null)=>new(new DutyRepository(db),new DutyAdministrationRepository(db),new TeacherDriveHarness.TestCurrentUser(RoleNames.SchoolManager,actor??s.Manager,s.School,true));
 [DutySqlFact]public async Task Scoped_grant_and_revoke_change_live_access_and_identity_without_global_storage_signing_or_delegation()
 {
  var s=await Seed();await using var db=fixture.Db();var service=Administration(db,s);
  (await service.AccountsAsync(default)).Should().Contain(x=>x.UserId==s.Guardian&&x.Eligible);
  var before=(await db.RolePermissions.CountAsync());var initialSchools=await db.UserSchoolRoles.CountAsync(x=>x.UserId==s.Replacement&&x.RoleId!=DutyPermissions.AdministratorRole);
  await service.ChangeAsync(s.Replacement,new(true),default);
  (await new DutyRepository(db).AccessAsync(s.School,s.Replacement,default))!.Manager.Should().BeTrue();
  var target=new TeacherDriveHarness.TestCurrentUser(RoleNames.Instructor,s.Replacement,s.School,true);
  var planner=new DutyService(new DutyRepository(db),target);(await planner.WorkspaceAsync(s.Term,default)).CanManage.Should().BeTrue();(await planner.DashboardAsync(default)).Current.Should().NotBeNull();
  await FluentActions.Invoking(()=>Administration(db,s,s.Replacement).ChangeAsync(s.Teacher,new(true),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
  var execution=new DutyExecutionService(new DutyRepository(db),new DutyExecutionRepository(db),target);
  await FluentActions.Invoking(()=>execution.AcknowledgeAsync(s.Assignment,new(1,Guid.NewGuid()),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
  (await db.UserRoles.AnyAsync(x=>x.UserId==s.Replacement&&x.RoleId==DutyPermissions.AdministratorRole)).Should().BeFalse();
  var reserved=await db.RolePermissions.Where(x=>x.RoleId==DutyPermissions.AdministratorRole).Select(x=>x.Permission.Name).ToArrayAsync();reserved.Should().BeEquivalentTo([DutyPermissions.View,DutyPermissions.Manage]);
  using var manager=new UserManager<ApplicationUser>(new UserStore<ApplicationUser,ApplicationRole,AlFalah.Infrastructure.Data.AlFalahDbContext>(db),Options.Create(new IdentityOptions()),new PasswordHasher<ApplicationUser>(),[],[],new UpperInvariantLookupNormalizer(),new IdentityErrorDescriber(),null!,NullLogger<UserManager<ApplicationUser>>.Instance);
  var auth=new AuthService(manager,db,null!,NullLogger<AuthService>.Instance,target,new DutyRepository(db));
  (await auth.GetCurrentUserAsync(s.Replacement)).Permissions.Should().Contain(DutyPermissions.Manage).And.NotContain("Storage.ManageSchool");
  var grant=(await service.AccountsAsync(default)).Single(x=>x.UserId==s.Replacement);
  await service.ChangeAsync(s.Replacement,new(false,grant.Version),default);
  (await new DutyRepository(db).AccessAsync(s.School,s.Replacement,default))!.Manager.Should().BeFalse();
  (await auth.GetCurrentUserAsync(s.Replacement)).Permissions.Should().NotContain(DutyPermissions.Manage).And.Contain(DutyPermissions.View);
  (await db.AuditLogs.CountAsync(x=>x.SchoolId==s.School&&x.EntityName=="DutyAdministrator")).Should().Be(2);
  (await db.UserSchoolRoles.CountAsync(x=>x.UserId==s.Replacement&&x.RoleId!=DutyPermissions.AdministratorRole)).Should().Be(initialSchools);
  await service.ChangeAsync(s.Guardian,new(true),default);(await new DutyRepository(db).AccessAsync(s.School,s.Guardian,default))!.Manager.Should().BeTrue();
 }
 [DutySqlFact]public async Task Concurrent_duplicate_grants_are_one_row_one_audit_and_stale_regrant_cannot_override_revocation()
 {
  var s=await Seed();
  async Task Grant(){await using var isolated=fixture.Db();await Administration(isolated,s).ChangeAsync(s.Replacement,new(true),default);}
  await Task.WhenAll(Grant(),Grant());await using var db=fixture.Db();var service=Administration(db,s);
  (await db.UserSchoolRoles.CountAsync(x=>x.SchoolId==s.School&&x.UserId==s.Replacement&&x.RoleId==DutyPermissions.AdministratorRole)).Should().Be(1);
  (await db.AuditLogs.CountAsync(x=>x.SchoolId==s.School&&x.Action=="DutyAdministratorGranted")).Should().Be(1);
  var old=(await service.AccountsAsync(default)).Single(x=>x.UserId==s.Replacement);await service.ChangeAsync(s.Replacement,new(false,old.Version),default);
  await FluentActions.Invoking(()=>service.ChangeAsync(s.Replacement,new(true,old.Version),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==409);
  var current=(await service.AccountsAsync(default)).Single(x=>x.UserId==s.Replacement);await service.ChangeAsync(s.Replacement,new(true,current.Version),default);
  (await db.AuditLogs.CountAsync(x=>x.SchoolId==s.School&&x.EntityName=="DutyAdministrator")).Should().Be(3);
 }
 [DutySqlFact]public async Task Foreign_inactive_and_role_only_accounts_cannot_be_granted_and_membership_loss_disables_reserved_grant()
 {
  var s=await Seed();var other=await Seed();await using var db=fixture.Db();var service=Administration(db,s);
  await FluentActions.Invoking(()=>service.ChangeAsync(other.Replacement,new(true),default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==404);
  await db.Users.Where(x=>x.Id==s.Replacement).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.IsActive,false));
  await FluentActions.Invoking(()=>service.ChangeAsync(s.Replacement,new(true),default)).Should().ThrowAsync<DutyException>();
  await db.Users.Where(x=>x.Id==s.Replacement).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.IsActive,true));
  await service.ChangeAsync(s.Replacement,new(true),default);
  await db.UserSchoolRoles.Where(x=>x.UserId==s.Replacement&&x.SchoolId==s.School&&x.RoleId!=DutyPermissions.AdministratorRole).ExecuteUpdateAsync(x=>x.SetProperty(m=>m.IsActive,false));
  (await new DutyRepository(db).AccessAsync(s.School,s.Replacement,default))!.Manager.Should().BeFalse();
  var invalid=Administration(db,s,other.Manager);await FluentActions.Invoking(()=>invalid.AccountsAsync(default)).Should().ThrowAsync<DutyException>().Where(x=>x.Status==403);
  (await new DutyRepository(db).AccessAsync(other.School,s.Replacement,default))!.Manager.Should().BeFalse();
 }
 [DutySqlFact]public async Task SQL_failure_rolls_back_grant_and_its_audit_together()
 {
  var s=await Seed();await using var db=fixture.Db();await db.Database.ExecuteSqlRawAsync("CREATE OR ALTER TRIGGER TR_TestDutyAdminAudit ON AuditLogs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityName='DutyAdministrator') THROW 51992,'Synthetic audit failure',1; END");
  try{await FluentActions.Invoking(()=>Administration(db,s).ChangeAsync(s.Replacement,new(true),default)).Should().ThrowAsync<DbUpdateException>();}
  finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER TR_TestDutyAdminAudit");}
  (await db.UserSchoolRoles.AnyAsync(x=>x.SchoolId==s.School&&x.UserId==s.Replacement&&x.RoleId==DutyPermissions.AdministratorRole)).Should().BeFalse();
  (await db.AuditLogs.AnyAsync(x=>x.SchoolId==s.School&&x.EntityName=="DutyAdministrator")).Should().BeFalse();
 }
}
