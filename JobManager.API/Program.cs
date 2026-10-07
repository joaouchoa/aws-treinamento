using JobManager.API.Entities;
using JobManager.API.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Amazon.Extensions.NETCore.Setup;
using Amazon;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Configuration.AddSystemsManager(source =>
{
    source.AwsOptions = new AWSOptions
    {
        Region = RegionEndpoint.SAEast1//sa-east-1
    };

    source.Path = "/";
    source.ReloadAfter = TimeSpan.FromSeconds(30);
});

builder.Configuration.AddSecretsManager(null, RegionEndpoint.SAEast1, config =>
{
    config.KeyGenerator = (secret, name) => name.Replace("/", ":");
    config.PollingInterval = TimeSpan.FromMinutes(30);
});

var connectionString = builder.Configuration.GetConnectionString("AppDb");
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/api/jobs", async (Job job, AppDbContext db) =>
{
    await db.Jobs.AddAsync(job);
    await db.SaveChangesAsync();
    return Results.Created($"/api/jobs/{job.Id}", job);
});

app.MapGet("/api/jobs/{id}", async (int id, AppDbContext context) =>
{
    var job = await context.Jobs.SingleOrDefaultAsync(j => j.Id == id);
    if (job == null)
    {
        return Results.NotFound();
    }
    return Results.Ok(job);
});

app.MapGet("/api/jobs", async (AppDbContext context) =>
{
    var jobs = await context.Jobs.ToListAsync();
    return Results.Ok(jobs);
});

app.MapPost("/api/jobs/{id}/applications", async (int id, JobAplication application,[FromServices] AppDbContext context) =>
{
    var job = await context.Jobs.SingleOrDefaultAsync(j => j.Id == id);
    if (job == null)
    {
        return Results.NotFound();
    }
    application.JobId = id;
    await context.JobApplications.AddAsync(application);
    await context.SaveChangesAsync();
    return Results.Created($"/api/jobs/{id}/applications/{application.Id}", application);
});

app.MapPost("/api/jobs/{id}/applications/upload", async (int id, IFormFile file, [FromServices] AppDbContext context) =>
{
    if(file == null || file.Length == 0)
    {
        return Results.BadRequest();
    }

    var extension = Path.GetExtension(file.FileName);
    
    var validExtensions = new List<string> { ".pdf", ".doc", ".docx" };
    if (!validExtensions.Contains(extension.ToLower()))
    {
        return Results.BadRequest();
    }

    var key = $"job-application/{id}-{file.FileName}";

    var application = await context.JobApplications.SingleOrDefaultAsync(ja => ja.Id == id);

    if(application is null)
    {
        return Results.NotFound();
    }

    application.CVUrl = key;

    await context.SaveChangesAsync();

    return Results.NoContent();
});

app.UseAuthorization();

app.MapControllers();

app.Run();
