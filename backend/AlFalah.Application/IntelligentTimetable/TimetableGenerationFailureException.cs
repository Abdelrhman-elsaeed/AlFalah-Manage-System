namespace AlFalah.Application.IntelligentTimetable;

public enum TimetableGenerationFailureCode
{
    Infeasible,
    SearchLimitReached
}

public sealed class TimetableGenerationFailureException : ArgumentException
{
    public TimetableGenerationFailureException(TimetableGenerationFailureCode code, string message)
        : base(message) => Code = code;

    public TimetableGenerationFailureCode Code { get; }
}
