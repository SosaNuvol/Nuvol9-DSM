namespace NVL9.DSM.Core.Codes;

public class DSMEnvelopeCodeManager
{
    // Add your class members and methods here
    private Dictionary<DSMEnvelopeCodeEnum, DSMEnvelopeCode> _dict = new Dictionary<DSMEnvelopeCodeEnum, DSMEnvelopeCode>();


    public static DSMEnvelopeCodeManager Manager = new DSMEnvelopeCodeManager();

    public DSMEnvelopeCodeManager()
    {
        _init();
    }

    private void _init()
    {
        // General Messages
        _dict.Add(DSMEnvelopeCodeEnum.GEN_COMMON_00000, new DSMEnvelopeCode((int)DSMEnvelopeCodeEnum.GEN_COMMON_00000, "GEN-COMMON-00000", "200", "Success", "No Errors"));
        _dict.Add(DSMEnvelopeCodeEnum.GEN_COMMON_00001, new DSMEnvelopeCode((int)DSMEnvelopeCodeEnum.GEN_COMMON_00001, "GEN-COMMON-00001", null!, "General initialization of Code Block.", null!));


        // Appliation Validation
        _dict.Add(DSMEnvelopeCodeEnum.API_APPVLD_02000, new DSMEnvelopeCode((int)DSMEnvelopeCodeEnum.API_APPVLD_02000, "API-APPVLD-02000", "404", "General application validation error.", null!));
        _dict.Add(DSMEnvelopeCodeEnum.API_APPVLD_02001, new DSMEnvelopeCode((int)DSMEnvelopeCodeEnum.API_APPVLD_02001, "API-APPVLD-02001", "404", "Invalid application arguments.", null!));

        // Security
        _dict.Add(DSMEnvelopeCodeEnum.API_COMMON_01000, new DSMEnvelopeCode((int)DSMEnvelopeCodeEnum.API_COMMON_01000, "API-COMMON-01000", "401", "Authentication Failure.", null!));
        _dict.Add(DSMEnvelopeCodeEnum.API_COMMON_01001, new DSMEnvelopeCode((int)DSMEnvelopeCodeEnum.API_COMMON_01001, "API-COMMON-01001", "401", "Token Generation Error.", null!));

        // Database
        _dict.Add(DSMEnvelopeCodeEnum.API_DATABASE_03020, new DSMEnvelopeCode((int)DSMEnvelopeCodeEnum.API_DATABASE_03020, "API-DATABASE-03020", "500", "Execution Error.", null!));
        _dict.Add(DSMEnvelopeCodeEnum.API_DATABASE_03021, new DSMEnvelopeCode((int)DSMEnvelopeCodeEnum.API_DATABASE_03021, "API-DATABASE-03021", "401", "SESSION CONTEXT User Not Found.", null!));
        _dict.Add(DSMEnvelopeCodeEnum.API_DATABASE_03022, new DSMEnvelopeCode((int)DSMEnvelopeCodeEnum.API_DATABASE_03022, "API-DATABASE-03022", "403", "User does not have access to these resources.", null!));
    }

    public DSMEnvelopeCode Find(DSMEnvelopeCodeEnum codeEnum)
    {
        return _dict[codeEnum];
    }
}
