﻿using System;
using DG.Some.Namespace;
using Microsoft.Xrm.Sdk;
using DG.Tools.XrmMockup;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Microsoft.Crm.Sdk.Messages;
using System.Diagnostics;
using Microsoft.Xrm.Sdk.Query;
using System.Linq;

namespace DG.XrmMockupTest
{
    [TestClass]
    public class UnitTestBase
    {
        private static DateTime _startTime { get; set; }

        protected IOrganizationService orgAdminUIService;
        protected IOrganizationService orgAdminService;
        protected IOrganizationService orgGodService;
        protected IOrganizationService orgRealDataService;

        protected Entity testUser1;
        protected IOrganizationService testUser1Service;

        protected Entity testUser2;
        protected IOrganizationService testUser2Service;

        protected Entity testUser3;
        protected IOrganizationService testUser3Service;

        protected Entity contactWriteAccessTeamTemplate;

        protected IOrganizationService salesUserService;
        

#if XRM_MOCKUP_TEST_2011
        static protected XrmMockup2011 crm;
        static protected XrmMockup2011 crmRealData;
#elif XRM_MOCKUP_TEST_2013
        static protected XrmMockup2013 crm;
        static protected XrmMockup2013 crmRealData;
#elif XRM_MOCKUP_TEST_2015
        static protected XrmMockup2015 crm;
        static protected XrmMockup2015 crmRealData;
#elif XRM_MOCKUP_TEST_2016
        static protected XrmMockup2016 crm;
        static XrmMockup2016 crmRealData;
#elif XRM_MOCKUP_TEST_365
        static protected XrmMockup365 crm;
        static protected XrmMockup365 crmRealData;
#endif

        public UnitTestBase()
        {
            orgAdminUIService = crm.GetAdminService(new MockupServiceSettings(true, false, MockupServiceSettings.Role.UI));
            orgGodService = crm.GetAdminService(new MockupServiceSettings(false, true, MockupServiceSettings.Role.SDK));
            orgAdminService = crm.GetAdminService();
            if (crmRealData != null)
                orgRealDataService = crmRealData.GetAdminService();

            //create an admin user for our impersonating user plugin to run as
            var adminId = Guid.Parse("84a23551-017a-44fa-9cc1-08ee14bb97e8");
            var admin = new Entity("systemuser");
            admin.Id = adminId;
            admin["internalemailaddress"] = "camstestuser1@official.mod.uk";
            admin["businessunitid"] = crm.RootBusinessUnit;
            admin["islicensed"] = true;

            // crm.CreateUser

            var adminRole = crm.GetSecurityRole("System Administrator");
            var adminUser = crm.CreateUser(orgAdminService, admin, new Guid[] { adminRole.RoleId });

            InitialiseAccessTeamConfiguration();
        }

        

