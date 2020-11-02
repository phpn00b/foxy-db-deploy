We will find all files with the extension of .sql here and try to apply them as a stored procedure. 
to do so it expects the first line to have create procedure. This is very imporant. What it will do is when necessary change the create to an alter.

It is extreamly important that you file names exactly match your stored procedure name. if you use [] brackets the file name should not include them. 
ie [dbo].[pLogEntry_Create] would be stored in a file called dbo.pLogEntry_Create.sql


even if you only use the schema dbo please make sure your file names all have schema.procedurename.sql format. 