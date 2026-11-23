using System;
using System.Linq;
using Mth.Crm.Plugins;
using Xunit;

// Fictitious companies only, in the same multi-ref SOAP format as NNE 3.1 returns.
public class NneSoapTests
{
    private const string SearchResponse = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:enc=""http://schemas.xmlsoap.org/soap/encoding/"" xmlns:ns0=""http://com.stibo.net/nne/3.1/Types/NNE""><env:Body>
<ans1:searchTargetGroupResponse xmlns:ans1=""http://com.stibo.net/nne/3.1/NNE""><result href=""#ID1""/></ans1:searchTargetGroupResponse>
<ns0:Answer id=""ID1"" xsi:type=""ns0:Answer""><companyBasic href=""#ID2""/><errorMessage xsi:type=""xsd:string"" xsi:nil=""1""/><window href=""#ID3""/></ns0:Answer>
<ns0:ArrayOfCompanyBasic id=""ID2"" xsi:type=""enc:Array""><item href=""#ID4""/><item href=""#ID5""/></ns0:ArrayOfCompanyBasic>
<ns0:Window id=""ID3""><currentPage xsi:type=""xsd:int"">1</currentPage><total xsi:type=""xsd:int"">137</total></ns0:Window>
<ns0:CompanyBasic id=""ID4""><PNo xsi:type=""xsd:int"">1000000001</PNo><adProtection xsi:type=""xsd:int"">1</adProtection><companyType xsi:type=""xsd:string"">VI</companyType><cvrNo xsi:type=""xsd:int"">12345678</cvrNo><district xsi:type=""xsd:string"">Kystby</district><officialName xsi:type=""xsd:string"">Testbyg &amp; Søn ApS</officialName><phone xsi:type=""xsd:string"" xsi:nil=""1""/><street xsi:type=""xsd:string"">Fiktivvej 1</street><tdcId xsi:type=""xsd:int"">900000001</tdcId><zipCode xsi:type=""xsd:int"">9999</zipCode></ns0:CompanyBasic>
<ns0:CompanyBasic id=""ID5""><PNo xsi:type=""xsd:int"">0</PNo><adProtection xsi:type=""xsd:int"">0</adProtection><cvrNo xsi:type=""xsd:int"">0</cvrNo><district xsi:type=""xsd:string"">Skovby</district><officialName xsi:type=""xsd:string"">Eksempel Forening</officialName><phone xsi:type=""xsd:string"">70000000</phone><street xsi:type=""xsd:string"">Opdigtet Allé 22</street><tdcId xsi:type=""xsd:int"">900000002</tdcId><zipCode xsi:type=""xsd:int"">9998</zipCode></ns0:CompanyBasic>
</env:Body></env:Envelope>";

    private const string CompanyResponse = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:ns0=""http://com.stibo.net/nne/3.1/Types/NNE""><env:Body>
<ans1:fetchCompanyResponse xmlns:ans1=""http://com.stibo.net/nne/3.1/NNE""><result href=""#ID1""/></ans1:fetchCompanyResponse>
<ns0:Company id=""ID1""><PNo xsi:type=""xsd:int"">1000000001</PNo><adProtection xsi:type=""xsd:int"">1</adProtection><companyForm xsi:type=""xsd:string"">Anpartsselskab</companyForm><cvrNo xsi:type=""xsd:int"">12345678</cvrNo><district xsi:type=""xsd:string"">Kystby</district><email xsi:type=""xsd:string"">info@example.com</email><foundedYear xsi:type=""xsd:int"">1999</foundedYear><geography href=""#ID2""/><homePage xsi:type=""xsd:string"">www.example.com</homePage><legalEntity xsi:type=""xsd:int"">1</legalEntity><numberOfEmployees xsi:type=""xsd:int"">42</numberOfEmployees><officialName xsi:type=""xsd:string"">Testbyg &amp; Søn ApS</officialName><phone xsi:type=""xsd:string"">70000001</phone><statusText xsi:type=""xsd:string"">Selskabet er i normal drift.</statusText><statusValue xsi:type=""xsd:int"">0</statusValue><street xsi:type=""xsd:string"">Fiktivvej 1</street><tdcId xsi:type=""xsd:int"">900000001</tdcId><tdfName xsi:type=""xsd:string"" xsi:nil=""1""/><zipCode xsi:type=""xsd:int"">9999</zipCode></ns0:Company>
<ns0:KVH id=""ID2""><houseNo xsi:type=""xsd:string"">1</houseNo></ns0:KVH>
</env:Body></env:Envelope>";

