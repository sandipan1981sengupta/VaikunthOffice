using Vaikunth.DtoLibrary;
using Vaikunth.UI.Infrastructure;

// Vishwaroopa Slides (PowerPoint). Everything shared with the other two apps is set up by AddVaikunthApp / UseVaikunthApp (Vaikunth.UI).
var builder = WebApplication.CreateBuilder(args);
builder.AddVaikunthApp(SuiteApp.Slides);

var app = builder.Build();
app.UseVaikunthApp();
app.Run();
