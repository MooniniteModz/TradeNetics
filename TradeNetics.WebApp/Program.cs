using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradeNetics.Shared.Interfaces;
using TradeNetics.Shared.Services;
using TradeNetics.WebApp.Data;
using TradeNetics.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<TradingBotStatusService>();
builder.Services.AddSingleton<TradeHistoryService>();
builder.Services.AddHttpClient<RealCryptoDataService>();

// Register ICryptoDataService with real data service
builder.Services.AddScoped<ICryptoDataService, RealCryptoDataService>();

// Shared Services
builder.Services.AddTradeNeticsSharedServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();