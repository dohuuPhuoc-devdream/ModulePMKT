using System;
using System.IO;
using System.Text;
using System.Web;

namespace ReportingModule.Web
{
 public static class Log
 {
  private static readonly object Gate=new object();
  public static void Error(string area,string message,Exception error)
  {
   if(error==null)return;
   try
   {
    var context=HttpContext.Current;
    if(context==null)return;
    string directory=context.Server.MapPath("~/App_Data/logs");
    Directory.CreateDirectory(directory);
    string path=Path.Combine(directory,DateTime.UtcNow.ToString("yyyyMMdd")+".log");
    string entry=DateTime.UtcNow.ToString("o")+" ["+(area??"Application")+"] "+(message??"Request failed")+Environment.NewLine+error+Environment.NewLine;
    lock(Gate)File.AppendAllText(path,entry,new UTF8Encoding(false));
   }
   catch { /* Logging must not fail the request. */ }
  }
 }
}
