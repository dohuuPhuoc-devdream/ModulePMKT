using System;
namespace ReportingModule.Web
{
 public sealed class MenuNode
 {
  public string Id { get; private set; }
  public string Title { get; private set; }
  public string Kind { get; private set; }
  public MenuNode[] Children { get; private set; }
  public MenuNode(string id, string title, string kind) { Id = id; Title = title; Kind = kind; Children = new MenuNode[0]; }
  public MenuNode(string title, params MenuNode[] children) { Title = title; Children = children; }
  public bool Contains(string id) { if (Id == id) return true; foreach (var child in Children) if (child.Contains(id)) return true; return false; }
 }
 public static class Navigation
 {
  public static readonly MenuNode[] Modules = new[] {
   new MenuNode("Hệ thống", new MenuNode("general", "Thông tin chung", "info"), new MenuNode("units", "Danh mục đơn vị cơ sở", "catalog"),new MenuNode("admin-users","Quản lý người dùng","admin"),new MenuNode("admin-roles","Vai trò & phân quyền","admin"),new MenuNode("admin-audit","Nhật ký hệ thống","admin")),
   new MenuNode("Tổng hợp", new MenuNode("journal", "Phiếu kế toán tổng hợp", "document"), new MenuNode("Báo cáo tài chính", new MenuNode("balance", "Bảng cân đối kế toán", "report"))),
   new MenuNode("Phải thu", new MenuNode("ar-invoice", "Hóa đơn bán hàng", "document"), new MenuNode("customers", "Danh mục khách hàng", "catalog"), new MenuNode("receivables", "Báo cáo công nợ khách hàng", "report")),
   new MenuNode("Phải trả", new MenuNode("ap-invoice", "Hóa đơn mua hàng trong nước", "document"), new MenuNode("suppliers", "Danh mục nhà cung cấp", "catalog"), new MenuNode("payables", "Báo cáo công nợ nhà cung cấp", "report")),
   new MenuNode("Bán hàng", new MenuNode("sales-order", "Đơn hàng bán", "document"), new MenuNode("sales-invoice", "Hóa đơn bán hàng", "document"), new MenuNode("Báo cáo bán hàng", new MenuNode("sales-detail", "Chi tiết bán hàng", "report"), new MenuNode("revenue", "Doanh thu bán hàng", "report")), new MenuNode("Báo cáo phân tích", new MenuNode("sales-analysis", "Phân tích doanh thu", "report"))),
   new MenuNode("Mua hàng", new MenuNode("purchase-order", "Đơn hàng mua trong nước", "document"), new MenuNode("purchase-receipt", "Phiếu nhập mua hàng", "document"), new MenuNode("Báo cáo mua hàng", new MenuNode("purchase-detail", "Chi tiết nhập/mua hàng", "report")), new MenuNode("Báo cáo phân tích", new MenuNode("purchase-analysis", "Phân tích mua hàng", "report"))),
   new MenuNode("Tồn kho", new MenuNode("stock-receipt", "Phiếu nhập kho", "document"), new MenuNode("stock-issue", "Phiếu xuất kho", "document"), new MenuNode("items", "Danh mục hàng hóa, vật tư", "catalog"), new MenuNode("inbound", "Báo cáo hàng nhập", "report"), new MenuNode("outbound", "Báo cáo hàng xuất", "report"), new MenuNode("Báo cáo hàng tồn", new MenuNode("inventory", "Nhập - Xuất - Tồn", "report")))
  };
  public static string PageFor(string id)
  {
   if (id=="stock-receipt" || id=="stock-issue" || id=="purchase-receipt" || id=="inbound" || id=="outbound" || id=="inventory") return "Stock.aspx";
   if (id=="items" || id=="customers" || id=="suppliers" || id=="units") return "Catalog.aspx";
   if (Array.IndexOf(new[]{"sales-order","sales-invoice","ar-invoice","purchase-order","ap-invoice","sales-detail","revenue","sales-analysis","purchase-detail","purchase-analysis","receivables","payables"},id)>=0) return "Commerce.aspx";
   if (id=="journal" || id=="balance") return "Journal.aspx";
   if (id=="admin-users"||id=="admin-roles"||id=="admin-audit") return "SystemAdmin.aspx";
   return "Default.aspx";
  }
  public static MenuNode Find(string id, out string module)
  {
   foreach (var root in Modules) { var found = FindNode(root, id); if (found != null) { module = root.Title; return found; } }
   module = "Chức năng"; return null;
  }
  private static MenuNode FindNode(MenuNode node, string id)
  {
   if (node.Id == id && node.Children.Length == 0) return node;
   foreach (var child in node.Children) { var found = FindNode(child, id); if (found != null) return found; }
   return null;
  }
 }
}



