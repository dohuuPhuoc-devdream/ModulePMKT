<%@ Page Language="C#" CodeBehind="Login.aspx.cs" Inherits="ReportingModule.Web.LoginPage" ViewStateEncryptionMode="Always" EnableViewStateMac="true" EnableEventValidation="true" %>
<!DOCTYPE html>
<html lang="vi"><head runat="server"><meta charset="utf-8" /><meta name="viewport" content="width=device-width, initial-scale=1" /><title>Đăng nhập | <%: ReportingModule.Web.AppIdentity.ProductName %></title><link href="Content/site.css" rel="stylesheet" /></head>
<body class="login-body"><form id="LoginForm" runat="server"><main class="login-panel"><a class="app-name" href="Login.aspx"><%: ReportingModule.Web.AppIdentity.ProductName %></a><h1>Đăng nhập</h1><p class="muted">Đăng nhập để tiếp tục làm việc.</p>
<asp:HiddenField ID="Csrf" runat="server" /><asp:Label ID="Message" runat="server" CssClass="catalog-message" role="alert" />
<div class="catalog-field"><asp:Label ID="UsernameLabel" runat="server" AssociatedControlID="Username" Text="Tên đăng nhập" /><asp:TextBox ID="Username" runat="server" MaxLength="64" autocomplete="username" /></div>
<div class="catalog-field"><asp:Label ID="PasswordLabel" runat="server" AssociatedControlID="Password" Text="Mật khẩu" /><asp:TextBox ID="Password" runat="server" TextMode="Password" MaxLength="256" autocomplete="current-password" /></div>
<asp:Button ID="SignIn" runat="server" Text="Đăng nhập" CssClass="catalog-button login-submit" OnClick="SignIn_Click" />
</main></form></body></html>
