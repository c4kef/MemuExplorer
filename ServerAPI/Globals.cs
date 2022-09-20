global using EmbedIO;
global using EmbedIO.Actions;
global using EmbedIO.Files;
global using EmbedIO.Security;
global using Swan.Logging;
global using Newtonsoft.Json;
global using Server;
global using Server.Api;
global using EmbedIO.Routing;
global using EmbedIO.WebApi;
global using System.Net;
global using System.Globalization;
global using System.Text;
global using System.Text.RegularExpressions;

public struct ServerData
{
    public ServerData()
    {
        Values = new List<object>();
        Type = string.Empty;
    }

    public string Type;
    public List<object> Values;
}