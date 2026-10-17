<%@ Page Language="C#" MasterPageFile="~/Site.Master" CodeBehind="Default.aspx.cs" Inherits="ReportingModule.Web.FunctionPage" %>
<asp:Content ID="MainContent" ContentPlaceHolderID="WorkspaceContent" runat="server">
 <p class="breadcrumb">Hệ thống</p><h1><%: ScreenTitle %></h1>
 <div class="context-toolbar"><a class="catalog-button" href="Default.aspx?screen=general">Làm tươi</a></div>
 <section class="catalog-editor">
 <% if (Found) { %>
  <h2>Ứng dụng kế toán và báo cáo</h2>
  <p>Chọn chức năng từ menu phía trên để nhập danh mục, lập chứng từ hoặc xem báo cáo.</p>
  <ol class="workflow-guide">
   <li>Chọn đơn vị và khoảng ngày, sau đó nhấn Tìm/Lọc.</li>
   <li>Chứng từ mới có thể lưu nháp. Ghi sổ cập nhật số liệu; chứng từ đã ghi sổ không sửa/xóa.</li>
   <li>Làm tươi tải lại dữ liệu theo điều kiện đang áp dụng. In/PDF/Excel lấy toàn bộ kết quả đã hiển thị.</li>
  </ol>
  <h2>Phạm vi nghiệp vụ</h2>
  <p>Mua/bán liên kết kho và công nợ; sổ cái được nhập bằng phiếu kế toán riêng.</p>
  <p class="muted">Tiền hàng chưa gồm thuế/chiết khấu. Bảng cân đối tổng hợp phiếu kế toán đã ghi sổ. PDF là bản ảnh, không tìm kiếm/chọn văn bản.</p>
 <% } else { %>
  <h2>Đường dẫn không hợp lệ</h2><p>Hãy chọn một chức năng có trong menu phía trên.</p>
 <% } %>
 </section>
</asp:Content>
