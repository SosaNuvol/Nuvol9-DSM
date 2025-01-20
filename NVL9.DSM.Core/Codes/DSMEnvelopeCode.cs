namespace NVL9.DSM.Core.Codes;

public class DSMEnvelopeCode
{
    public int Code { get; private set; }

    public string StatusCode { get; private set; }

    public string HttpStatus { get; private set; }

    public string ErrorMessage { get; private set; }

    public string Notes { get; private set; }

    public DSMEnvelopeCode(int code, string statusCode, string httpStatus, string errorMessage, string notes)
    {
        Code = code;
        StatusCode = statusCode;
        HttpStatus = httpStatus;
        ErrorMessage = errorMessage;
        Notes = notes;
    }

    public override string ToString()
    {
        return StatusCode;
    }
}
