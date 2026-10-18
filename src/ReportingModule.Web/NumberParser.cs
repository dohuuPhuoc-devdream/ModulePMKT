using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ReportingModule.Web
{
 // User-entered numbers follow vi-VN notation: dot groups thousands, comma marks decimals.
 public static class NumberParser
 {
  private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
  // A four-digit fractional suffix after one dot is unambiguous for the
  // application's 4-decimal price precision and preserves existing inputs
  // such as 123.4567. Three digits remain a thousands group (15.000).
  private static readonly Regex Valid = new Regex(@"\A(?:[+-]?(?:\d+|[1-9]\d{0,2}(?:\.\d{3})+)(?:,\d+)?|[+-]?\d{1,3}\.\d{4})\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
  public static bool TryParse(string text, out decimal value)
  {
   value=0m;
   string input=(text??"").Trim();
   if(input.Length==0 || !Valid.IsMatch(input)) return false;
   if(Regex.IsMatch(input,@"\A[+-]?\d{1,3}\.\d{4}\z",RegexOptions.CultureInvariant)) input=input.Replace('.', ',');
   return decimal.TryParse(input,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint|NumberStyles.AllowThousands,Vi,out value);
  }
  public static string FormatInput(object value)
  { return Format(value); }
  public static string Format(object value)
  { return Convert.ToDecimal(value,CultureInfo.InvariantCulture).ToString("#,##0.####",Vi); }
  public const string Hint="Dùng dấu chấm phân tách hàng nghìn và dấu phẩy thập phân (ví dụ 15.000 hoặc 1,5).";
 }
}
