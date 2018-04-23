
-- --------------------------------------------------
-- Entity Designer DDL Script for SQL Server 2005, 2008, 2012 and Azure
-- --------------------------------------------------
-- Date Created: 03/25/2018 15:11:15
-- Generated from EDMX file: BloggingContext.edmx
-- --------------------------------------------------

SET QUOTED_IDENTIFIER OFF;
GO
USE [Blogging];
GO
IF SCHEMA_ID(N'dbo') IS NULL EXECUTE(N'CREATE SCHEMA [dbo]');
GO

-- --------------------------------------------------
-- Dropping existing FOREIGN KEY constraints
-- --------------------------------------------------

IF OBJECT_ID(N'[dbo].[FK_Blog_User]', 'F') IS NOT NULL
    ALTER TABLE [dbo].[Blog] DROP CONSTRAINT [FK_Blog_User];
GO
IF OBJECT_ID(N'[dbo].[FK_BlogPost_Blog]', 'F') IS NOT NULL
    ALTER TABLE [dbo].[BlogPost] DROP CONSTRAINT [FK_BlogPost_Blog];
GO
IF OBJECT_ID(N'[dbo].[FK_BlogSettings_Blog]', 'F') IS NOT NULL
    ALTER TABLE [dbo].[BlogSettings] DROP CONSTRAINT [FK_BlogSettings_Blog];
GO

-- --------------------------------------------------
-- Dropping existing tables
-- --------------------------------------------------

IF OBJECT_ID(N'[dbo].[Blog]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Blog];
GO
IF OBJECT_ID(N'[dbo].[BlogPost]', 'U') IS NOT NULL
    DROP TABLE [dbo].[BlogPost];
GO
IF OBJECT_ID(N'[dbo].[BlogSettings]', 'U') IS NOT NULL
    DROP TABLE [dbo].[BlogSettings];
GO

IF OBJECT_ID(N'[dbo].[User]', 'U') IS NOT NULL
    DROP TABLE [dbo].[User];
GO

-- --------------------------------------------------
-- Creating all tables
-- --------------------------------------------------

-- Creating table 'Blog'
CREATE TABLE [dbo].[Blog] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserId] int  NOT NULL,
    [Name] varchar(50)  NOT NULL,
    [DateCreate] datetime  NOT NULL,
    [Description] varchar(200)  NOT NULL
);
GO

-- Creating table 'BlogPost'
CREATE TABLE [dbo].[BlogPost] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [BlogId] int  NOT NULL,
    [Body] varchar(max)  NOT NULL,
    [DatePublication] datetime  NOT NULL
);
GO

-- Creating table 'BlogSettings'
CREATE TABLE [dbo].[BlogSettings] (
    [BlogId] int  NOT NULL,
    [AutoSave] bit  NOT NULL,
    [AutoPost] bit  NOT NULL
);
GO

-- Creating table 'User'
CREATE TABLE [dbo].[User] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] varchar(50)  NOT NULL,
    [DateCreate] datetime  NOT NULL,
    [Gender] int  NOT NULL
);
GO

-- --------------------------------------------------
-- Creating all PRIMARY KEY constraints
-- --------------------------------------------------

-- Creating primary key on [Id] in table 'Blog'
ALTER TABLE [dbo].[Blog]
ADD CONSTRAINT [PK_Blog]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- Creating primary key on [Id] in table 'BlogPost'
ALTER TABLE [dbo].[BlogPost]
ADD CONSTRAINT [PK_BlogPost]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- Creating primary key on [BlogId] in table 'BlogSettings'
ALTER TABLE [dbo].[BlogSettings]
ADD CONSTRAINT [PK_BlogSettings]
    PRIMARY KEY CLUSTERED ([BlogId] ASC);
GO

-- Creating primary key on [Id] in table 'User'
ALTER TABLE [dbo].[User]
ADD CONSTRAINT [PK_User]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- --------------------------------------------------
-- Creating all FOREIGN KEY constraints
-- --------------------------------------------------

-- Creating foreign key on [UserId] in table 'Blog'
ALTER TABLE [dbo].[Blog]
ADD CONSTRAINT [FK_Blog_User]
    FOREIGN KEY ([UserId])
    REFERENCES [dbo].[User]
        ([Id])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_Blog_User'
CREATE INDEX [IX_FK_Blog_User]
ON [dbo].[Blog]
    ([UserId]);
GO

-- Creating foreign key on [BlogId] in table 'BlogPost'
ALTER TABLE [dbo].[BlogPost]
ADD CONSTRAINT [FK_BlogPost_Blog]
    FOREIGN KEY ([BlogId])
    REFERENCES [dbo].[Blog]
        ([Id])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_BlogPost_Blog'
CREATE INDEX [IX_FK_BlogPost_Blog]
ON [dbo].[BlogPost]
    ([BlogId]);
GO

-- Creating foreign key on [BlogId] in table 'BlogSettings'
ALTER TABLE [dbo].[BlogSettings]
ADD CONSTRAINT [FK_BlogSettings_Blog]
    FOREIGN KEY ([BlogId])
    REFERENCES [dbo].[Blog]
        ([Id])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- --------------------------------------------------
-- Script has ended
-- --------------------------------------------------