:On Error exit
USE master;
GO
IF DB_ID(N'ReportingModuleDemo') IS NULL
BEGIN
 EXEC(N'CREATE DATABASE ReportingModuleDemo');
 EXEC ReportingModuleDemo.sys.sp_addextendedproperty @name=N'ReportingModulePurpose',@value=N'Synthetic internship demo V3';
END;
GO
USE ReportingModuleDemo;
GO
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'ReportingModulePurpose' AND CONVERT(nvarchar(200),value)=N'Synthetic internship demo V3')
 THROW 51004,'Existing database is not marked as this project demo. No schema changes allowed.',1;
GO
:r database/001-schema.sql
GO
:r database/002-seed.sql
GO
:r database/003-inventory.sql
GO
:r database/004-commerce.sql
GO
:r database/005-debt.sql
GO
:r database/006-ledger.sql
GO
:r database/007-security.sql
GO
:r database/008-purchase-workflow.sql
GO
