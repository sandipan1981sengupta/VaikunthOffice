using Vaikunth.UI.Infrastructure;

// Vaikunth Desk: one website. After sign-in the landing page shows Word, Excel and PowerPoint;
// each opens in its own tab at /pages, /workbook or /slides. Everything is set up in Vaikunth.UI (WebRegistration).
var builder = WebApplication.CreateBuilder(args);
builder.AddVaikunthOffice();

var app = builder.Build();
app.UseVaikunthOffice();
app.Run();