        private void InitialiseAccessTeamConfiguration()
        {
            var sw = new Stopwatch();
            sw.Start();

            //create a new security role with basic level only on all contact privileges
            var accessTeamTestRole = crm.CloneSecurityRole("Salesperson");
            accessTeamTestRole.Name = "AccessTeamTest";
            accessTeamTestRole.RoleId = Guid.Parse("c8357b35-8a17-4601-997a-5d011960ba2b");
            var contactPriv = accessTeamTestRole.Privileges["contact"];
            var newPriv = new Dictionary<AccessRights, DG.Tools.XrmMockup.RolePrivilege>();
            foreach (var priv in contactPriv)
            {
                var newP = priv.Value.Clone();
                newP.PrivilegeDepth = PrivilegeDepth.Basic;
                newPriv.Add(priv.Key, newP);
            }
            accessTeamTestRole.Privileges.Remove("contact");
            accessTeamTestRole.Privileges.Add("contact", newPriv);

            var accountPriv = accessTeamTestRole.Privileges["account"];
            newPriv = new Dictionary<AccessRights, DG.Tools.XrmMockup.RolePrivilege>();
            foreach (var priv in accountPriv)
            {
                var newP = priv.Value.Clone();
                newP.PrivilegeDepth = PrivilegeDepth.Basic;
                newPriv.Add(priv.Key, newP);
            }
            accessTeamTestRole.Privileges.Remove("account");
            accessTeamTestRole.Privileges.Add("account", newPriv);
            crm.AddSecurityRole(accessTeamTestRole);

            //create some users with the new role
            var user = new Entity("systemuser");
            user["internalemailaddress"] = "camstestuser1@official.mod.uk";
            user["businessunitid"] = crm.RootBusinessUnit;
            user["islicensed"] = true;
            user.Id = Guid.Parse("d8357b35-8a17-4601-997a-5d011960ba2b");
            testUser1 = crm.CreateUser(orgAdminService, user, new Guid[] { crm.GetSecurityRole("AccessTeamTest").RoleId });
            testUser1Service = crm.CreateOrganizationService(testUser1.Id);

            var user2 = new Entity("systemuser");
            user2["internalemailaddress"] = "camstestuser2@official.mod.uk";
            user2["businessunitid"] = crm.RootBusinessUnit;
            user2["islicensed"] = true;
            user2.Id = Guid.Parse("e8357b35-8a17-4601-997a-5d011960ba2b");
            testUser2 = crm.CreateUser(orgAdminService, user2, new Guid[] { crm.GetSecurityRole("AccessTeamTest").RoleId });
            testUser2Service = crm.CreateOrganizationService(testUser2.Id);

            var user3 = new Entity("systemuser");
            user3["internalemailaddress"] = "camstestuser3@official.mod.uk";
            user3["businessunitid"] = crm.RootBusinessUnit;
            user3["islicensed"] = true;
            user3.Id = Guid.Parse("f8357b35-8a17-4601-997a-5d011960ba2b");
            testUser3 = crm.CreateUser(orgAdminService, user3, new Guid[] { crm.GetSecurityRole("AccessTeamTest").RoleId });
            testUser3Service = crm.CreateOrganizationService(testUser3.Id);

            var salesUser = new Entity("systemuser");
            salesUser["internalemailaddress"] = "camstestuser4@official.mod.uk";
            salesUser["businessunitid"] = crm.RootBusinessUnit;
            salesUser["islicensed"] = true;
            salesUser.Id = Guid.Parse("09357b35-8a17-4601-997a-5d011960ba2b");
            crm.CreateUser(orgAdminService, salesUser, new Guid[] { SecurityRoles.Salesperson });
            salesUserService = crm.CreateOrganizationService(salesUser.Id);

            //create some access team templates
            var q = new QueryExpression("teamtemplate");
            q.ColumnSet = new ColumnSet("teamtemplatename");
            var existing = orgAdminService.RetrieveMultiple(q);

            

            if (!existing.Entities.Any(x => x.GetAttributeValue<string>("teamtemplatename") == "TestWriteContact"))
                CreateAccessTeamTemplate("TestWriteContact", 2, AccessRights.WriteAccess);
            if (!existing.Entities.Any(x => x.GetAttributeValue<string>("teamtemplatename") == "TestReadContact"))
                CreateAccessTeamTemplate("TestReadContact", 2, AccessRights.ReadAccess);
            if (!existing.Entities.Any(x => x.GetAttributeValue<string>("teamtemplatename") == "TestDeleteContact"))
                CreateAccessTeamTemplate("TestDeleteContact", 2, AccessRights.DeleteAccess);
            if (!existing.Entities.Any(x => x.GetAttributeValue<string>("teamtemplatename") == "TestAppendContact"))
                CreateAccessTeamTemplate("TestAppendContact", 2, AccessRights.AppendAccess);
            if (!existing.Entities.Any(x => x.GetAttributeValue<string>("teamtemplatename") == "TestAssignContact"))
                CreateAccessTeamTemplate("TestAssignContact", 2, AccessRights.AssignAccess);
            if (!existing.Entities.Any(x => x.GetAttributeValue<string>("teamtemplatename") == "TestShareContact"))
                CreateAccessTeamTemplate("TestShareContact", 2, AccessRights.ShareAccess);
            if (!existing.Entities.Any(x => x.GetAttributeValue<string>("teamtemplatename") == "TestAppendToAccount"))
                CreateAccessTeamTemplate("TestAppendToAccount", 1, AccessRights.AppendToAccess);
            if (!existing.Entities.Any(x => x.GetAttributeValue<string>("teamtemplatename") == "TestMultipleContact"))
                CreateAccessTeamTemplate("TestMultipleContact", 2, AccessRights.WriteAccess, AccessRights.ReadAccess, AccessRights.DeleteAccess);

            Console.WriteLine(sw.ElapsedMilliseconds.ToString());
            sw.Stop();
        }

