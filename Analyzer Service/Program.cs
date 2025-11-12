using Analyzer_Service.Models.Algorithms;
using Analyzer_Service.Models.Configuration;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Ccm;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms.Pelt.Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Services.Algorithms;
using Analyzer_Service.Services.Algorithms.Ccm;
using Analyzer_Service.Services.Algorithms.Pelt;
using Analyzer_Service.Services.Mongo;
using MathNet.Numerics;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.Configure<MongoSettings>(
    builder.Configuration.GetSection(MongoSettings.SectionName));


builder.Services.AddSingleton<IGrangerCausalityAnalyzer, GrangerCausalityAnalyzer>();
builder.Services.AddSingleton<ICcmCausalityAnalyzer, CcmCausalityAnalyzer>();

builder.Services.AddSingleton<IPrepareFlightData, PrepareFlightData>();
builder.Services.AddSingleton<IFlightTelemetryMongoProxy, FlightTelemetryMongoProxy>();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IFlightCausality, FlightCausality>();
builder.Services.AddSingleton<IAutoCausalitySelector, AutoCausalitySelector>();


builder.Services.AddSingleton<IChangePointDetectionService, ChangePointDetectionService>();
builder.Services.AddSingleton<ISignalPreprocessor, SignalPreprocessor>();
builder.Services.AddSingleton<IPeltAlgorithm, PeltAlgorithm>();
builder.Services.AddSingleton<IRbfKernelCost, RbfKernelCost>();



WebApplication app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
