﻿using System;
using DG.Some.Namespace;
using Microsoft.Xrm.Sdk;
using DG.Tools.XrmMockup;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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



        protected Entity contactWriteAccessTeamTemplate;

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

            var user = new Entity("systemuser");
            user["internalemailaddress"] = "camstestuser1@official.mod.uk";
            user["businessunitid"] = crm.RootBusinessUnit;
            user["islicensed"] = true;
            testUser1 = crm.CreateUser(orgAdminService, user, new Guid[] { crm.GetSecurityRole("Test").RoleId });
            testUser1Service = crm.CreateOrganizationService(testUser1.Id);

            var user2 = new Entity("systemuser");
            user2["internalemailaddress"] = "camstestuser2@official.mod.uk";
            user2["businessunitid"] = crm.RootBusinessUnit;
            user2["islicensed"] = true;
            testUser2 = crm.CreateUser(orgAdminService, user2, new Guid[] { crm.GetSecurityRole("Test").RoleId });
            testUser2Service = crm.CreateOrganizationService(testUser2.Id);

            contactWriteAccessTeamTemplate = new Entity("teamtemplate");
            contactWriteAccessTeamTemplate["objecttypecode"] = 2;
            contactWriteAccessTeamTemplate["defaultaccessrightsmask"] = 22;
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
            var settings = new XrmMockupSettings
            {
                BasePluginTypes = new Type[] { typeof(Plugin)},//, typeof(PluginNonDaxif) },
                CodeActivityInstanceTypes = new Type[] { typeof(AccountWorkflowActivity) },
                EnableProxyTypes = true,
                IncludeAllWorkflows = false,
                ExceptionFreeRequests = new string[] { "TestWrongRequest" },
                //MetadataDirectoryPath = "../../../Metadata"
                MetadataDirectoryPath = @"C:\dev\MOD\CAMS\Plugins\XrmMockupTests\Metadata"

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

//            try
//            {
//                var realDataSettings = new XrmMockupSettings
//                {
//                    BasePluginTypes = settings.BasePluginTypes,
//                    CodeActivityInstanceTypes = settings.CodeActivityInstanceTypes,
//                    EnableProxyTypes = settings.EnableProxyTypes,
//                    IncludeAllWorkflows = settings.IncludeAllWorkflows,
//                    ExceptionFreeRequests = settings.ExceptionFreeRequests,
//                    OnlineEnvironment = new Env
//                    {
//                        providerType = AuthenticationProviderType.OnlineFederation,
//                        uri = "https://exampleURL/XRMServices/2011/Organization.svc",
//                        username = "exampleUser",
//                        password = "examplePass"
//                    }
//                };
//#if XRM_MOCKUP_TEST_2011
//                crmRealData = XrmMockup2011.GetInstance(realDataSettings);
//#elif XRM_MOCKUP_TEST_2013
//                crmRealData = XrmMockup2013.GetInstance(realDataSettings);
//#elif XRM_MOCKUP_TEST_2015
//                crmRealData = XrmMockup2015.GetInstance(realDataSettings);
//#elif XRM_MOCKUP_TEST_2016
//                crmRealData = XrmMockup2016.GetInstance(realDataSettings);
//#elif XRM_MOCKUP_TEST_365
//                crmRealData = XrmMockup365.GetInstance(realDataSettings);
//#endif
//            }
//            catch
//            {
//                // ignore
//            }
        }
    }
}