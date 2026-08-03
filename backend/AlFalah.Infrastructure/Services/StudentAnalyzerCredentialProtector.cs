using Microsoft.AspNetCore.DataProtection;

namespace AlFalah.Infrastructure.Services;

public sealed class StudentAnalyzerCredentialProtector
{
    private const string Purpose = "AlFalah.StudentAnalyzer.ProviderCredentials.v1";
    private readonly IDataProtector _protector;

    public StudentAnalyzerCredentialProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector(Purpose);

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext)
    {
        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "تعذر فك تشفير مفتاح مزود الذكاء الاصطناعي. يجب إعادة إدخال المفتاح من إعدادات محلل الطلاب.", ex);
        }
    }
}
