# Service Hooks Security Utilities

Utilities for Visual Studio Team Services service hooks.

## Restore permissions for project administrators

This sample gives the Project Administrators group (and therefore its members) the ability to manage service hook subscriptions for the project.

### To use

1. From Visual Studio (2015 or later), open ServiceHooksSecurityUtils\ServiceHooksSecurityUtils.sln

2. Build the solution (which will pull in the required NuGet packages)

3. Run the command from the command line, specifying your account URL as the first argument. For example: :

   ```
     RestoreManagePermissionsToProjectAdminGroups.exe https://fabrikam-fiber-inc.visualstudio.com
   ```

4. Provide your credentials. Note: you must have the necessary permissions to run this utility. Typically members of the Project Collection Administrators group.
