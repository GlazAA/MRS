using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MRS.Application.Security;
using MRS.Application.Sync;
using MRS.Infrastructure.Postgres;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

var connectionString = builder.Configuration.GetConnectionString("Mrs")
	?? throw new InvalidOperationException("ConnectionStrings:Mrs не задана.");
var jwtKey = builder.Configuration["Jwt:Key"]
	?? throw new InvalidOperationException("Jwt:Key не задан.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "MRS";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "MRS.Maui";

builder.Services.AddSingleton(new PostgresConnectionFactory(connectionString));
builder.Services.AddSingleton<PostgresDatabaseBootstrapper>();
builder.Services.AddSingleton(sp => new PostgresAuthService(
	sp.GetRequiredService<PostgresConnectionFactory>(),
	jwtKey,
	jwtIssuer,
	jwtAudience));
builder.Services.AddSingleton<PostgresSyncService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			ValidIssuer = jwtIssuer,
			ValidAudience = jwtAudience,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
		};
	});
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var bootstrapper = scope.ServiceProvider.GetRequiredService<PostgresDatabaseBootstrapper>();
	await bootstrapper.EnsureReadyAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

app.MapPost("/api/auth/login", async (LoginRequest request, PostgresAuthService auth, CancellationToken ct) =>
{
	var result = await auth.LoginAsync(request.Login, request.Password, ct).ConfigureAwait(false);
	if (!result.Ok)
		return Results.Json(result, statusCode: StatusCodes.Status401Unauthorized);
	return Results.Ok(result);
});

app.MapPost("/api/sync/push", async (
	SyncPushRequest request,
	ClaimsPrincipal user,
	PostgresSyncService sync,
	CancellationToken ct) =>
{
	var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
	if (!int.TryParse(userIdClaim, out var userId))
		return Results.Unauthorized();

	var response = await sync.PushAsync(request, userId, ct).ConfigureAwait(false);
	return Results.Ok(response);
}).RequireAuthorization();

app.MapPost("/api/sync/pull", async (
	SyncPullRequest request,
	PostgresSyncService sync,
	CancellationToken ct) =>
{
	var response = await sync.PullAsync(request.Since, ct).ConfigureAwait(false);
	return Results.Ok(response);
}).RequireAuthorization();

app.Run();

internal sealed record LoginRequest(string Login, string Password);
