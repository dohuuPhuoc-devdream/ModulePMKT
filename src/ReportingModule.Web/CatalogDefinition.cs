using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ReportingModule.Web
{
 public sealed class CatalogField
 {
  public readonly string Name, Label, Kind;
  public readonly int Length;
  public readonly bool Required;
  public CatalogField(string name, string label, string kind, int length, bool required)
  { Name=name; Label=label; Kind=kind; Length=length; Required=required; }
 }
 public sealed class CatalogDefinition
 {
  public readonly string Id, Table, Title;
  public readonly CatalogField[] Fields;
  private CatalogDefinition(string id,string table,string title,params CatalogField[] fields)
  { Id=id; Table=table; Title=title; Fields=fields; }
  private static CatalogField F(string n,string l,int size,bool required=true)
  { return new CatalogField(n,l,"text",size,required); }
  public static CatalogDefinition Get(string id)
  {
   switch(id)
   {
    case "items": return new CatalogDefinition(id,"HangHoa","Danh mục hàng hóa, vật tư",
     F("MaHang","Mã hàng",20),F("TenHang","Tên hàng",200),F("DonViTinh","Đơn vị tính",30),
     new CatalogField("GiaNhap","Giá nhập","decimal",19,true),new CatalogField("GiaBan","Giá bán","decimal",19,true));
    case "customers": return new CatalogDefinition(id,"KhachHang","Danh mục khách hàng",
     F("MaKH","Mã khách hàng",20),F("TenKH","Tên khách hàng",200),F("MaSoThue","Mã số thuế",20,false),
     F("DiaChi","Địa chỉ",300,false),F("DienThoai","Điện thoại",30,false));
    case "suppliers": return new CatalogDefinition(id,"NhaCungCap","Danh mục nhà cung cấp",
     F("MaNCC","Mã nhà cung cấp",20),F("TenNCC","Tên nhà cung cấp",200),F("MaSoThue","Mã số thuế",20,false),
     F("DiaChi","Địa chỉ",300,false),F("DienThoai","Điện thoại",30,false));
    case "units": return new CatalogDefinition(id,"DonVi","Danh mục đơn vị cơ sở",
     F("MaDonVi","Mã đơn vị",20),F("TenDonVi","Tên đơn vị",200),new CatalogField("NgayBatDau","Ngày bắt đầu","date",10,true),
     F("TenPhapLy","Tên pháp lý",300,false),F("DiaChi","Địa chỉ",300,false),F("MaSoThue","Mã số thuế",20,false),
     F("NguoiDaiDien","Người đại diện",120,false),F("DienThoai","Điện thoại",30,false),F("Email","Email",254,false));
    default: throw new ArgumentException("Danh mục không hợp lệ.");
   }
  }
  public object[] Validate(string[] values)
  {
   // Keep the original three-column unit create/update contract working for existing callers;
   // newly exported templates include the optional legal profile columns.
   if(Id=="units"&&values!=null&&values.Length==3) values=values.Concat(new string[6]).ToArray();
   if(values==null || values.Length!=Fields.Length) throw new ArgumentException("Số cột không đúng.");
   var result=new object[Fields.Length];
   for(int i=0;i<Fields.Length;i++)
   {
    var f=Fields[i]; string v=(values[i]??"").Trim();
    if(v.Length==0) { if(f.Required) throw new ArgumentException(f.Label+": bắt buộc nhập."); result[i]=DBNull.Value; continue; }
    if(f.Kind=="decimal")
    {
     decimal number;
     if(!NumberParser.TryParse(v,out number)
       || number<0 || number>999999999999999.9999m || decimal.Round(number,4)!=number)
      throw new ArgumentException(f.Label+": số không âm, tối đa 15 chữ số nguyên và 4 chữ số thập phân. "+NumberParser.Hint);
     result[i]=number;
    }
    else if(f.Kind=="date")
    {
     DateTime date;
     if(!DateTime.TryParseExact(v,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date) || date < new DateTime(1900,1,1))
      throw new ArgumentException(f.Label+": dùng định dạng yyyy-MM-dd, từ 1900-01-01.");
     result[i]=date;
    }
    else
    {
     if(v.Length>f.Length || v.Any(char.IsControl)) throw new ArgumentException(f.Label+": quá dài hoặc chứa ký tự điều khiển.");
     if(i==0 && !Regex.IsMatch(v,@"\A[A-Za-z0-9_-]+\z")) throw new ArgumentException(f.Label+": chỉ dùng chữ không dấu, số, _ hoặc -.");
     if((f.Name=="MaSoThue"||f.Name=="DienThoai") && v.Any(c=>c>127)) throw new ArgumentException(f.Label+": chỉ dùng ký tự ASCII.");
     if(f.Name=="MaSoThue" && !ValidTaxCode(v)) throw new ArgumentException("Mã số thuế phải gồm 10 hoặc 13 chữ số và có chữ số kiểm tra hợp lệ.");
     if(f.Name=="Email" && !Regex.IsMatch(v,@"\A[^\s@]+@[^\s@]+\.[^\s@]+\z",RegexOptions.CultureInvariant)) throw new ArgumentException("Email không đúng định dạng.");
     result[i]=v;
    }
   }
   return result;
  }
  private static bool ValidTaxCode(string value)
  {
   if(!Regex.IsMatch(value,@"\A(?:\d{10}|\d{13})\z",RegexOptions.CultureInvariant)) return false;
   int[] weights={31,29,23,19,17,13,7,5,3}; int sum=0;
   for(int i=0;i<weights.Length;i++) sum+=(value[i]-'0')*weights[i];
   int remainder=sum%11;
   // A remainder of zero yields a check value of 10, which is not a decimal digit.
   // Do not fold it to zero: that incorrectly accepts some invalid tax codes.
   return remainder!=0 && 10-remainder==value[9]-'0';
  }
  public static string Text(object value)
  {
   if(value==null || value==DBNull.Value) return "";
   if(value is DateTime) return ((DateTime)value).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
   return Convert.ToString(value,CultureInfo.InvariantCulture);
  }
 }
}
