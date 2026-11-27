using Vaikunth.DtoLibrary;
using Vaikunth.UI.Infrastructure;

// Hrishikesha Workbook (Excel). Everything shared with the other two apps is set up by AddVaikunthApp / UseVaikunthApp (Vaikunth.UI).
var builder = WebApplication.CreateBuilder(args);
builder.AddVaikunthApp(SuiteApp.Workbook);

var app = builder.Build();
app.UseVaikunthApp();
app.Run();
