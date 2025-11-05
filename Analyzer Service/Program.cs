using Analyzer_Service.Models.Configuration;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Services.Algorithms;
using Analyzer_Service.Services.Mongo;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.Configure<MongoSettings>(
    builder.Configuration.GetSection(MongoSettings.SectionName));
builder.Services.AddSingleton<IGrangerCausalityAnalyzer, GrangerCausalityAnalyzer>();
builder.Services.AddSingleton<IPrepareFlightData, PrepareFlightData>();
builder.Services.AddSingleton<IFlightTelemetryMongoProxy, FlightTelemetryMongoProxy>();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IFlightCausality, FlightCausality>();

WebApplication app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