    [Fact]
    public void ParsesSearchHitsAndTotal()
    {
        var rows = NneSoap.ParseSearch(SearchResponse, out var total);
        Assert.Equal(137, total);
        Assert.Equal(2, rows.Count);
        var a = rows[0];
        Assert.Equal("900000001", a.TdcId);
        Assert.Equal("Testbyg & Søn ApS", a.OfficialName);
        Assert.Equal("12345678", a.CvrNo);
        Assert.Equal("1000000001", a.PNo);
        Assert.True(a.AdProtection);
        Assert.Equal("", a.Phone);                   // xsi:nil
        Assert.Equal("", rows[1].CvrNo);             // 0 = no CVR
        Assert.Equal("", rows[1].PNo);
    }

    [Fact]
    public void ParsesCompanyDetails()
    {
        var c = NneSoap.ParseCompany(CompanyResponse);
        Assert.Equal("Selskabet er i normal drift.", c.StatusText);
        Assert.Equal(0, c.StatusValue);
        Assert.Equal("Anpartsselskab", c.CompanyForm);
        Assert.True(c.LegalEntity);
        Assert.Equal(42, c.NumberOfEmployees);
        Assert.Equal(1999, c.FoundedYear);
        Assert.Equal("info@example.com", c.Email);
        Assert.Equal("", c.TdfName);
    }

    [Fact]
    public void MergeCopiesDetailsOntoSearchRow()
    {
        var row = NneSoap.ParseSearch(SearchResponse, out _)[0];
        NneSoap.Merge(row, NneSoap.ParseCompany(CompanyResponse));
        Assert.Equal("www.example.com", row.HomePage);
        Assert.Equal(42, row.NumberOfEmployees);
    }

    [Fact]
    public void ErrorMessageAndFaultThrow()
    {
        var withError = SearchResponse.Replace(@"<errorMessage xsi:type=""xsd:string"" xsi:nil=""1""/>", @"<errorMessage xsi:type=""xsd:string"">Ugyldig adgangsnøgle</errorMessage>");
        var ex = Assert.Throws<InvalidOperationException>(() => NneSoap.ParseSearch(withError, out _));
        Assert.Contains("Ugyldig adgangsnøgle", ex.Message);
        var fault = @"<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Body><env:Fault><faultcode>env:Server</faultcode><faultstring>Access denied</faultstring></env:Fault></env:Body></env:Envelope>";
        Assert.Contains("Access denied", Assert.Throws<InvalidOperationException>(() => NneSoap.ParseCompany(fault)).Message);
    }

    [Fact]
    public void SearchEnvelopeEscapesAndNormalises()
    {
        var xml = NneSoap.SearchEnvelope(new NneQuery { Name = "A & B <test>", Zip = " 9999 ", Cvr = "abc", ActiveOnly = true, MaxHits = 99999 }, "key<1>");
        Assert.Contains("<name xsi:type=\"xsd:string\">A &amp; B &lt;test&gt;</name>", xml);
        Assert.Contains("<zipCode xsi:type=\"xsd:int\">9999</zipCode>", xml);
        Assert.Contains("<cvrNo xsi:type=\"xsd:int\">0</cvrNo>", xml);          // not a number -> 0 (= not used)
        Assert.Contains("<companyStatus xsi:type=\"xsd:string\">0</companyStatus>", xml);
        Assert.Contains("<int_2 xsi:type=\"xsd:int\">5000</int_2>", xml);        // capped
        Assert.Contains("key&lt;1&gt;", xml);
        System.Xml.Linq.XDocument.Parse(xml);                                     // well-formed
    }
}
