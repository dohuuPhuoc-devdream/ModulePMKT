SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @lock<0 THROW 51600,'Cannot acquire schema lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=5) THROW 51601,'Ledger schema version 5 required.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=6)
BEGIN
 CREATE TABLE dbo.AppUser(
  UserId uniqueidentifier NOT NULL CONSTRAINT DF_AppUser_Id DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_AppUser PRIMARY KEY,
  Username nvarchar(64) NOT NULL, NormalizedUsername nvarchar(64) NOT NULL,
  PasswordHash varbinary(32) NOT NULL, PasswordSalt varbinary(16) NOT NULL,
  HashIterations int NOT NULL CONSTRAINT DF_AppUser_Iterations DEFAULT 310000,
  HashVersion smallint NOT NULL CONSTRAINT DF_AppUser_HashVersion DEFAULT 1,
  DisplayName nvarchar(120) NOT NULL, IsEnabled bit NOT NULL CONSTRAINT DF_AppUser_Enabled DEFAULT 1,
  FailedLoginCount int NOT NULL CONSTRAINT DF_AppUser_Failed DEFAULT 0,
  LockedUntilUtc datetime2(0) NULL, CreatedAtUtc datetime2(0) NOT NULL CONSTRAINT DF_AppUser_Created DEFAULT SYSUTCDATETIME(),
  CreatedBy uniqueidentifier NULL, UpdatedAtUtc datetime2(0) NULL, UpdatedBy uniqueidentifier NULL,
  LastLoginAtUtc datetime2(0) NULL, MustChangePassword bit NOT NULL CONSTRAINT DF_AppUser_Change DEFAULT 0,
  CONSTRAINT UQ_AppUser_NormalizedUsername UNIQUE(NormalizedUsername),
  CONSTRAINT CK_AppUser_Hash CHECK(HashIterations BETWEEN 100000 AND 1000000 AND HashVersion=1 AND DATALENGTH(PasswordSalt)=16 AND DATALENGTH(PasswordHash)=32),
  CONSTRAINT CK_AppUser_Failures CHECK(FailedLoginCount BETWEEN 0 AND 5));
 CREATE TABLE dbo.AppRole(
  RoleId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppRole PRIMARY KEY,
  RoleCode varchar(40) NOT NULL CONSTRAINT UQ_AppRole_Code UNIQUE,
  RoleName nvarchar(100) NOT NULL, IsSystem bit NOT NULL CONSTRAINT DF_AppRole_System DEFAULT 0,
  IsEnabled bit NOT NULL CONSTRAINT DF_AppRole_Enabled DEFAULT 1,
  CreatedAtUtc datetime2(0) NOT NULL CONSTRAINT DF_AppRole_Created DEFAULT SYSUTCDATETIME());
 CREATE TABLE dbo.AppPermission(
  PermissionCode varchar(120) NOT NULL CONSTRAINT PK_AppPermission PRIMARY KEY,
  ModuleCode varchar(30) NOT NULL, FunctionCode varchar(50) NOT NULL, ActionCode varchar(30) NOT NULL,
  DisplayName nvarchar(160) NOT NULL, SortOrder int NOT NULL,
  CONSTRAINT UQ_AppPermission_FunctionAction UNIQUE(FunctionCode,ActionCode));
 CREATE TABLE dbo.AppUserRole(
  UserId uniqueidentifier NOT NULL CONSTRAINT FK_AppUserRole_User REFERENCES dbo.AppUser(UserId),
  RoleId int NOT NULL CONSTRAINT FK_AppUserRole_Role REFERENCES dbo.AppRole(RoleId),
  AssignedAtUtc datetime2(0) NOT NULL CONSTRAINT DF_AppUserRole_Assigned DEFAULT SYSUTCDATETIME(),
  AssignedBy uniqueidentifier NULL, CONSTRAINT PK_AppUserRole PRIMARY KEY(UserId,RoleId));
 CREATE TABLE dbo.AppRolePermission(
  RoleId int NOT NULL CONSTRAINT FK_AppRolePermission_Role REFERENCES dbo.AppRole(RoleId),
  PermissionCode varchar(120) NOT NULL CONSTRAINT FK_AppRolePermission_Permission REFERENCES dbo.AppPermission(PermissionCode),
  CONSTRAINT PK_AppRolePermission PRIMARY KEY(RoleId,PermissionCode));
 CREATE TABLE dbo.AppUserUnit(
  UserId uniqueidentifier NOT NULL CONSTRAINT FK_AppUserUnit_User REFERENCES dbo.AppUser(UserId),
  MaDonVi varchar(20) NOT NULL CONSTRAINT FK_AppUserUnit_Unit REFERENCES dbo.DonVi(MaDonVi),
  AssignedAtUtc datetime2(0) NOT NULL CONSTRAINT DF_AppUserUnit_Assigned DEFAULT SYSUTCDATETIME(),
  AssignedBy uniqueidentifier NULL, CONSTRAINT PK_AppUserUnit PRIMARY KEY(UserId,MaDonVi));
 CREATE TABLE dbo.AuditLog(
  AuditId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
  OccurredAtUtc datetime2(0) NOT NULL CONSTRAINT DF_AuditLog_Date DEFAULT SYSUTCDATETIME(),
  ActorUserId uniqueidentifier NULL, ActorUsername nvarchar(64) NOT NULL,
  UnitCode varchar(20) NULL, ModuleCode varchar(30) NOT NULL, FunctionCode varchar(50) NOT NULL,
  ActionCode varchar(30) NOT NULL, EntityIdentifier nvarchar(150) NULL,
  Outcome varchar(12) NOT NULL, SafeSummary nvarchar(500) NOT NULL,
  CONSTRAINT FK_AuditLog_User FOREIGN KEY(ActorUserId) REFERENCES dbo.AppUser(UserId),
  CONSTRAINT CK_AuditLog_Outcome CHECK(Outcome IN('SUCCESS','FAILURE','DENIED')));
 CREATE INDEX IX_AuditLog_Date ON dbo.AuditLog(OccurredAtUtc DESC,AuditId DESC);
 CREATE INDEX IX_AuditLog_Actor ON dbo.AuditLog(ActorUserId,OccurredAtUtc DESC);
 EXEC(N'CREATE TRIGGER dbo.TR_AuditLog_AppendOnly ON dbo.AuditLog INSTEAD OF UPDATE,DELETE AS BEGIN SET NOCOUNT ON; THROW 51603, ''Audit records are append-only.'', 1; END');
 INSERT dbo.AppRole(RoleCode,RoleName,IsSystem) VALUES('Administrator',N'Quản trị hệ thống',1),('User',N'Người dùng',1);

 DECLARE @Functions TABLE(ModuleCode varchar(30),FunctionCode varchar(50),DisplayName nvarchar(120),FunctionType varchar(12),SortOrder int);
 INSERT @Functions VALUES
 ('SYSTEM','GENERAL',N'Thông tin chung','READ',10),('SYSTEM','UNIT',N'Danh mục đơn vị cơ sở','CATALOG',20),
 ('GENERAL','JOURNAL',N'Phiếu kế toán tổng hợp','DOCUMENT',30),('GENERAL','BALANCE',N'Bảng cân đối kế toán','REPORT',40),
 ('AR','AR_INVOICE',N'Hóa đơn bán hàng','DOCUMENT',50),('AR','CUSTOMERS',N'Danh mục khách hàng','CATALOG',60),('AR','RECEIVABLES',N'Báo cáo công nợ khách hàng','REPORT',70),
 ('AP','AP_INVOICE',N'Hóa đơn mua hàng trong nước','DOCUMENT',80),('AP','SUPPLIERS',N'Danh mục nhà cung cấp','CATALOG',90),('AP','PAYABLES',N'Báo cáo công nợ nhà cung cấp','REPORT',100),
 ('SALES','SALES_ORDER',N'Đơn hàng bán','DOCUMENT',110),('SALES','SALES_INVOICE',N'Hóa đơn bán hàng','DOCUMENT',120),('SALES','SALES_DETAIL',N'Chi tiết bán hàng','REPORT',130),('SALES','REVENUE',N'Doanh thu bán hàng','REPORT',140),('SALES','SALES_ANALYSIS',N'Phân tích doanh thu','REPORT',150),
 ('PURCHASE','PURCHASE_ORDER',N'Đơn hàng mua trong nước','DOCUMENT',160),('PURCHASE','PURCHASE_RECEIPT',N'Phiếu nhập mua hàng','DOCUMENT',170),('PURCHASE','PURCHASE_DETAIL',N'Chi tiết nhập/mua hàng','REPORT',180),('PURCHASE','PURCHASE_ANALYSIS',N'Phân tích mua hàng','REPORT',190),
 ('STOCK','STOCK_RECEIPT',N'Phiếu nhập kho','DOCUMENT',200),('STOCK','STOCK_ISSUE',N'Phiếu xuất kho','DOCUMENT',210),('STOCK','ITEMS',N'Danh mục hàng hóa, vật tư','CATALOG',220),('STOCK','INBOUND',N'Báo cáo hàng nhập','REPORT',230),('STOCK','OUTBOUND',N'Báo cáo hàng xuất','REPORT',240),('STOCK','INVENTORY',N'Nhập - Xuất - Tồn','REPORT',250),
 ('SYSTEM','ADMIN_USERS',N'Quản lý người dùng','ADMIN',260),('SYSTEM','ADMIN_ROLES',N'Vai trò & phân quyền','ADMIN',270),('SYSTEM','ADMIN_AUDIT',N'Nhật ký hệ thống','ADMIN',280);
 DECLARE @Actions TABLE(ActionCode varchar(30),DisplayName nvarchar(60),SortOrder int,FunctionType varchar(12));
 INSERT @Actions VALUES
 ('VIEW',N'Xem',1,'ALL'),('CREATE',N'Tạo',2,'CATALOG'),('EDIT',N'Sửa',3,'CATALOG'),('DELETE',N'Xóa',4,'CATALOG'),('IMPORT',N'Import',5,'CATALOG'),('EXPORT',N'Export',6,'CATALOG'),
 ('CREATE',N'Tạo',2,'DOCUMENT'),('EDIT',N'Sửa',3,'DOCUMENT'),('DELETE_DRAFT',N'Xóa nháp',4,'DOCUMENT'),('POST',N'Ghi sổ',5,'DOCUMENT'),('PRINT',N'In',6,'DOCUMENT'),('PAYMENT',N'Ghi nhận thanh toán',7,'DOCUMENT'),
 ('EXPORT',N'Kết xuất',2,'REPORT'),('PRINT',N'In',3,'REPORT'),('ADMIN',N'Quản trị',2,'ADMIN'),('ADMIN',N'Quản trị danh mục đơn vị',7,'CATALOG');
 INSERT dbo.AppPermission(PermissionCode,ModuleCode,FunctionCode,ActionCode,DisplayName,SortOrder)
 SELECT f.ModuleCode+'.'+f.FunctionCode+'.'+a.ActionCode,f.ModuleCode,f.FunctionCode,a.ActionCode,f.DisplayName+N' - '+a.DisplayName,f.SortOrder*10+a.SortOrder
 FROM @Functions f JOIN @Actions a ON a.FunctionType='ALL' OR a.FunctionType=f.FunctionType;
 INSERT dbo.AppRolePermission(RoleId,PermissionCode)
 SELECT r.RoleId,p.PermissionCode FROM dbo.AppRole r CROSS JOIN dbo.AppPermission p WHERE r.RoleCode='Administrator';
 INSERT dbo.AppRolePermission(RoleId,PermissionCode)
 SELECT r.RoleId,p.PermissionCode FROM dbo.AppRole r JOIN dbo.AppPermission p ON p.ActionCode='VIEW' OR p.ActionCode IN('CREATE','EDIT','DELETE_DRAFT','POST','PRINT','IMPORT','EXPORT')
 WHERE r.RoleCode='User' AND p.FunctionCode NOT LIKE 'ADMIN_%' AND (p.FunctionCode<>'UNIT' OR p.ActionCode='VIEW');
 INSERT dbo.SchemaVersion(Version) VALUES(6);
END;
COMMIT;
PRINT 'Schema version 6 ready (identity, RBAC, units and append-only audit).';
