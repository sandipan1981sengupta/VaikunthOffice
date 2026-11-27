using System.Text.Json;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Controllers;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.DhruvaPlanner.Controllers;

/// <summary>Dhruva Planner: open, create and save projects. Open/New/Save live in EditorControllerBase; Download makes an Excel file.</summary>
[Microsoft.AspNetCore.Mvc.Area("planner")]
public sealed class EditorController : EditorControllerBase
{
    public EditorController(IDocumentService documents, CurrentApp current) : base(documents, current) { }

    /// <summary>A new project starts today, runs four weeks and has three tasks to show how the board works.</summary>
    protected override string BlankContent()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var me = User.DisplayName();
        return JsonSerializer.Serialize(new PlannerContent
        {
            Start = today,
            End = today.AddDays(27),
            Tasks =
            [
                new PlannerTaskDto { Id = "t1", Title = "Agree the goal and the team", Status = "doing", Priority = "high", Owner = me, Start = today, Due = today.AddDays(3), Progress = 40 },
                new PlannerTaskDto { Id = "t2", Title = "Break the work into tasks", Status = "todo", Owner = me, Start = today.AddDays(3), Due = today.AddDays(7) },
                new PlannerTaskDto { Id = "t3", Title = "Kick-off", Status = "todo", Owner = me, Start = today.AddDays(7), Due = today.AddDays(7), Milestone = true }
            ]
        }, JsonSerializerOptions.Web);
    }

    protected override Task<ExportedFile?> ExportAsync(string content, string title, CancellationToken ct)
    {
        var plan = JsonSerializer.Deserialize<PlannerContent>(content, JsonSerializerOptions.Web) ?? new PlannerContent();
        return Task.FromResult<ExportedFile?>(new ExportedFile(Vaikunth.DhruvaPlanner.Export.PlannerXlsx.Build(plan, title),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx"));
    }
}
