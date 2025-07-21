using ModelContextProtocol.Server;
using System.ComponentModel;
using DotNut.Api;
using DotNut.ApiModels;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Add HttpClient for Cashu API calls
builder.Services.AddHttpClient();

await builder.Build().RunAsync();

[McpServerToolType]
public static class CashuMintTools
{
    [McpServerTool, Description("Get information about a Cashu mint including name, version, contact info, and supported features.")]
    public static async Task<string> GetMintInfo(string mintUrl)
    {
        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(mintUrl) };
            var cashuClient = new CashuHttpClient(httpClient);
            
            var info = await cashuClient.GetInfo();
            
            return JsonSerializer.Serialize(new
            {
                name = info.Name,
                version = info.Version,
                description = info.Description,
                description_long = info.DescriptionLong,
                pubkey = info.Pubkey,
                contact = info.Contact,
                motd = info.Motd,
                icon_url = info.IconUrl,
                time = info.Time,
                tos_url = info.TosUrl,
                nuts = info.Nuts?.Keys.ToArray()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool, Description("Get all active keysets from a Cashu mint with their IDs, units, and fee information.")]
    public static async Task<string> GetMintKeysets(string mintUrl)
    {
        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(mintUrl) };
            var cashuClient = new CashuHttpClient(httpClient);
            
            var keysets = await cashuClient.GetKeysets();
            
            return JsonSerializer.Serialize(new
            {
                keysets = keysets.Keysets.Select(k => new
                {
                    id = k.Id.ToString(),
                    unit = k.Unit,
                    active = k.Active,
                    input_fee_ppk = k.InputFee
                })
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool, Description("Get the cryptographic keys for all active keysets from a Cashu mint.")]
    public static async Task<string> GetMintKeys(string mintUrl, string? keysetId = null)
    {
        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(mintUrl) };
            var cashuClient = new CashuHttpClient(httpClient);
            
            GetKeysResponse keys;
            if (!string.IsNullOrEmpty(keysetId))
            {
                keys = await cashuClient.GetKeys(new DotNut.KeysetId(keysetId));
            }
            else
            {
                keys = await cashuClient.GetKeys();
            }
            
            return JsonSerializer.Serialize(new
            {
                keysets = keys.Keysets.Select(k => new
                {
                    id = k.Id.ToString(),
                    unit = k.Unit,
                    keys = k.Keys.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value.ToString())
                })
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool, Description("Check the state of Cashu proofs (spent or unspent) on a mint.")]
    public static async Task<string> CheckProofStates(string mintUrl, string proofsJson)
    {
        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(mintUrl) };
            var cashuClient = new CashuHttpClient(httpClient);
            
            var proofs = JsonSerializer.Deserialize<DotNut.Proof[]>(proofsJson);
            if (proofs == null || proofs.Length == 0)
            {
                return JsonSerializer.Serialize(new { error = "Invalid or empty proofs array" });
            }
            
            var request = new PostCheckStateRequest { Ys = proofs.Select(p => p.C.ToString()).ToArray() };
            var response = await cashuClient.CheckState(request);
            
            return JsonSerializer.Serialize(new
            {
                states = response.States.Select(s => new
                {
                    Y = s.Y.ToString(),
                    state = s.State.ToString(),
                    witness = s.Witness
                })
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}