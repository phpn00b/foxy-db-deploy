# foxy-db-deploy
Tool to easily manage SQL Server database changes

## Intro
If you love code first migrations or entity framework this tool is not for you. I am not going to say that entity framework doesn’t have its uses as it clearly does. I will however state that if you care about performance and call yourself a “Senior” * (Architect, Developer, Consultant, etc) and you are using entity framework you should have the skills and drive to use and deliver something better.  In the next few blog posts I will walk you through how I go about managing databases in my projects. I feel that it is clean orderly and logical how I go about doing so. I am open to critique if you feel that it is merited so please feel free to comment and if you feel necessary burn me at the stake for being a heretic. 

Keep in mind only very recently pulled this out of my much larger solution and decided to open source it. I fully understand that it doesn’t meet everyone’s needs currently. I am more than willing to work with anyone to make this a super useful tool for everyone.

## What it does do:
* Functions
* Procedures
* Types
* Database schema changes
* Manage data
* Upgrades ie moving project forward.
* Tries to be as efficient as possible.
* Plays nicely with others ( ie extensable) 
* Works well with multiple databases and build servers. 
* Supports SQL Server

## What it doesn’t do:
* Automatic downgrades of schema.
* Views as a first-class citizen though it can handle them
* Other things that might be important to other people. 
* Make coffee*
* Supports other SQL Database servers currently. 

## Coming Soon:
* Rewrite to make it cleaner and more extensible.  
* Abstract how it performs its operations to make it so that it can easily work with other databases
* Add Unit tests to verify that it works perfectly and verifiably. 


### How it works
Simple open source .net core app that looks at your file system and your database and tries to make your database match your file system. 


### Layout
* {DatabaseName}
  * dbDeploy
    * 1.0
      * 0001.some change.sql
    * 1.1
      * 0001.some change.sql
    * 1.2
      * 0001.some change.sql
  * Functions
    * dbo.fPerson_FetchFullName.sql
  * Procedures
    * dbo.pPerson_Add.sql
    * dbo.pPerson_Modify.sql
    * dbo.pPerson_Fetch.sql
    
  * Types
    * dbo.IntSortedList.sql
    
