# foxy-db-deploy
Tool to easily manage SQL Server database changes


## Layout
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
    
