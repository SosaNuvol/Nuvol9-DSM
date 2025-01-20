using Microsoft.Extensions.Configuration;

namespace NVL9.DSM.Core;

public static class ApplicationInformationManager
{
    private static readonly object _lock = new object();

    private static ApplicationInformation _appInfoInstance = null!;

    public static ApplicationInformation AppInfoInstance
    {
        get
        {
            lock (_lock)
            {
                if (_appInfoInstance == null)
                {
                    var builder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json");

                    var configuration = builder.Build();
                    var appInfo = new ApplicationInformation(configuration);

                    _appInfoInstance = new ApplicationInformation(configuration);
                }
                return _appInfoInstance;
            }
        }
    }

    public static void SetApplicationInformation(ApplicationInformation appInfo)
    {
        _appInfoInstance = appInfo;
    }
}