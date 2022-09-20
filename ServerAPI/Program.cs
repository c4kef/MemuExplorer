using EmbedIO.Net;

var cts = new CancellationTokenSource();

EndPointManager.UseIpv6 = false;

using var server = CreateWebServer("http://*:228");
await server.RunAsync(cts.Token).ConfigureAwait(false);

WebServer CreateWebServer(string url)
{
    var lserver = new WebServer(o => o
            .WithUrlPrefix(url)
            .WithMode(HttpListenerMode.EmbedIO))
        /*.WithIPBanning(o => o
            .WithMaxRequestsPerSecond(100)
            .WithRegexRules("HTTP exception 404"))*/
        .WithWebApi("/Api", m => m
            .WithController<Global>())
        .WithLocalSessionManager()
        .WithCors()
        .WithStaticFolder("/", "cweb", true, m => m
            .WithContentCaching(false))
        .WithModule(new ActionModule("/", HttpVerbs.Any, ctx => ctx.SendDataAsync(new {Message = "Error"})))
        .HandleHttpException(Exception);

    return lserver;
}

Task Exception(IHttpContext context, IHttpException httpException)
{
    //context.Redirect("/error");
    return Task.CompletedTask;
}