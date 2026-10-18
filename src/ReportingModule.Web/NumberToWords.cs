using System;
using System.Globalization;
using System.Text;

namespace ReportingModule.Web
{
 public static class NumberToWords
 {
  private static readonly string[] Ones={"không","một","hai","ba","bốn","năm","sáu","bảy","tám","chín"};
  private static readonly string[] Scales={"","nghìn","triệu","tỷ","nghìn tỷ","triệu tỷ","tỷ tỷ","nghìn tỷ tỷ","triệu tỷ tỷ","tỷ tỷ tỷ"};
  public static string Vietnamese(decimal amount)
  {
   decimal rounded=decimal.Round(amount,0,MidpointRounding.AwayFromZero);
   bool negative=rounded<0; string digits=Math.Abs(rounded).ToString("0",CultureInfo.InvariantCulture);
   int padding=(3-digits.Length%3)%3; digits=new string('0',padding)+digits;
   int groupCount=digits.Length/3;
   if(groupCount>Scales.Length) throw new ArgumentOutOfRangeException("amount","Số tiền vượt giới hạn đọc bằng chữ.");
   var result=new StringBuilder(); bool hasWords=false;
   for(int group=0;group<groupCount;group++)
   {
    string chunk=digits.Substring(group*3,3); int value=int.Parse(chunk,CultureInfo.InvariantCulture);
    if(value==0)continue;
    int scaleIndex=groupCount-group-1;
    bool full=group>0;
    string spoken=ThreeDigits(value,full);
    if(hasWords)result.Append(' ');
    result.Append(spoken);
    if(Scales[scaleIndex].Length>0)result.Append(' ').Append(Scales[scaleIndex]);
    hasWords=true;
   }
   if(!hasWords)result.Append("không");
   if(negative)result.Insert(0,"âm ");
   result.Append(" đồng chẵn");
   result[0]=char.ToUpperInvariant(result[0]);
   return result.ToString();
  }
  private static string ThreeDigits(int value,bool full)
  {
   int hundreds=value/100,tens=(value/10)%10,ones=value%10;
   var words=new StringBuilder();
   if(hundreds>0){words.Append(Ones[hundreds]).Append(" trăm");}
   else if(full) words.Append("không trăm");
   if(tens>1){if(words.Length>0)words.Append(' ');words.Append(Ones[tens]).Append(" mươi");}
   else if(tens==1){if(words.Length>0)words.Append(' ');words.Append("mười");}
   else if(ones>0&&words.Length>0){words.Append(" linh");}
   if(ones>0)
   {
    if(words.Length>0)words.Append(' ');
    string word=Ones[ones];
    if(tens>1&&ones==1)word="mốt";
    else if(tens>1&&ones==4)word="tư";
    else if(tens>0&&ones==5)word="lăm";
    words.Append(word);
   }
   return words.ToString();
  }
 }
}
