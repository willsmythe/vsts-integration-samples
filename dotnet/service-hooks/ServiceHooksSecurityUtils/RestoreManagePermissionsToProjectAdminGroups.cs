using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.Framework.Common;

namespace ServiceHooksSecurityUtils
{
    public class RestoreManagePermissionsToProjectAdminGroups
    {
        private static readonly Guid ServiceHooksSecurityNamespaceId = new Guid("cb594ebe-87dd-4fc9-ac2c-6a10a4c92046");

        static void Main(string[] args)
        {
            Console.WriteLine("Utility to grant Service Hooks permissions to the project administrator group");
            Console.WriteLine("");

            Uri collectionUri = null;
            try
            {
                collectionUri = new Uri(args[0]);
            }
            catch (Exception)
            {
                Console.WriteLine("Usage: RestoreManagePermissionsToProjectAdminGroups [TeamProjectCollectionUrl]");
            }

            if (collectionUri != null)
            {
                TfsTeamProjectCollection connection = new TfsTeamProjectCollection(collectionUri);

                // Get Core, security, and identity services
                ISecurityService securityService = connection.GetService<ISecurityService>();
                SecurityNamespace hooksSecurity = securityService.GetSecurityNamespace(ServiceHooksSecurityNamespaceId);
                IIdentityManagementService2 identityService = connection.GetService<IIdentityManagementService2>();
                ProjectHttpClient projectClient = connection.GetClient<ProjectHttpClient>();

                IEnumerable<TeamProjectReference> projects = projectClient.GetProjects(stateFilter: Microsoft.TeamFoundation.Common.ProjectState.WellFormed).Result;

                // Iterate over each project, check SH permissions, and grant if needed
                foreach (var project in projects)
                {
                    Console.WriteLine(String.Format("Project {0} ({1})", project.Name, project.Id));

                    var groups = identityService.ListApplicationGroups(project.Id.ToString(), ReadIdentityOptions.None, null, Microsoft.TeamFoundation.Framework.Common.IdentityPropertyScope.Both);

                    String adminGroupName = String.Format("vstfs:///Classification/TeamProject/{0}\\Project Administrators", project.Id);

                    try
                    {
                        TeamFoundationIdentity adminGroup = groups.First(g => String.Equals(g.UniqueName, adminGroupName, StringComparison.InvariantCultureIgnoreCase));

                        Console.WriteLine(" - Checking Project Administrators group permissions");

                        AccessControlEntry ace = new AccessControlEntry(adminGroup.Descriptor, 7, 0); // 7 = view, create, delete
                        String securityToken = "PublisherSecurity/" + project.Id;

                        bool hasPermission = hooksSecurity.HasPermission(securityToken, adminGroup.Descriptor, 7, false);

                        if (!hasPermission)
                        {
                            Console.WriteLine(" - Missing. Granting...");

                            hooksSecurity.SetAccessControlEntry(securityToken, ace, true);

                            // check permission again after granting
                            hasPermission = hooksSecurity.HasPermission(securityToken, adminGroup.Descriptor, 7, false);
                            if (hasPermission)
                            {
                                Console.WriteLine(" - Granted");
                            }
                            else
                            {
                                Console.WriteLine(" - Still does not have permission. Check to make sure it has not been explicitly denied.");
                            }
                         
                        }
                        else
                        {
                            Console.WriteLine(" - Already has permission");
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(String.Format("Admin group: Not found! ({0})", ex.Message));
                    }
                    
                    Console.WriteLine("");
                }
            }
        }
    }
}
