using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates.Finance;

/// <summary>
/// Excel loan amortization schedule. Computes the standard fixed-payment
/// amortization given principal, APR, and term in months. Emits an
/// <c>{preamble, headers, rows}</c> body that the Excel generator turns
/// into a formatted worksheet.
/// </summary>
public sealed class LoanAmortizationTemplate : IDocumentTemplate
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string Id => "finance/loan-amortization";
    public string Name => "Loan Amortization Schedule";
    public string Industry => "finance";
    public string Description => "Fixed-payment amortization schedule with monthly principal, interest, and balance.";
    public DocumentFormat DefaultFormat => DocumentFormat.Excel;

    public IReadOnlyList<TemplateField> Fields { get; } = new List<TemplateField>
    {
        new() { Name = "borrower", Type = TemplateFieldType.String, Required = true },
        new() { Name = "loan_name", Type = TemplateFieldType.String, Required = false, Description = "e.g. \"30-year fixed mortgage\"." },
        new() { Name = "principal", Type = TemplateFieldType.Currency, Required = true },
        new() { Name = "annual_rate_percent", Type = TemplateFieldType.Decimal, Required = true, Description = "Annual percentage rate, e.g. 6.5 for 6.5% APR." },
        new() { Name = "term_months", Type = TemplateFieldType.Integer, Required = true },
        new() { Name = "first_payment_date", Type = TemplateFieldType.Date, Required = false, Description = "First payment ISO date (defaults to no dates)." },
        new() { Name = "currency", Type = TemplateFieldType.String, Required = false },
    };

    public TemplateRenderResult Render(JsonElement data)
    {
        var d = TemplateData.Validate(data, Fields);
        var borrower = d.String("borrower");
        var loanName = d.String("loan_name");
        var principal = d.Decimal("principal");
        var apr = d.Decimal("annual_rate_percent");
        var termMonths = d.Integer("term_months");
        var firstPaymentDate = d.Date("first_payment_date");
        var currency = TemplateFormat.NormalizeCurrency(d.String("currency", "USD"));

        if (termMonths <= 0)
            throw new TemplateValidationException("'term_months' must be a positive integer.");

        var monthlyRate = (double)(apr / 100m / 12m);
        var n = termMonths;
        double payment;
        if (monthlyRate == 0d)
        {
            payment = (double)principal / n;
        }
        else
        {
            var factor = Math.Pow(1 + monthlyRate, n);
            payment = (double)principal * (monthlyRate * factor) / (factor - 1);
        }

        var preamble = new List<object[]>
        {
            new object[] { $"Loan Amortization — {borrower}", string.Empty, string.Empty, string.Empty, string.Empty },
            new object[] { "Borrower", borrower, string.Empty, string.Empty, string.Empty },
        };
        if (!string.IsNullOrWhiteSpace(loanName))
            preamble.Add(new object[] { "Loan", loanName, string.Empty, string.Empty, string.Empty });
        preamble.Add(new object[] { "Principal", TemplateFormat.FormatMoney(principal, currency), string.Empty, string.Empty, string.Empty });
        preamble.Add(new object[] { "APR", TemplateFormat.FormatPercent(apr), string.Empty, string.Empty, string.Empty });
        preamble.Add(new object[] { "Term (months)", termMonths.ToString(CultureInfo.InvariantCulture), string.Empty, string.Empty, string.Empty });
        preamble.Add(new object[] { "Monthly payment", string.Create(CultureInfo.InvariantCulture, $"{currency} {payment:N2}"), string.Empty, string.Empty, string.Empty });
        preamble.Add(new object[] { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty });

        var headers = new[] { "Payment #", "Date", "Payment", "Interest", "Principal", "Balance" };

        var rows = new List<object[]>();
        var balance = (double)principal;
        var date = firstPaymentDate;
        double totalInterest = 0d;
        double totalPaid = 0d;

        for (var i = 1; i <= termMonths; i++)
        {
            var interestPortion = balance * monthlyRate;
            var principalPortion = payment - interestPortion;
            if (i == termMonths)
            {
                principalPortion = balance;
                payment = principalPortion + interestPortion;
            }
            balance -= principalPortion;
            if (balance < 0 && balance > -0.005) balance = 0;
            totalInterest += interestPortion;
            totalPaid += payment;

            rows.Add(new object[]
            {
                i.ToString(CultureInfo.InvariantCulture),
                date is not null ? TemplateFormat.FormatDate(date) : string.Empty,
                payment.ToString("0.00", CultureInfo.InvariantCulture),
                interestPortion.ToString("0.00", CultureInfo.InvariantCulture),
                principalPortion.ToString("0.00", CultureInfo.InvariantCulture),
                balance.ToString("0.00", CultureInfo.InvariantCulture),
            });

            if (date is not null)
                date = date.Value.AddMonths(1);
        }

        rows.Add(new object[] { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty });
        rows.Add(new object[] { "Totals", string.Empty,
            totalPaid.ToString("0.00", CultureInfo.InvariantCulture),
            totalInterest.ToString("0.00", CultureInfo.InvariantCulture),
            ((double)principal).ToString("0.00", CultureInfo.InvariantCulture),
            string.Empty });

        var body = JsonSerializer.Serialize(new { preamble, headers, rows }, s_jsonOptions);
        var fileName = $"amortization-{TemplateFormat.Slug(borrower)}.xlsx";
        return new TemplateRenderResult(DocumentFormat.Excel, body, fileName, $"Loan Amortization — {borrower}");
    }
}
