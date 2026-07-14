using System.Text.Json;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Centralized audit-log writer used by every service that mutates a sensitive
/// row. Captures the standard fields (school, user, action, entity, old/new
/// JSON values, reason, ip) in a single place so the Phase 10 audit-coverage
/// audit is easier to maintain.
///
/// Phase 10: introduced to FILL the audit-coverage gaps identified in the
/// Phase 10 hardening review — schools, users, user-school-roles, improvement
/// plans, follow-ups, and signatures now write rows here on every mutation.
/// </summary>
public class AuditLogWriter
{
    private readonly AlFalahDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLogWriter> _logger;

    public AuditLogWriter(
        AlFalahDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditLogWriter> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Stage a new <see cref="AuditLog"/> row in the same DbContext. The caller
    /// is responsible for the SaveChangesAsync so the row is committed in the
    /// same unit-of-work as the actual mutation (atomic write-or-rollback).
    /// </summary>
    public void Write(
        int? schoolId,
        string? userId,
        string action,
        string entityName,
        string? entityId,
        string? reason,
        object? oldValues = null,
        object? newValues = null)
    {
        try
        {
            _context.AuditLogs.Add(new AuditLog
            {
                SchoolId = schoolId,
                UserId = userId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
                NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
                Reason = reason,
                CreatedAt = DateTimeOffset.UtcNow,
                IpAddress = TryResolveClientIp(),
                UserAgent = TryResolveUserAgent()
            });
        }
        catch (Exception ex)
        {
            // Audit must never break the calling mutation — log + swallow.
            _logger.LogWarning(ex, "Failed to write audit row {Action} on {EntityName}#{EntityId}", action, entityName, entityId);
        }
    }

    /// <summary>
    /// Best-effort capture of the caller's IP. Reads X-Forwarded-For first
    /// (when behind a proxy), then falls back to the connection's remote IP.
    /// Returns null on any failure — failing to capture an IP is NOT a hard
    /// failure for audit; the row is still written.
    /// </summary>
    private string? TryResolveClientIp()
    {
        try
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null) return null;
            var xff = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(xff))
                return xff.Split(',')[0].Trim();
            return ctx.Connection?.RemoteIpAddress?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private string? TryResolveUserAgent()
    {
        try
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null) return null;
            var ua = ctx.Request.Headers.UserAgent.ToString();
            return string.IsNullOrEmpty(ua) ? null : ua;
        }
        catch
        {
            return null;
        }
    }
}