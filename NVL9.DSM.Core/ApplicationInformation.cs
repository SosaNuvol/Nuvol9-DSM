namespace NVL9.DSM.Core;

using Microsoft.Extensions.Configuration;

public interface IApplicationInformation
{
    string ApplicationId { get; }
    string HostName { get; }
}

public class ApplicationInformation : IApplicationInformation
{
    private readonly IConfiguration _configuration;

    public string ApplicationId
    {
        get
        {
            return _configuration.GetSection("PBCSettings:CP:APPLICATION_ID").Value ?? "NOTSET";
        }
    }

    public string HostName
    {
        get
        {
            return _configuration.GetSection("PBCSettings:CP:HOST_NAME").Value ?? "NOTSET";
        }
    }

    public string ConnectionString
    {
        get
        {
            return _configuration.GetSection("PBCSettings:CP:CONN_STR").Value ?? "NOTSET";
        }
    }

    public ApplicationInformation(IConfiguration configuration)
    {
        _configuration = configuration;
    }
}