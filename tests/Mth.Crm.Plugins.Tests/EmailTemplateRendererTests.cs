using System;
using System.Collections.Generic;
using Mth.Crm.Plugins;
using Xunit;

public class EmailTemplateRendererTests
{
    private static TemplateValues Values() => new TemplateValues
    {
        Opportunity = "Havneudvidelse, Odense C",
        CaseNumber = "P123456",
        BuildingContract = "El-arbejde",
        TenderDeadline = new DateTime(2026, 12, 4),
        Partner = "Fyns El & <Co> ApS",
        ContactFirstNames = new List<string> { "Anne", "Bo" },
        Sender = "Søren Klint"
    };

    [Fact]
    public void Subject_IsRenderedPlain()
    {
        var s = EmailTemplateRenderer.RenderSubject("Tilbud: {{BuildingContract}} – {{Opportunity}} ({{CaseNumber}})", Values());
        Assert.Equal("Tilbud: El-arbejde – Havneudvidelse, Odense C (P123456)", s);
    }

    [Fact]
    public void Body_IsHtmlEncoded()
    {
        var b = EmailTemplateRenderer.RenderBody("<p>Kære {{ContactFirstName}} hos {{Partner}}</p>", Values());
        Assert.Equal("<p>Kære Anne og Bo hos Fyns El &amp; &lt;Co&gt; ApS</p>", b);
    }

    [Fact]
    public void Body_KeepsDanishLetters()
    {
        var v = Values(); v.Partner = "Øst Gulvservice I/S – Æbleø";
        Assert.Equal("Øst Gulvservice I/S – Æbleø", EmailTemplateRenderer.RenderBody("{{Partner}}", v));
    }

    [Fact]
    public void EmptyValue_IsShownAsDash()
    {
        var v = Values(); v.CaseNumber = null;
        Assert.Equal("(sagsnr. –)", EmailTemplateRenderer.RenderBody("(sagsnr. {{CaseNumber}})", v));
    }

    [Fact]
    public void Deadline_IsDanishLongDate()
    {
        Assert.Equal("4. december 2026", EmailTemplateRenderer.RenderSubject("{{TenderDeadline}}", Values()));
    }

    [Fact]
    public void MissingDeadline_IsExplained()
    {
        var v = Values(); v.TenderDeadline = null;
        Assert.Equal("(ingen frist)", EmailTemplateRenderer.RenderSubject("{{TenderDeadline}}", v));
    }

    [Fact]
    public void UnknownPlaceholder_IsLeftVisible()
    {
        Assert.Equal("Hej {{Ukendt}}", EmailTemplateRenderer.RenderSubject("Hej {{Ukendt}}", Values()));
    }

    [Fact]
    public void Placeholders_AreCaseInsensitive_AndAllowSpaces()
    {
        Assert.Equal("Søren Klint", EmailTemplateRenderer.RenderSubject("{{ sender }}", Values()));
    }

    [Theory]
    [InlineData(new string[0], "alle")]
    [InlineData(new[] { "Anne" }, "Anne")]
    [InlineData(new[] { "Anne", "Bo" }, "Anne og Bo")]
    [InlineData(new[] { "Anne", "Bo", "Carl" }, "Anne, Bo og Carl")]
    [InlineData(new[] { "Anne", " ", "Anne" }, "Anne")]
    public void JoinNames(string[] names, string expected)
    {
        Assert.Equal(expected, EmailTemplateRenderer.JoinNames(names));
    }
}
