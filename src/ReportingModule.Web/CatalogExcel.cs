using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ReportingModule.Web
{
 // Deliberately bounded .xlsx exchange, not a general spreadsheet calculation engine.
 public static class CatalogExcel
 {
  private static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
  private static readonly XNamespace R="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
  private static string Column(int index) { string s=""; for(int n=index+1;n>0;n=(n-1)/26) s=(char)('A'+(n-1)%26)+s; return s; }
  private static XElement Cell(string reference,object value,bool date=false)
  {
   if(date && value!=DBNull.Value)
    return new XElement(S+"c",new XAttribute("r",reference),new XAttribute("s","1"),
     new XElement(S+"v",(((DateTime)value).ToOADate()-(((DateTime)value)<new DateTime(1900,3,1)?1:0)).ToString("R",CultureInfo.InvariantCulture)));
   if(value is decimal)
    return new XElement(S+"c",new XAttribute("r",reference),new XElement(S+"v",CatalogDefinition.Text(value)));
   return new XElement(S+"c",new XAttribute("r",reference),new XAttribute("t","inlineStr"),
    new XElement(S+"is",new XElement(S+"t",new XAttribute(XNamespace.Xml+"space","preserve"),CatalogDefinition.Text(value))));
  }
  private static void Write(ZipArchive zip,string path,XDocument xml)
  {
   using(var stream=zip.CreateEntry(path).Open())
   using(var writer=XmlWriter.Create(stream,new XmlWriterSettings { Encoding=new UTF8Encoding(false) })) xml.Save(writer);
  }
  public static byte[] Export(CatalogDefinition d,DataTable table)
  {
   if(table.Rows.Count>5000) throw new ArgumentException("Kết xuất tối đa 5000 dòng. Hãy thu hẹp bộ lọc.");
   using(var memory=new MemoryStream())
   {
    using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true))
    {
     XNamespace ct="http://schemas.openxmlformats.org/package/2006/content-types";
     Write(zip,"[Content_Types].xml",new XDocument(new XElement(ct+"Types",
      new XElement(ct+"Default",new XAttribute("Extension","rels"),new XAttribute("ContentType","application/vnd.openxmlformats-package.relationships+xml")),
      new XElement(ct+"Default",new XAttribute("Extension","xml"),new XAttribute("ContentType","application/xml")),
      new XElement(ct+"Override",new XAttribute("PartName","/xl/workbook.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
      new XElement(ct+"Override",new XAttribute("PartName","/xl/worksheets/sheet1.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
      new XElement(ct+"Override",new XAttribute("PartName","/xl/styles.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")))));
     XNamespace rel="http://schemas.openxmlformats.org/package/2006/relationships";
     Write(zip,"_rels/.rels",new XDocument(new XElement(rel+"Relationships",
      new XElement(rel+"Relationship",new XAttribute("Id","rId1"),new XAttribute("Type",R.NamespaceName+"/officeDocument"),new XAttribute("Target","xl/workbook.xml")))));
     Write(zip,"xl/workbook.xml",new XDocument(new XElement(S+"workbook",new XElement(S+"sheets",
      new XElement(S+"sheet",new XAttribute("name","DanhMuc"),new XAttribute("sheetId","1"),new XAttribute(R+"id","rId1"))))));
     Write(zip,"xl/_rels/workbook.xml.rels",new XDocument(new XElement(rel+"Relationships",
      new XElement(rel+"Relationship",new XAttribute("Id","rId1"),new XAttribute("Type",R.NamespaceName+"/worksheet"),new XAttribute("Target","worksheets/sheet1.xml")),
      new XElement(rel+"Relationship",new XAttribute("Id","rId2"),new XAttribute("Type",R.NamespaceName+"/styles"),new XAttribute("Target","styles.xml")))));
     Write(zip,"xl/styles.xml",XDocument.Parse(@"<styleSheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'>
      <fonts count='1'><font><sz val='11'/><name val='Calibri'/></font></fonts>
      <fills count='2'><fill><patternFill patternType='none'/></fill><fill><patternFill patternType='gray125'/></fill></fills>
      <borders count='1'><border/></borders><cellStyleXfs count='1'><xf numFmtId='0' fontId='0' fillId='0' borderId='0'/></cellStyleXfs>
      <cellXfs count='2'><xf numFmtId='0' fontId='0' fillId='0' borderId='0' xfId='0'/><xf numFmtId='14' fontId='0' fillId='0' borderId='0' xfId='0' applyNumberFormat='1'/></cellXfs>
      <cellStyles count='1'><cellStyle name='Normal' xfId='0' builtinId='0'/></cellStyles></styleSheet>"));
     var data=new XElement(S+"sheetData");
     data.Add(new XElement(S+"row",new XAttribute("r",1),d.Fields.Select((f,i)=>Cell(Column(i)+"1",f.Name))));
     for(int i=0;i<table.Rows.Count;i++)
     {
      var row=new XElement(S+"row",new XAttribute("r",i+2));
      for(int j=0;j<d.Fields.Length;j++) row.Add(Cell(Column(j)+(i+2),table.Rows[i][j],d.Fields[j].Kind=="date"));
      data.Add(row);
     }
     Write(zip,"xl/worksheets/sheet1.xml",new XDocument(new XElement(S+"worksheet",data)));
    }
    return memory.ToArray();
   }
  }
  private static XDocument Read(ZipArchive zip,string path,bool optional=false)
  {
   var entry=zip.GetEntry(path);
   if(entry==null) { if(optional) return null; throw new ArgumentException("File Excel thiếu thành phần: "+path); }
   using(var s=entry.Open())
   using(var r=XmlReader.Create(s,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=20000000 }))
    return XDocument.Load(r);
  }
  public static List<string[]> Import(CatalogDefinition d,Stream input)
  {
   using(var memory=new MemoryStream())
   {
    var buffer=new byte[8192]; int n;
    while((n=input.Read(buffer,0,buffer.Length))>0) { if(memory.Length+n>5*1024*1024) throw new ArgumentException("File tối đa 5 MB."); memory.Write(buffer,0,n); }
    memory.Position=0;
    using(var zip=new ZipArchive(memory,ZipArchiveMode.Read))
    {
     if(zip.Entries.Count>100 || zip.Entries.Sum(e=>e.Length)>20*1024*1024) throw new ArgumentException("Nội dung giải nén vượt giới hạn 20 MB/100 thành phần.");
     if(zip.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=zip.Entries.Count) throw new ArgumentException("File có thành phần trùng tên.");
     if(zip.Entries.Any(e=>e.FullName.EndsWith("vbaProject.bin",StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Không hỗ trợ macro.");
     var workbook=Read(zip,"xl/workbook.xml");
     if(workbook.Descendants(S+"workbookPr").Any(e=>(string)e.Attribute("date1904")=="1" || (string)e.Attribute("date1904")=="true"))
      throw new ArgumentException("Hãy chuyển workbook sang hệ ngày 1900.");
     var sheets=workbook.Descendants(S+"sheet").ToArray();
     if(sheets.Length!=1) throw new ArgumentException("File phải có đúng một sheet.");
     string rid=(string)sheets[0].Attribute(R+"id");
     var relationships=Read(zip,"xl/_rels/workbook.xml.rels");
     XNamespace rel="http://schemas.openxmlformats.org/package/2006/relationships";
     var link=relationships.Root.Elements(rel+"Relationship").SingleOrDefault(e=>(string)e.Attribute("Id")==rid);
     if(link==null || (string)link.Attribute("TargetMode")=="External") throw new ArgumentException("Sheet không hợp lệ.");
     string target=(string)link.Attribute("Target");
     if(string.IsNullOrEmpty(target)||target.Contains("..")||target.Contains("\\")||target.Contains(":")) throw new ArgumentException("Đường dẫn sheet không hợp lệ.");
     string path=target.StartsWith("/")?target.TrimStart('/'):"xl/"+target;
     var sheet=Read(zip,path);
     if(sheet.Descendants(S+"f").Any() || sheet.Descendants(S+"mergeCell").Any()) throw new ArgumentException("Không hỗ trợ công thức hoặc ô gộp; hãy dùng giá trị thuần.");
     var shared=Read(zip,"xl/sharedStrings.xml",true);
     var strings=shared==null?new string[0]:shared.Descendants(S+"si").Select(e=>string.Concat(e.Descendants(S+"t").Select(t=>t.Value))).ToArray();
     var result=new List<string[]>(); int expectedRow=1;
     foreach(var row in sheet.Descendants(S+"sheetData").Elements(S+"row"))
     {
      int rowNumber;
      if(!int.TryParse((string)row.Attribute("r"),out rowNumber)||rowNumber!=expectedRow++) throw new ArgumentException("Các dòng phải liên tục từ dòng 1; xóa dòng trống thừa.");
      if(rowNumber>5001) throw new ArgumentException("Tối đa 5000 dòng dữ liệu.");
      var values=Enumerable.Repeat("",d.Fields.Length).ToArray(); var seen=new HashSet<int>();
      foreach(var cell in row.Elements(S+"c"))
      {
       string reference=(string)cell.Attribute("r")??"";
       int column=0, pos=0;
       while(pos<reference.Length && reference[pos]>='A' && reference[pos]<='Z') { column=checked(column*26+reference[pos]-'A'+1); pos++; }
       int cellRow;
       if(column<1||column>d.Fields.Length||!int.TryParse(reference.Substring(pos),out cellRow)||cellRow!=rowNumber||!seen.Add(column))
        throw new ArgumentException("Dòng "+rowNumber+": ô/cột không hợp lệ.");
       string type=(string)cell.Attribute("t"); string value=(string)cell.Element(S+"v")??"";
       if(type=="inlineStr") value=string.Concat(cell.Descendants(S+"t").Select(t=>t.Value));
       else if(type=="s") { int index; if(!int.TryParse(value,out index)||index<0||index>=strings.Length) throw new ArgumentException("Shared string không hợp lệ."); value=strings[index]; }
       else if(type!=null && type!="n" && type!="str" && type!="d") throw new ArgumentException("Dòng "+rowNumber+": kiểu ô không hỗ trợ.");
       if(rowNumber>1 && value.Length>0 && (type==null||type=="n"))
       {
        decimal numeric;
        if(d.Fields[column-1].Kind=="date")
        {
         double serial;
         if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out serial)||serial<1||serial>=2958466||serial!=Math.Floor(serial)||serial==60) throw new ArgumentException("Ngày Excel không hợp lệ.");
         value=DateTime.FromOADate(serial<60?serial+1:serial).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        }
        else if(d.Fields[column-1].Kind=="decimal" && decimal.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out numeric))
         value=NumberParser.FormatInput(numeric);
       }
       values[column-1]=value;
      }
      if(rowNumber==1) { if(!values.SequenceEqual(d.Fields.Select(f=>f.Name))) throw new ArgumentException("Header/thứ tự cột phải khớp file mẫu."); }
      else result.Add(values);
     }
     if(expectedRow==1) throw new ArgumentException("Sheet không có header.");
     return result;
    }
   }
  }
 }
}
