using System.Text.Json;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Controllers;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.VishwaroopaSlides.Controllers;

/// <summary>Vishwaroopa Slides: open, create and save decks. Open/New/Save live in EditorControllerBase.</summary>
[Microsoft.AspNetCore.Mvc.Area("slides")]
public sealed class EditorController : EditorControllerBase
{
    public EditorController(IDocumentService documents, CurrentApp current) : base(documents, current) { }

    protected override string BlankContent() =>
        JsonSerializer.Serialize(new SlidesContent
        {
            Slides =
            [
                new SlideDto
                {
                    Layout = "title",
                    Boxes =
                    [
                        new SlideBoxDto { X = 8, Y = 30, W = 84, H = 20, Html = "Click to add a title", FontSize = 54, Role = "title" },
                        new SlideBoxDto { X = 8, Y = 52, W = 84, H = 12, Html = "Click to add a subtitle", FontSize = 24, Role = "subtitle" },
                    ]
                }
            ]
        }, JsonSerializerOptions.Web);
}