        private void CreateAccessTeamTemplate(string name,int objectTypeCode,params AccessRights[] access)
        {


            var contactWriteAccessTeamTemplate = new Entity("teamtemplate");
            contactWriteAccessTeamTemplate["teamtemplatename"] = name;
            contactWriteAccessTeamTemplate["objecttypecode"] = objectTypeCode;
            int mask = 0;
            //"OR" the access rights together to get the mask
            foreach (var a in access)
            {
                mask |= (int)a;
            }
            contactWriteAccessTeamTemplate["defaultaccessrightsmask"] = mask;
            contactWriteAccessTeamTemplate.Id = orgAdminService.Create(contactWriteAccessTeamTemplate);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            crm.ResetEnvironment();
        }


        [AssemblyInitialize]
        public static void InitializeServices(TestContext context)
        {
            InitializeMockup(context);
        }

        public static void InitializeMockup(TestContext context)
        {
            var additionalPluginMetadata = new MetaPlugin();
            additionalPluginMetadata.AssemblyName = "DG.Some.Namespace.ContactIPluginDirectPreOp";
            additionalPluginMetadata.MessageName = "Create";
            additionalPluginMetadata.PrimaryEntity = "contact";
            additionalPluginMetadata.Rank = 10;
            additionalPluginMetadata.Stage = 20; //pre op as it only updates a field name

            var additionalPluginMetadata2 = new MetaPlugin();
            additionalPluginMetadata2.AssemblyName = "DG.Some.Namespace.ContactIPluginDirectPostOp";
            additionalPluginMetadata2.MessageName = "Create";
            additionalPluginMetadata2.PrimaryEntity = "contact";
            additionalPluginMetadata2.Rank = 10;
            additionalPluginMetadata2.Stage = 40; //postOp

            var settings = new XrmMockupSettings
            {
                BasePluginTypes = new Type[] { typeof(Plugin), typeof(PluginNonDaxif), typeof(ContactIPluginDirectPreOp) },
                CodeActivityInstanceTypes = new Type[] { typeof(AccountWorkflowActivity) },
                EnableProxyTypes = true,
                IncludeAllWorkflows = false,
                ExceptionFreeRequests = new string[] { "TestWrongRequest" },
                MetadataDirectoryPath = "../../../Metadata",
                IPluginMetadata = new MetaPlugin[] { additionalPluginMetadata, additionalPluginMetadata2 }
                //MetadataDirectoryPath = @"C:\dev\MOD\CAMS\Plugins\XrmMockupTests\Metadata"
                , DatabaseConnectionString = "Server=.;Database=XrmMockup;Trusted_Connection=True;"
                , RecreateDatabase = false
                //,RetainTables = new string[] { "fax" ,"goal","lead"}
              //  ,AppendAndAppendToPrivilegeCheck = false
            };

#if XRM_MOCKUP_TEST_2011
            crm = XrmMockup2011.GetInstance(settings);
#elif XRM_MOCKUP_TEST_2013
            crm = XrmMockup2013.GetInstance(settings);
#elif XRM_MOCKUP_TEST_2015
            crm = XrmMockup2015.GetInstance(settings);
#elif XRM_MOCKUP_TEST_2016
            crm = XrmMockup2016.GetInstance(settings);
#elif XRM_MOCKUP_TEST_365
            crm = XrmMockup365.GetInstance(settings);
#endif

            try
            {
                var realDataSettings = new XrmMockupSettings
                {
                    BasePluginTypes = settings.BasePluginTypes,
                    CodeActivityInstanceTypes = settings.CodeActivityInstanceTypes,
                    EnableProxyTypes = settings.EnableProxyTypes,
                    IncludeAllWorkflows = settings.IncludeAllWorkflows,
                    ExceptionFreeRequests = settings.ExceptionFreeRequests,
                    OnlineEnvironment = new Env
                    {
                        providerType = AuthenticationProviderType.OnlineFederation,
                        uri = "https://exampleURL/XRMServices/2011/Organization.svc",
                        username = "exampleUser",
                        password = "examplePass"
                    }
                };
#if XRM_MOCKUP_TEST_2011
                            crmRealData = XrmMockup2011.GetInstance(realDataSettings);
#elif XRM_MOCKUP_TEST_2013
                            crmRealData = XrmMockup2013.GetInstance(realDataSettings);
#elif XRM_MOCKUP_TEST_2015
                            crmRealData = XrmMockup2015.GetInstance(realDataSettings);
#elif XRM_MOCKUP_TEST_2016
                            crmRealData = XrmMockup2016.GetInstance(realDataSettings);
#elif XRM_MOCKUP_TEST_365
                crmRealData = XrmMockup365.GetInstance(realDataSettings);
#endif
            }
            catch
            {
                // ignore
            }
        }
    }
}