using JobManager.API.Entities;
using JobManager.API.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Amazon.Extensions.NETCore.Setup;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

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
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest();
    }

    var extension = Path.GetExtension(file.FileName);

    var validExtensions = new List<string> { ".pdf", ".doc", ".docx" };
    if (!validExtensions.Contains(extension.ToLower()))
    {
        return Results.BadRequest();
    }

    var client = new AmazonS3Client(RegionEndpoint.SAEast1);

    var bucketName = "formacao-aws-cv-jdu";
    var key = $"job-application/{id}-{file.FileName}";

    using (var stream = file.OpenReadStream())
    {
        var request = new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = stream,
            ContentType = file.ContentType
        };
        await client.PutObjectAsync(request);
    }

    var application = await context.JobApplications.SingleOrDefaultAsync(ja => ja.Id == id);

    if (application is null)
    {
        return Results.NotFound();
    }

    application.CVUrl = key;

    await context.SaveChangesAsync();

    return Results.NoContent();
}).DisableAntiforgery();

app.MapGet("/api/job-aplications/cvs/{id}", async (int id, string email, [FromServices] AppDbContext context) => 
{
    var aplication = await context.JobApplications.FirstOrDefaultAsync(ja => ja.CandidateEmail == email);

    if(aplication == null)
    {
        return Results.NotFound();
    }   

    var bucketName = "formacao-aws-cv-jdu";

    var getRequest = new GetObjectRequest
    {
        BucketName = bucketName,
        Key = aplication.CVUrl
    };

    var response = await new AmazonS3Client(RegionEndpoint.SAEast1).GetObjectAsync(getRequest);

    return Results.File(response.ResponseStream, response.Headers.ContentType);
});

app.UseAuthorization();

app.MapControllers();

app.Run();
