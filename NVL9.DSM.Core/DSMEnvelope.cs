namespace NVL9.DSM.Core;

using NVL9.DSM.Core.Codes;
using NVL9.DSM.Core.Models;
using System.Diagnostics;

public partial class DSMEnvelope<T> : IDSMEnvelope
{
    private const short _MAX_RANDOM_NUMBER = short.MaxValue;
    private const short _MIN_RANDOM_NUMBER = 999;
    private const int _ONE_DEEP_INTO_STACK = 2;

    private const int SUCCESS_CODE = 0;
    private const int INITIAL_CODE = 1;

    private static ApplicationInformation _applicationInformation
    {
        get
        {
            return ApplicationInformationManager.AppInfoInstance;
        }

    }

    public string UniqueIEID { get; private set; } = null!;

    public string ErrorIEID { get; private set; } = null!;

    public string? ApiTraceId { get; private set; } = null!;

    public string? IdempotencyKeyId { get; private set; } = null!;

    public string DTOMessage { get; private set; } = null!;

    public string Notes { get; private set; } = null!;

    public DSMEnvelopeCode Code { get; private set; } = null!;

    public string ObjectSignature { get; private set; } = null!;

    public string RawJson { get; set; } = null!;

    public string RootEnvelopID { get; private set; } = null!;

    public string ParentEnvelopID { get; private set; } = null!;

    public bool FreezeStatus { get; private set; } = false;

    public string ExecutionTime { get; private set; } = null!;

    public DateTimeOffset StartTime { get; private set; }

    public DateTimeOffset EndTime { get; private set; }

    public CodeBlock CodeBlockInfo { get; private set; } = null!;

    public Dictionary<string, List<string>>? ValidationErrors { get; private set; } = null!;

    private Stopwatch _stopWatch = null!;

    private int _pid;

    private bool _showArguments = false;

    private IList<string> _obfuscatedListOfArguments = null!;

    public T Value { get; private set; }

    private DSMEnvelope()
    {
        _initEnvelop();
    }

    private DSMEnvelope(bool showArguments = false, IList<string> obfuscatedListOfArguments = null!)
    {
        _initEnvelop();
        _obfuscatedListOfArguments = obfuscatedListOfArguments;
        _showArguments = showArguments;
    }


    private static void _initProvidedParams(object[] providedParams, out bool showArguments, out IList<string> obfuscatedListOfArguments)
    {
        showArguments = false;
        obfuscatedListOfArguments = new List<string>();

        // Find showArguments
        foreach (var item in providedParams)
        {
            if (nameof(item).Equals("showArguments")) showArguments = (bool)item;

            if (nameof(item).Equals("obfuscatedListOfArguments")) obfuscatedListOfArguments = item as IList<string> ?? new List<string>();
        }
    }

    private void _initEnvelop()
    {
        Code = DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.GEN_COMMON_00001);
        DTOMessage = Code.ErrorMessage;
        _stopWatch = Stopwatch.StartNew();
        StartTime = DateTimeOffset.Now;

        var rnd = new Random();
        _pid = rnd.Next(_MIN_RANDOM_NUMBER, _MAX_RANDOM_NUMBER);

        UniqueIEID = _generateUniqueIEID();
        RootEnvelopID = _generateReadableIEID(StartTime);
    }

    private string _generateUniqueIEID()
    {
        var result = $"{Guid.NewGuid()}";
        result += $"|{_pid}";

        return result;
    }

    private string _generateReadableIEID(DateTimeOffset dateTimeStamp)
    {
        var offsetInMinutes = _convertOffsetToMinutes(dateTimeStamp.Offset.ToString());
        var result = $"{_applicationInformation.ApplicationId}";
        result += $"|{_applicationInformation.HostName}";
        result += $"|{dateTimeStamp.Year:D4}";
        result += $":{dateTimeStamp.Month:D2}";
        result += $":{dateTimeStamp.Day:D2}";
        result += $":{dateTimeStamp.Hour:D2}";
        result += $":{dateTimeStamp.Minute:D2}";
        result += $":{dateTimeStamp.Second:D2}";
        result += $":{dateTimeStamp.Millisecond:D2}";
        result += $":{offsetInMinutes}|{_pid:D5}";

        return result;
    }

    private string _convertOffsetToMinutes(string offsetStr)
    {
        var components = offsetStr.Split(':');

        if (short.TryParse(components[0], out var hours))
        {
            hours = (short)(hours * 60);
        }

        if (!short.TryParse(components[1], out var minutes))
        {
            minutes = 0;
        }

        var result = hours > 0
            ? hours + minutes
            : hours - minutes;

        var resultStr = result < 0
            ? result.ToString()
            : $"+{result.ToString()}";

        return resultStr;
    }

    private void _calculateExecutionTime()
    {
        ExecutionTime = _stopWatch.ElapsedMilliseconds.ToString();
        EndTime = DateTimeOffset.Now;
    }

    private void _setForgroundColor()
    {
        switch (Code.Code)
        {
            case INITIAL_CODE:
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;

            case SUCCESS_CODE:
                Console.ForegroundColor = ConsoleColor.Green;
                break;

            default:
                Console.ForegroundColor = ConsoleColor.Red;
                break;
        }
    }
}
