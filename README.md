# Azure DevOps Migration Tools [![Chocolatey](https://img.shields.io/chocolatey/dt/vsts-sync-migrator.svg)](https://chocolatey.org/packages/vsts-sync-migrator/) [![GitHub release](https://img.shields.io/github/release/nkdAgility/vsts-sync-migration.svg)](https://github.com/nkdAgility/vsts-sync-migrator/releases) ![Build on VSTS](https://nkdagility.visualstudio.com/_apis/public/build/definitions/1b52ce63-eccc-41c8-88f9-ae6ebeefdc63/94/badge) 

The Azure DevOps Migration Tools allow you to bulk edit and migrate data between Team Projects on both Microsoft Team Foundation Server (TFS) and Azure DevOps Services. You can find out [why](https://dev.azure.com/nkdagility/migration-tools/_wiki/wikis/docs?pagePath=%2Fwhy) this tooling exists and you can access the [documentation](https://dev.azure.com/nkdagility/migration-tools/_wiki/wikis/docs) to find out how. This project is published as [code on GitHub](https://github.com/nkdAgility/azure-devops-migration-tools/) as well as a [Azure DevOps Migration Tools on Chocolatey](https://chocolatey.org/packages/vsts-sync-migrator/).

**WARNING: This tool is not designed for a novice. This tool was developed to support the senarios below, and the edge cases that have been encountered by the 30+ contributers from around the Azure DevOps community. You should be comfortable with the TFS/Azure DevOps object model, as well as debugging code in Visual Studio.**

## What can you do with this tool?

- Migrate Work Items from one Team Project to another Team Project
- Merge many Team Projects into a single Team Project
- Split one Team Project into many Team Projects
- Assistance in changing Process Templates
- Bulk edit of Work Items
- Migration of Test Suits & Test Plans

## What versions of Azure DevOps & TFS do you support?

- Work Item Migration Supports all versions of TFS 2013+ and all versions of Azure DevOps
- Process Template migration only supports XML based Projects

**NOTE: If you are able to migrate your entire Collection to Azure DevOps Services you should use [Azure DevOps Migration Service](https://www.visualstudio.com/team-services/migrate-tfs-vsts/) from Microsoft. If you have a requirement to change Process Template then you will need to do that before you move to Azure DevOps Services.**

## Quick Links

 - [Video Overview](https://youtu.be/ZxDktQae10M)
 - [Getting Started](https://dev.azure.com/nkdagility/migration-tools/_wiki/wikis/docs?wikiVersion=GBmaster&pagePath=%2Fgetting%20started)
 - [Documentation](https://dev.azure.com/nkdagility/migration-tools/_wiki/wikis/docs?pagePath=%2Findex)
 - [Contributing](https://dev.azure.com/nkdagility/migration-tools/_wiki/wikis/docs?pagePath=%2Findex&anchor=contributing)
 - [Why VSTS Bulk Data Editor](https://dev.azure.com/nkdagility/migration-tools/_wiki/wikis/docs?pagePath=%2Fwhy)
 - [Usage](https://dev.azure.com/nkdagility/migration-tools/_wiki/wikis/docs?pagePath=%2Fusage%2Fusage)

### External Walkthroughs and Reviews

  - [TFS 2017 Migration To VSTS with VSTS Sync Migrator from Mohamed Radwan](http://mohamedradwan.com/2017/09/15/tfs-2017-migration-to-vsts-with-vsts-sync-migrator/)
  - [Options migrating TFS to VSTS from Richard Fennell](https://blogs.blackmarble.co.uk/blogs/rfennell/post/2017/05/10/Options-migrating-TFS-to-VSTS)

## Getting the Tools

There are two ways to use these tools:

- (recommended)[Install from Chocolatey](https://chocolatey.org/packages/vsts-sync-migrator/)
- Download the [latest release from GitHub](https://github.com/nkdAgility/azure-devops-migration-tools/releases) and unzip

## The Technical Details

|-| Technical Overview |
|-------------:|:-------------|
| Quality Gate | [![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=vsts-sync-migrator%3Amaster&metric=alert_status)](https://sonarcloud.io/dashboard?id=vsts-sync-migrator%3Amaster) |
| Azure Pipeline | ![Build on VSTS](https://nkdagility.visualstudio.com/_apis/public/build/definitions/1b52ce63-eccc-41c8-88f9-ae6ebeefdc63/94/badge) |
| Coverage | [![Coverage](https://sonarcloud.io/api/project_badges/measure?project=vsts-sync-migrator%3Amaster&metric=coverage)](https://sonarcloud.io/dashboard?id=vsts-sync-migrator%3Amaster) |
| Maintainability | [![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=vsts-sync-migrator%3Amaster&metric=sqale_rating)](https://sonarcloud.io/dashboard?id=vsts-sync-migrator%3Amaster) |
| Security Rating | [![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=vsts-sync-migrator%3Amaster&metric=security_rating)](https://sonarcloud.io/dashboard?id=vsts-sync-migrator%3Amaster) |
| Vulnerabilities | [![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=vsts-sync-migrator%3Amaster&metric=vulnerabilities)](https://sonarcloud.io/dashboard?id=vsts-sync-migrator%3Amaster) |
| Release |[![GitHub release](https://img.shields.io/github/release/nkdAgility/vsts-sync-migration.svg)](https://github.com/nkdAgility/vsts-sync-migrator/releases)|
| Chocolatey |[![Chocolatey](https://img.shields.io/chocolatey/v/vsts-sync-migrator.svg)](https://chocolatey.org/packages/vsts-sync-migrator/)|
