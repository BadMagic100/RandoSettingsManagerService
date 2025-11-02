using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using RandoSettingsManagerService.Model;
using System.Net;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace RandoSettingsManagerService;

public class Handlers
{
    private const int TTL_DAYS = 14;

    private static AmazonDynamoDBClient client = new();
    public static DynamoDBContext ctx = new(client);

    private bool IsBase64(string s)
    {
        Span<byte> buffer = new(new byte[s.Length]);
        if (Convert.TryFromBase64String(s, buffer, out int i))
        {
            return true;
        }
        return false;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest input, ILambdaContext context)
    {
        try
        {
            if (input.RequestContext.Http.Method == "POST")
            {
                if (TryParseInput(input.Body, out CreateSettingsInput? csi) && csi != null && csi.Settings != null)
                {
                    if (!IsBase64(csi.Settings))
                    {
                        return RespondError(HttpStatusCode.BadRequest);
                    }
                    CreateSettingsOutput output = await CreateSettings(csi, context);
                    return RespondOK(output);
                }
                else
                {
                    return RespondError(HttpStatusCode.BadRequest);
                }
            }
            else if (input.RequestContext.Http.Method == "GET")
            {
                if (TryParseInput(JsonSerializer.Serialize(input.QueryStringParameters), out RetrieveSettingsInput? rsi)
                    && rsi != null && !string.IsNullOrWhiteSpace(rsi.SettingsKey))
                {
                    try
                    {
                        RetrieveSettingsOutput output = await RetrieveSettings(rsi, context);
                        return RespondOK(output);
                    }
                    catch (AmazonDynamoDBException ddbe)
                    {
                        if (ddbe.StatusCode == HttpStatusCode.BadRequest)
                        {
                            context.Logger.LogWarning($"Caught 400 error during GET execution, this is likely due to caller error. {ddbe}");
                            return RespondError(HttpStatusCode.BadRequest);
                        }
                        throw;
                    }
                }
                else
                {
                    return RespondError(HttpStatusCode.BadRequest);
                }
            }
            else
            {
                return RespondError(HttpStatusCode.MethodNotAllowed);
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Unknown error caught in top-level handler: {ex}");
            return RespondError(HttpStatusCode.InternalServerError);
        }
    }

    private bool TryParseInput<T>(string body, out T? input)
    {
        try
        {
            input = JsonSerializer.Deserialize<T>(body);
            return input != null;
        }
        catch
        {
            input = default;
            return false;
        }
    }

    private APIGatewayHttpApiV2ProxyResponse RespondOK<T>(T body) => new()
    {
        StatusCode = (int)HttpStatusCode.OK,
        Body = JsonSerializer.Serialize(body),
        Headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json"
        }
    };

    private APIGatewayHttpApiV2ProxyResponse RespondError(HttpStatusCode statusCode) => new()
    {
        StatusCode = (int)statusCode
    };

    public async Task<CreateSettingsOutput> CreateSettings(CreateSettingsInput input, ILambdaContext context)
    {
        context.Logger.LogInformation($"Beginning create settings request. Encoded settings are \"{input.Settings}\"");
        long timestamp = DateTimeOffset.UtcNow.AddDays(TTL_DAYS).ToUnixTimeSeconds();
        string id = Guid.NewGuid().ToString();
        await ctx.SaveAsync(new SettingsRecord()
        {
            ID = id,
            ExpiryTimestamp = timestamp,
            Data = Convert.FromBase64String(input.Settings!)
        });
        return new CreateSettingsOutput(id);
    }

    public async Task<RetrieveSettingsOutput> RetrieveSettings(RetrieveSettingsInput input, ILambdaContext context)
    {
        context.Logger.LogInformation($"Beginning retrieve settings request. Key is \"{input.SettingsKey}\"");
        SettingsRecord? record = await ctx.LoadAsync<SettingsRecord>(input.SettingsKey);
        RetrieveSettingsOutput output = new();
        if (record != null && record.Data != null)
        {
            output.Found = true;
            output.Settings = Convert.ToBase64String(record.Data);
        }
        return output;
    }
}
