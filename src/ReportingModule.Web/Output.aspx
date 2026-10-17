<%@ Page Language="C#" MasterPageFile="~/Site.Master" CodeBehind="Output.aspx.cs" Inherits="ReportingModule.Web.OutputPage" %>
<asp:Content ID="OutputContent" ContentPlaceHolderID="WorkspaceContent" runat="server">
 <div class="context-toolbar no-print"><button type="button" class="catalog-button" onclick="window.print()">In</button><asp:HyperLink ID="Excel" runat="server" CssClass="catalog-button">Excel</asp:HyperLink><asp:HyperLink ID="Pdf" runat="server" CssClass="catalog-button">PDF</asp:HyperLink><asp:HyperLink ID="Back" runat="server" CssClass="catalog-button">Trở lại</asp:HyperLink></div>
 <asp:Literal ID="Report" runat="server" />
</asp:Content>
