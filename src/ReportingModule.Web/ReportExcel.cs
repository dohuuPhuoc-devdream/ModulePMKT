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
 public static class ReportExcel
 {
  private static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
  private static readonly XNamespace R="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
  private static string Column(int index) { string s=""; for(int n=index+1;n>0;n=(n-1)/26) s=(char)('A'+(n-1)%26)+s; return s; }
  private static XElement Cell(string reference,object value,bool date=false)
  {
   if(date && value!=DBNull.Value)
    return new XElement(S+"c",new XAttribute("r",reference),new XAttribute("s","1"),
     new XElement(S+"v",(((DateTime)value).ToOADate()-(((DateTime)value)<new DateTime(1900,3,1)?1:0)).ToString("R",CultureInfo.InvariantCulture)));
   if((value is decimal || value is int || value is long) && CatalogDefinition.Text(value).Replace("-", "").Replace(".", "").TrimStart('0').Length<=15)
    return new XElement(S+"c",new XAttribute("r",reference),new XElement(S+"v",CatalogDefinition.Text(value)));
   return new XElement(S+"c",new XAttribute("r",reference),new XAttribute("t","inlineStr"),
    new XElement(S+"is",new XElement(S+"t",new XAttribute(XNamespace.Xml+"space","preserve"),CatalogDefinition.Text(value))));
  }
  private static void Write(ZipArchive zip,string path,XDocument xml)
  {
   using(var stream=zip.CreateEntry(path).Open())
   using(var writer=XmlWriter.Create(stream,new XmlWriterSettings { Encoding=new UTF8Encoding(false) })) xml.Save(writer);
  }
  public static byte[] Export(OutputSnapshot snapshot)
  {
   if(ReportOutput.IsDocument(snapshot.Screen)) return ExportVoucher(snapshot);
   var table=snapshot.Data;
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
      new XElement(S+"sheet",new XAttribute("name","BaoCao"),new XAttribute("sheetId","1"),new XAttribute(R+"id","rId1"))))));
     Write(zip,"xl/_rels/workbook.xml.rels",new XDocument(new XElement(rel+"Relationships",
      new XElement(rel+"Relationship",new XAttribute("Id","rId1"),new XAttribute("Type",R.NamespaceName+"/worksheet"),new XAttribute("Target","worksheets/sheet1.xml")),
      new XElement(rel+"Relationship",new XAttribute("Id","rId2"),new XAttribute("Type",R.NamespaceName+"/styles"),new XAttribute("Target","styles.xml")))));
     Write(zip,"xl/styles.xml",XDocument.Parse(@"<styleSheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'>
      <numFmts count='2'><numFmt numFmtId='164' formatCode='#,##0.0000'/><numFmt numFmtId='165' formatCode='#,##0.000'/></numFmts><fonts count='1'><font><sz val='11'/><name val='Calibri'/></font></fonts>
      <fills count='2'><fill><patternFill patternType='none'/></fill><fill><patternFill patternType='gray125'/></fill></fills>
      <borders count='1'><border/></borders><cellStyleXfs count='1'><xf numFmtId='0' fontId='0' fillId='0' borderId='0'/></cellStyleXfs>
      <cellXfs count='4'><xf numFmtId='0' fontId='0' fillId='0' borderId='0' xfId='0'/><xf numFmtId='14' fontId='0' fillId='0' borderId='0' xfId='0' applyNumberFormat='1'/><xf numFmtId='164' fontId='0' fillId='0' borderId='0' xfId='0' applyNumberFormat='1'/><xf numFmtId='165' fontId='0' fillId='0' borderId='0' xfId='0' applyNumberFormat='1'/></cellXfs>
      <cellStyles count='1'><cellStyle name='Normal' xfId='0' builtinId='0'/></cellStyles></styleSheet>"));
     var data=new XElement(S+"sheetData");
     var metadata=new[]{snapshot.Title,snapshot.Criteria,"Thời điểm dữ liệu UTC: "+snapshot.Created.ToString("yyyy-MM-dd HH:mm:ss"),snapshot.Summary};
     for(int i=0;i<metadata.Length;i++) data.Add(new XElement(S+"row",new XAttribute("r",i+1),Cell("A"+(i+1),metadata[i])));
     data.Add(new XElement(S+"row",new XAttribute("r",5),snapshot.Columns.Select((c,i)=>Cell(Column(i)+"5",c.Title))));
     for(int i=0;i<table.Rows.Count;i++) {
      var row=new XElement(S+"row",new XAttribute("r",i+6));
      for(int j=0;j<snapshot.Columns.Length;j++) { object v=table.Rows[i][snapshot.Columns[j].Field]; var cell=Cell(Column(j)+(i+6),v,v is DateTime); if(v is decimal && cell.Attribute("t")==null) cell.SetAttributeValue("s",snapshot.Columns[j].Format.Contains("N3")?3:2); row.Add(cell); }
      data.Add(row);
     }     Write(zip,"xl/worksheets/sheet1.xml",new XDocument(new XElement(S+"worksheet",new XElement(S+"sheetViews",new XElement(S+"sheetView",new XAttribute("workbookViewId",0),new XElement(S+"pane",new XAttribute("ySplit",5),new XAttribute("topLeftCell","A6"),new XAttribute("state","frozen")))),new XElement(S+"cols",snapshot.Columns.Select((c,i)=>new XElement(S+"col",new XAttribute("min",i+1),new XAttribute("max",i+1),new XAttribute("width",c.Format==""?28:20),new XAttribute("customWidth",1)))),data)));
    }
    return memory.ToArray();
   }
  }
  private static XElement Styled(string address,object value,int style,bool date=false)
  { var cell=Cell(address,value,date); if(style>0) cell.SetAttributeValue("s",style); return cell; }
  private static byte[] ExportVoucher(OutputSnapshot s)
  {
   if(s.Data.Rows.Count>5000) throw new ArgumentException("Kết xuất tối đa 5000 dòng.");
   int columnCount=Math.Max(5,s.Columns.Length+1);string last=Column(columnCount-1);
   var rows=new List<XElement>();
   Action<int,int,object,int> put=(rowIndex,colIndex,v,style)=>{
    while(rows.Count<rowIndex) rows.Add(new XElement(S+"row",new XAttribute("r",rows.Count+1)));
    rows[rowIndex-1].Add(Styled(Column(colIndex)+(rowIndex),v,style));
   };
   Action<int,int,string,int> label=(rowIndex,colIndex,v,style)=>put(rowIndex,colIndex,v,style);
   label(1,0,"PicoCorp Accounting",4);
   put(2,0,s.UnitName??"",7);
   put(3,0,(s.Title??"").ToUpperInvariant(),5);
   put(4,0,"Ngày "+s.DocumentDate.ToString("dd 'tháng' MM 'năm' yyyy",CultureInfo.GetCultureInfo("vi-VN")),7);
   put(4,2,"Số chứng từ: "+(s.DocumentNumber??""),7);
   label(6,0,"Đơn vị",6);put(6,1,(s.UnitName??"")+(string.IsNullOrEmpty(s.UnitCode)?"":" ("+s.UnitCode+")"),7);
   int r=7;
   if(!string.IsNullOrEmpty(s.PartnerCode)){label(r,0,s.PartnerLabel??"Đối tác",6);put(r,1,(s.PartnerName??"")+(string.IsNullOrEmpty(s.PartnerCode)?"":" ("+s.PartnerCode+")"),7);r++;}
   if(!string.IsNullOrEmpty(s.PartnerAddress)){label(r,0,"Địa chỉ",6);put(r,1,s.PartnerAddress,7);r++;}
   if(!string.IsNullOrEmpty(s.DocumentNote)){label(r,0,"Diễn giải",6);put(r,1,s.DocumentNote,7);r++;}
   r+=1;int headerRow=r;put(r,0,"STT",8);
   for(int c=0;c<s.Columns.Length;c++) put(r,c+1,s.Columns[c].Title,8);
   int firstLine=headerRow+1,index=0;
   foreach(DataRow dataRow in s.Data.Rows){r=firstLine+index;put(r,0,++index,9);for(int c=0;c<s.Columns.Length;c++){
     var outputColumn=s.Columns[c];object value=dataRow[outputColumn.Field];int style=outputColumn.Format.Contains("N3")?10:outputColumn.Format.Contains("N")?11:9;
     put(r,c+1,value,style);
    }}
   int totalRow=firstLine+s.Data.Rows.Count+1;
   put(totalRow,Math.Max(0,columnCount-2),"Tổng cộng",12);
   decimal total=0m;
   if(s.Data.Columns.Contains("ThanhTien")) { foreach(DataRow dr in s.Data.Rows) if(dr["ThanhTien"]!=DBNull.Value) total+=Convert.ToDecimal(dr["ThanhTien"]); }
   else if(s.Data.Columns.Contains("PhatSinhNo")) { foreach(DataRow dr in s.Data.Rows) if(dr["PhatSinhNo"]!=DBNull.Value) total+=Convert.ToDecimal(dr["PhatSinhNo"]); }
   put(totalRow,columnCount-1,total,13);
   int signRow=totalRow+3;string[] signatures=ReportOutput.SignatureLabels(s.Screen);
   for(int i=0;i<signatures.Length;i++){int c=i*columnCount/signatures.Length;put(signRow,c,signatures[i],14);put(signRow+1,c,"(Ký, họ tên)",7);}
   var merges=new List<XElement>{new XElement(S+"mergeCell",new XAttribute("ref","A1:"+last+"1")),new XElement(S+"mergeCell",new XAttribute("ref","A2:"+last+"2")),new XElement(S+"mergeCell",new XAttribute("ref","A3:"+last+"3")),new XElement(S+"mergeCell",new XAttribute("ref","A4:B4")),new XElement(S+"mergeCell",new XAttribute("ref","C4:"+last+"4"))};
   for(int mr=6;mr<headerRow;mr++) if(rows[mr-1].Elements(S+"c").Count()>1) merges.Add(new XElement(S+"mergeCell",new XAttribute("ref","B"+mr+":"+last+mr)));
   var columns=new XElement(S+"cols",Enumerable.Range(1,columnCount).Select(i=>new XElement(S+"col",new XAttribute("min",i),new XAttribute("max",i),new XAttribute("width",i==1?9:i==2?24:18),new XAttribute("customWidth",1))));
   var sheet=new XElement(S+"worksheet",new XElement(S+"sheetViews",new XElement(S+"sheetView",new XAttribute("workbookViewId",0))),columns,new XElement(S+"sheetFormatPr",new XAttribute("defaultRowHeight",22)),new XElement(S+"sheetData",rows),new XElement(S+"mergeCells",new XAttribute("count",merges.Count),merges),new XElement(S+"printOptions",new XAttribute("horizontalCentered",1)),new XElement(S+"pageMargins",new XAttribute("left",0.3),new XAttribute("right",0.3),new XAttribute("top",0.5),new XAttribute("bottom",0.5),new XAttribute("header",0.2),new XAttribute("footer",0.2)),new XElement(S+"pageSetup",new XAttribute("paperSize",9),new XAttribute("orientation","portrait"),new XAttribute("fitToWidth",1),new XAttribute("fitToHeight",0)));
   XDocument styles=XDocument.Parse(@"<styleSheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'>
    <numFmts count='2'><numFmt numFmtId='164' formatCode='#,##0.0000'/><numFmt numFmtId='165' formatCode='#,##0.000'/></numFmts>
    <fonts count='4'><font><sz val='10'/><name val='Arial'/></font><font><b/><sz val='11'/><color rgb='FF174D73'/><name val='Arial'/></font><font><b/><sz val='16'/><name val='Arial'/></font><font><b/><sz val='10'/><name val='Arial'/></font></fonts>
    <fills count='4'><fill><patternFill patternType='none'/></fill><fill><patternFill patternType='gray125'/></fill><fill><patternFill patternType='solid'><fgColor rgb='FFE8EEF3'/><bgColor indexed='64'/></patternFill></fill><fill><patternFill patternType='solid'><fgColor rgb='FFF4F7FA'/><bgColor indexed='64'/></patternFill></fill></fills>
    <borders count='3'><border/><border><left style='thin'><color rgb='FF9EABB7'/></left><right style='thin'><color rgb='FF9EABB7'/></right><top style='thin'><color rgb='FF9EABB7'/></top><bottom style='thin'><color rgb='FF9EABB7'/></bottom></border><border><bottom style='medium'><color rgb='FF174D73'/></bottom></border></borders>
    <cellStyleXfs count='1'><xf numFmtId='0' fontId='0' fillId='0' borderId='0'/></cellStyleXfs>
    <cellXfs count='15'><xf numFmtId='0' fontId='0' fillId='0' borderId='0' xfId='0'/><xf numFmtId='14' fontId='0' fillId='0' borderId='0' xfId='0' applyNumberFormat='1'/><xf numFmtId='164' fontId='0' fillId='0' borderId='0' xfId='0' applyNumberFormat='1'/><xf numFmtId='165' fontId='0' fillId='0' borderId='0' xfId='0' applyNumberFormat='1'/>
    <xf numFmtId='0' fontId='1' fillId='0' borderId='2' xfId='0' applyFont='1' applyBorder='1' applyAlignment='1'><alignment vertical='center'/></xf><xf numFmtId='0' fontId='2' fillId='0' borderId='0' xfId='0' applyFont='1' applyAlignment='1'><alignment horizontal='center' vertical='center'/></xf><xf numFmtId='0' fontId='3' fillId='3' borderId='1' xfId='0' applyFont='1' applyFill='1' applyBorder='1'/><xf numFmtId='0' fontId='0' fillId='0' borderId='1' xfId='0' applyBorder='1' applyAlignment='1'><alignment wrapText='1' vertical='center'/></xf><xf numFmtId='0' fontId='3' fillId='2' borderId='1' xfId='0' applyFont='1' applyFill='1' applyBorder='1' applyAlignment='1'><alignment horizontal='center' vertical='center' wrapText='1'/></xf><xf numFmtId='0' fontId='0' fillId='0' borderId='1' xfId='0' applyBorder='1' applyAlignment='1'><alignment horizontal='center' vertical='center'/></xf><xf numFmtId='165' fontId='0' fillId='0' borderId='1' xfId='0' applyNumberFormat='1' applyBorder='1' applyAlignment='1'><alignment horizontal='right' vertical='center'/></xf><xf numFmtId='164' fontId='0' fillId='0' borderId='1' xfId='0' applyNumberFormat='1' applyBorder='1' applyAlignment='1'><alignment horizontal='right' vertical='center'/></xf><xf numFmtId='0' fontId='3' fillId='2' borderId='1' xfId='0' applyFont='1' applyFill='1' applyBorder='1' applyAlignment='1'><alignment horizontal='right'/></xf><xf numFmtId='164' fontId='3' fillId='2' borderId='1' xfId='0' applyFont='1' applyFill='1' applyNumberFormat='1' applyBorder='1' applyAlignment='1'><alignment horizontal='right'/></xf><xf numFmtId='0' fontId='3' fillId='0' borderId='0' xfId='0' applyFont='1' applyAlignment='1'><alignment horizontal='center' vertical='center'/></xf></cellXfs>
    <cellStyles count='1'><cellStyle name='Normal' xfId='0' builtinId='0'/></cellStyles></styleSheet>");
   using(var memory=new MemoryStream()) {
    using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)){
     XNamespace ct="http://schemas.openxmlformats.org/package/2006/content-types", rel="http://schemas.openxmlformats.org/package/2006/relationships";
     Write(zip,"[Content_Types].xml",new XDocument(new XElement(ct+"Types",new XElement(ct+"Default",new XAttribute("Extension","rels"),new XAttribute("ContentType","application/vnd.openxmlformats-package.relationships+xml")),new XElement(ct+"Default",new XAttribute("Extension","xml"),new XAttribute("ContentType","application/xml")),new XElement(ct+"Override",new XAttribute("PartName","/xl/workbook.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),new XElement(ct+"Override",new XAttribute("PartName","/xl/worksheets/sheet1.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),new XElement(ct+"Override",new XAttribute("PartName","/xl/styles.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")))));
     Write(zip,"_rels/.rels",new XDocument(new XElement(rel+"Relationships",new XElement(rel+"Relationship",new XAttribute("Id","rId1"),new XAttribute("Type",R.NamespaceName+"/officeDocument"),new XAttribute("Target","xl/workbook.xml")))));
     Write(zip,"xl/workbook.xml",new XDocument(new XElement(S+"workbook",new XElement(S+"sheets",new XElement(S+"sheet",new XAttribute("name","ChungTu"),new XAttribute("sheetId",1),new XAttribute(R+"id","rId1"))),new XElement(S+"definedNames",new XElement(S+"definedName",new XAttribute("name","_xlnm.Print_Area"),new XAttribute("localSheetId",0),"'ChungTu'!$A$1:$"+last+"$"+(signRow+1))))));
     Write(zip,"xl/_rels/workbook.xml.rels",new XDocument(new XElement(rel+"Relationships",new XElement(rel+"Relationship",new XAttribute("Id","rId1"),new XAttribute("Type",R.NamespaceName+"/worksheet"),new XAttribute("Target","worksheets/sheet1.xml")),new XElement(rel+"Relationship",new XAttribute("Id","rId2"),new XAttribute("Type",R.NamespaceName+"/styles"),new XAttribute("Target","styles.xml")))));
     Write(zip,"xl/styles.xml",styles);Write(zip,"xl/worksheets/sheet1.xml",new XDocument(sheet));
    }
    return memory.ToArray();
   }
  }
 }
}

