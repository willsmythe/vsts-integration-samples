using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.Framework.Common;
using System.Collections.ObjectModel;

namespace ServiceHooksSecurityUtils
{
    public class RestoreManagePermissionsToProjectAdminGroups
    {
        private static readonly Guid ServiceHooksSecurityNamespaceId = new Guid("cb594ebe-87dd-4fc9-ac2c-6a10a4c92046");

        static void Main(string[] args)
        {
            const string usageMsg = "Usage: RestoreManagePermissionsToProjectAdminGroups [/collection:CollectionURL] or [/server:ServerURL]";

            Console.WriteLine("Utility to grant Service Hooks permissions to the project administrator group");
            Console.WriteLine("");

            if (args.Count() == 0)
            {
                Console.WriteLine(usageMsg);
                Environment.Exit(2);
            }

            if (args[0].ToUpperInvariant().StartsWith("/COLLECTION"))
            {
                Uri collectionUri = GetUri(args[0].Substring(12));

                TfsTeamProjectCollection connection = new TfsTeamProjectCollection(collectionUri);

                ProcessCollection(connection);
            }
            else if (args[0].ToUpperInvariant().StartsWith("/SERVER"))
            {
                Uri tfsUri = new Uri(args[0].Substring(8));

                TfsConfigurationServer tfsServer = TfsConfigurationServerFactory.GetConfigurationServer(tfsUri);

                ReadOnlyCollection<CatalogNode> collectionNodes = tfsServer.CatalogNode.QueryChildren(new[] { CatalogResourceTypes.ProjectCollection }, false, CatalogQueryOptions.None);
                foreach (CatalogNode collectionNode in collectionNodes)
                {
                    Guid collectionId = new Guid(collectionNode.Resource.Properties["InstanceId"]);
                    TfsTeamProjectCollection teamProjectCollection = tfsServer.GetTeamProjectCollection(collectionId);

                    ProcessCollection(teamProjectCollection);
                }

            }
            else
            {
                Console.WriteLine(usageMsg);
                Environment.Exit(2);
            }
        }

        protected static Uri GetUri(string url)
        {
            Uri uri = null;

            try
            {
                uri = new Uri(url);
            }
            catch (Exception)
            {
                Console.WriteLine("Invalid Url specified");
            }

            return uri;
        }

        protected static void ProcessCollection(TfsTeamProjectCollection collection)
        {
            Console.WriteLine(String.Format("Processing collection {0}", collection.DisplayName));
            Console.WriteLine(String.Empty);

            // Get Core, security, and identity services
            ISecurityService securityService = collection.GetService<ISecurityService>();
            SecurityNamespace hooksSecurity = securityService.GetSecurityNamespace(ServiceHooksSecurityNamespaceId);
            IIdentityManagementService2 identityService = collection.GetService<IIdentityManagementService2>();
            ProjectHttpClient projectClient = collection.GetClient<ProjectHttpClient>();

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
