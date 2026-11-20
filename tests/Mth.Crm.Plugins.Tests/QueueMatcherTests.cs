using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mth.Crm.Plugins;
using Xunit;

public class QueueMatcherTests
{
    private static readonly DateTime Today = new DateTime(2026, 11, 20);

    private static LeadFacts Lead() => new LeadFacts
    {
        Name = "Nyt plejecenter i Odense", Title = "Opførelse af plejecenter med 60 boliger", Texts = "Totalentreprise. Facaderenovering indgår ikke.",
        Region = "Syddanmark", Value = 120m, Categories = "Institution - Nybyggeri;Bolig - Nybyggeri", MainCategory = "Institution", MainDevType = "Nybyggeri",
        PlanningStage = "Projektering", ProjectStage = "Planlægning", ContractType = "Totalentreprise", StartDate = new DateTime(2027, 3, 1)
    };

    [Fact]
    public void EmptyQueue_MatchesEverything() => Assert.True(QueueMatcher.Matches(Lead(), new QueueCriteria(), Today));

    [Fact]
    public void Region_MatchesByName_CaseInsensitive()
    {
        Assert.True(QueueMatcher.Matches(Lead(), new QueueCriteria { Regions = { "syddanmark", "Fyn" } }, Today));
        Assert.False(QueueMatcher.Matches(Lead(), new QueueCriteria { Regions = { "Hovedstaden" } }, Today));
        var l = Lead(); l.Region = "Region Syddanmark";
        Assert.True(QueueMatcher.Matches(l, new QueueCriteria { Regions = { "Syddanmark" } }, Today));
    }

    [Fact]
    public void Value_MinMax_AndMissingValueDoesNotMatch()
    {
        Assert.True(QueueMatcher.Matches(Lead(), new QueueCriteria { MinValue = 100, MaxValue = 120 }, Today));
        Assert.False(QueueMatcher.Matches(Lead(), new QueueCriteria { MinValue = 121 }, Today));
        var l = Lead(); l.Value = null;
        Assert.False(QueueMatcher.Matches(l, new QueueCriteria { MinValue = 1 }, Today));
    }

    [Fact]
    public void Keywords_AnyHit_SemicolonSeparated()
    {
        Assert.True(QueueMatcher.Matches(Lead(), new QueueCriteria { CategoryKeywords = "Erhverv; institution" }, Today));
        Assert.False(QueueMatcher.Matches(Lead(), new QueueCriteria { CategoryKeywords = "Anlæg;Vej" }, Today));
        Assert.True(QueueMatcher.Matches(Lead(), new QueueCriteria { StageKeywords = "projekt" }, Today));
        Assert.True(QueueMatcher.Matches(Lead(), new QueueCriteria { ContractTypeKeywords = "total" }, Today));
        Assert.True(QueueMatcher.Matches(Lead(), new QueueCriteria { TextKeywords = "plejecenter" }, Today));
    }

    [Fact]
    public void ExcludeKeywords_RejectOnAnyHit()
    {
        Assert.False(QueueMatcher.Matches(Lead(), new QueueCriteria { ExcludeKeywords = "facaderenovering" }, Today));
        Assert.True(QueueMatcher.Matches(Lead(), new QueueCriteria { ExcludeKeywords = "nedrivning; ;" }, Today));
    }

    [Fact]
    public void DanishLetters_MatchCaseInsensitive()
    {
        var l = Lead(); l.Region = "Sjælland";
        Assert.True(QueueMatcher.Matches(l, new QueueCriteria { Regions = { "SJÆLLAND" } }, Today));
    }

    [Fact]
    public void StartWithinMonths_AndExcludeStarted()
    {
        Assert.True(QueueMatcher.Matches(Lead(), new QueueCriteria { StartWithinMonths = 6 }, Today));
        Assert.False(QueueMatcher.Matches(Lead(), new QueueCriteria { StartWithinMonths = 3 }, Today));
        var started = Lead(); started.StartDate = new DateTime(2026, 1, 1);
        Assert.False(QueueMatcher.Matches(started, new QueueCriteria { ExcludeStarted = true }, Today));
        Assert.True(QueueMatcher.Matches(started, new QueueCriteria(), Today));
        var noStart = Lead(); noStart.StartDate = null;
        Assert.True(QueueMatcher.Matches(noStart, new QueueCriteria { ExcludeStarted = true }, Today));
        Assert.False(QueueMatcher.Matches(noStart, new QueueCriteria { StartWithinMonths = 12 }, Today));
    }

    [Fact]
    public void AllCriteriaMustMatch()
    {
        var q = new QueueCriteria { Regions = { "Syddanmark" }, MinValue = 50, CategoryKeywords = "Institution", TextKeywords = "skole" };
        Assert.False(QueueMatcher.Matches(Lead(), q, Today));
        q.TextKeywords = "plejecenter";
        Assert.True(QueueMatcher.Matches(Lead(), q, Today));
    }

    /// <summary>Guards against drift: ProjectFields.Names must equal the project columns that exist on BOTH lead and opportunity in schema.json.</summary>
    [Fact]
    public void ProjectFields_MatchSchemaJson()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "scripts", "schema", "schema.json"))) dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!, "scripts", "schema", "schema.json")));
        string[] Cols(string table) => doc.RootElement.GetProperty("tables").EnumerateArray()
            .First(t => t.GetProperty("schemaName").GetString() == table).GetProperty("columns").EnumerateArray()
            .Select(c => c.GetProperty("schemaName").GetString()!.ToLowerInvariant()).ToArray();
        var shared = Cols("mth_Lead").Intersect(Cols("mth_Opportunity")).ToArray();
        Assert.Equal(shared.OrderBy(x => x), ProjectFields.Names.OrderBy(x => x));
    }
}
