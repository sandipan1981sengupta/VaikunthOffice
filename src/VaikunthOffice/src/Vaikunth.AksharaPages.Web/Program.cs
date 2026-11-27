using Vaikunth.DtoLibrary;
using Vaikunth.UI.Infrastructure;

// Akshara Pages (Word). Everything shared with the other two apps is set up by AddVaikunthApp / UseVaikunthApp (Vaikunth.UI).
var builder = WebApplication.CreateBuilder(args);
builder.AddVaikunthApp(SuiteApp.Pages);

var app = builder.Build();
app.UseVaikunthApp();
app.Run();
