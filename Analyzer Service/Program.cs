using Analyzer_Service.Models.Configuration;
using Analyzer_Service.Services.Mongo;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.Configure<MongoSettings>(
    builder.Configuration.GetSection(MongoSettings.SectionName));

builder.Services.AddOpenApi();
builder.Services.AddSingleton<FlightTelemetryMongoProxy>();

WebApplication app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
