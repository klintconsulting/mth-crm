using System;
using System.Linq;
using Mth.Crm.Plugins;
using Xunit;

public class MentionParserTests
{
    private static readonly MentionCandidate Soeren = new MentionCandidate(Guid.NewGuid(), "Søren Klint", "sk@mth.dk");
    private static readonly MentionCandidate Anne = new MentionCandidate(Guid.NewGuid(), "Anne Hansen", "ah@mth.dk");
    private static readonly MentionCandidate Annette = new MentionCandidate(Guid.NewGuid(), "Annette Holm", "anho@mth.dk");
    private static readonly MentionCandidate[] All = { Soeren, Anne, Annette };

    [Fact]
    public void FullName_IsFound()
    {
        var r = MentionParser.FindMentions("Kan @Søren Klint kigge på tilbuddet?", All);
        Assert.Equal(new[] { Soeren.Id }, r.Select(x => x.Id));
    }

    [Fact]
    public void Alias_FromEmail_IsFound()
    {
        var r = MentionParser.FindMentions("@sk og @ah: deadline fredag", All);
        Assert.Equal(2, r.Count);
        Assert.Contains(r, x => x.Id == Soeren.Id);
        Assert.Contains(r, x => x.Id == Anne.Id);
    }

    [Fact]
    public void IsCaseInsensitive()
    {
        Assert.Single(MentionParser.FindMentions("hej @SØREN KLINT", All));
    }

    [Fact]
    public void PartialWord_DoesNotMatch()
    {
        // "@Anne Hansenx" must not match Anne; "@ahx" must not match alias "ah"
        Assert.Empty(MentionParser.FindMentions("@Anne Hansenx og @ahx", All));
    }

    [Fact]
    public void EmailAddressInText_IsNotAMention()
    {
        // "kontakt@sk.dk" contains "@sk" but is an e-mail address, not a mention
        Assert.Empty(MentionParser.FindMentions("skriv til kontakt@sk.dk", All));
    }

    [Fact]
    public void SameUserMentionedTwice_IsReturnedOnce()
    {
        Assert.Single(MentionParser.FindMentions("@sk ... @Søren Klint", All));
    }

    [Fact]
    public void NoAt_ReturnsEmpty()
    {
        Assert.Empty(MentionParser.FindMentions("Søren Klint skal kigge på det", All));
    }

    [Fact]
    public void PunctuationAfterMention_StillMatches()
    {
        Assert.Single(MentionParser.FindMentions("Tak @Anne Hansen!", All));
    }

    [Fact]
    public void Html_IsConvertedToPlainText()
    {
        var text = MentionParser.ToPlainText("<div><p>Hej&nbsp;<b>@Søren&nbsp;Klint</b></p><p>Linje&nbsp;2 &amp; mere</p></div>");
        Assert.Equal("Hej @Søren Klint\nLinje 2 & mere", text);
        Assert.Single(MentionParser.FindMentions(text, All));
    }

    [Fact]
    public void Excerpt_ShortensLongText()
    {
        Assert.Equal("abcd…", MentionParser.Excerpt("abcdefghij", 5));
        Assert.Equal("abc", MentionParser.Excerpt("abc", 5));
    }
}
