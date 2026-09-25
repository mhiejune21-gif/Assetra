using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Assetra.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IFirestoreService, FirestoreService>();
builder.Services.AddSession();

var app = builder.Build();
app.UseSession();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Initialize Firestore database & seeding
using (var scope = app.Services.CreateScope())
{
    var firestoreService = scope.ServiceProvider.GetRequiredService<IFirestoreService>();
    await firestoreService.InitializeAsync();

    // Ensure QR code images exist on disk for demo properties
    var properties = await firestoreService.GetPropertiesAsync();
    string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "qrcodes");
    if (!Directory.Exists(folder))
        Directory.CreateDirectory(folder);

    foreach (var property in properties)
    {
        string fileName = $"{property.PropertyId}_qr.png";
        string path = Path.Combine(folder, fileName);

        if (!System.IO.File.Exists(path))
        {
            using var qrGenerator = new QRCoder.QRCodeGenerator();
            var data = qrGenerator.CreateQrCode(property.PropertyId, QRCoder.QRCodeGenerator.ECCLevel.Q);
            var pngQr = new QRCoder.PngByteQRCode(data);
            byte[] bytes = pngQr.GetGraphic(20);
            System.IO.File.WriteAllBytes(path, bytes);
        }
    }
}

app.Run();
