namespace NVL9.DSM.Core;

using Microsoft.AspNetCore.Http;

public class DSMAccessor
{
    private HttpContext httpContext;

    public HttpContext HttpContext => httpContext;

    public string Audience => httpContext.User.Claims.FirstOrDefault(c => c.Type == "aud")?.Value;

    public Guid SubscriberId
    {
        get
        {
            var subKey = GetSubscriberKey();
            var subClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == subKey)?.Value;
            return subClaim != null ? Guid.Parse(subClaim) : Guid.Empty;
        }
    }

    private DSMAccessor(HttpContext httpContext)
    {
        this.httpContext = httpContext;
    }

    public static DSMAccessor Instance { get; private set; }

    public static void Init(HttpContext httpContext)
    {
        Instance = new DSMAccessor(httpContext);
    }

    public string GetSubscriberKey()
    {
        var iss = httpContext.User.Claims.FirstOrDefault(c => c.Type == "iss")?.Value;

        switch (iss)
        {
            case "https://qadidentity-dev.nuvol9.com":
                return "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
            case "https://accounts.google.com":
                return "Google";
            default:
                return "Unknown";
        }
    }

}
