using System;
using Microsoft.Xrm.Sdk;
using DG.Tools.XrmMockup;
using Xunit;
using Microsoft.Crm.Sdk.Messages;
using System.Collections.Generic;
using static DG.Tools.XrmMockup.MetadataSkeleton;

namespace DG.XrmMockupTest
{
    [Collection("Xrm Collection")]
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

#if XRM_MOCKUP_TEST_2011
        static protected XrmMockup2011 crm;
#elif XRM_MOCKUP_TEST_2013
        static protected XrmMockup2013 crm;
#elif XRM_MOCKUP_TEST_2015
        static protected XrmMockup2015 crm;
#elif XRM_MOCKUP_TEST_2016
        static protected XrmMockup2016 crm;
#elif XRM_MOCKUP_TEST_365
        static protected XrmMockup365 crm;
#endif

        public UnitTestBase(XrmMockupFixture fixture)
        {
            crm = fixture.crm;
            crm.ResetEnvironment();
            orgAdminUIService = crm.GetAdminService(new MockupServiceSettings(true, false, MockupServiceSettings.Role.UI));
            orgGodService = crm.GetAdminService(new MockupServiceSettings(false, true, MockupServiceSettings.Role.SDK));
            orgAdminService = crm.GetAdminService();
            if (fixture.crmRealData != null)
                orgRealDataService = fixture.crmRealData.GetAdminService();

            //create an admin user to run our impersonating user plugins as
            var admin = new Entity("systemuser") { Id = Guid.Parse("3b961284-cd7a-4fa3-af7e-89802e88dd5c") };
            admin["businessunitid"] = crm.RootBusinessUnit;
            admin["internalemailaddress"] = "camstestuser1@official.mod.uk";
            admin["islicensed"] = true;

            //create an admin user for our impersonating user plugin to run as
            //var adminId = Guid.Parse("84a23551-017a-44fa-9cc1-08ee14bb97e8");
            //var admin = new Entity("systemuser");
            //admin.Id = adminId;
            //admin["internalemailaddress"] = "camstestuser1@official.mod.uk";
            //admin["businessunitid"] = crm.RootBusinessUnit;
            //admin["islicensed"] = true;

            // crm.CreateUser

            var adminRole = crm.GetSecurityRole("System Administrator");
            var adminUser = crm.CreateUser(orgAdminService, admin, new Guid[] { adminRole.RoleId });

            InitialiseAccessTeamConfiguration();
        }

        private void InitialiseAccessTeamConfiguration()
        {
            //create a new security role with basic level only on all contact privileges
            var accessTeamTestRole = crm.CloneSecurityRole("Salesperson");
            accessTeamTestRole.Name = "AccessTeamTest";
            var contactPriv = accessTeamTestRole.Privileges["contact"];
            var newPriv = new Dictionary<AccessRights, Tools.XrmMockup.RolePrivilege>();
            foreach (var priv in contactPriv)
            {
                var newP = priv.Value.Clone();
                newP.PrivilegeDepth = PrivilegeDepth.Basic;
                newPriv.Add(priv.Key, newP);
            }
            accessTeamTestRole.Privileges.Remove("contact");
            accessTeamTestRole.Privileges.Add("contact", newPriv);

            var accountPriv = accessTeamTestRole.Privileges["account"];
            newPriv = new Dictionary<AccessRights, Tools.XrmMockup.RolePrivilege>();
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
            testUser1 = crm.CreateUser(orgAdminService, user, new Guid[] { crm.GetSecurityRole("AccessTeamTest").RoleId });
            testUser1Service = crm.CreateOrganizationService(testUser1.Id);

            var user2 = new Entity("systemuser");
            user2["internalemailaddress"] = "camstestuser2@official.mod.uk";
            user2["businessunitid"] = crm.RootBusinessUnit;
            user2["islicensed"] = true;
            testUser2 = crm.CreateUser(orgAdminService, user2, new Guid[] { crm.GetSecurityRole("AccessTeamTest").RoleId });
            testUser2Service = crm.CreateOrganizationService(testUser2.Id);

            var user3 = new Entity("systemuser");
            user3["internalemailaddress"] = "camstestuser3@official.mod.uk";
            user3["businessunitid"] = crm.RootBusinessUnit;
            user3["islicensed"] = true;
            testUser3 = crm.CreateUser(orgAdminService, user3, new Guid[] { crm.GetSecurityRole("AccessTeamTest").RoleId });
            testUser3Service = crm.CreateOrganizationService(testUser3.Id);

            //create some access team templates
            CreateAccessTeamTemplate("TestWriteContact", 2, AccessRights.WriteAccess);
            CreateAccessTeamTemplate("TestReadContact", 2, AccessRights.ReadAccess);
            CreateAccessTeamTemplate("TestDeleteContact", 2, AccessRights.DeleteAccess);
            CreateAccessTeamTemplate("TestAppendContact", 2, AccessRights.AppendAccess);
            CreateAccessTeamTemplate("TestAssignContact", 2, AccessRights.AssignAccess);
            CreateAccessTeamTemplate("TestShareContact", 2, AccessRights.ShareAccess);
            CreateAccessTeamTemplate("TestAppendToAccount", 1, AccessRights.AppendToAccess);
            CreateAccessTeamTemplate("TestMultipleContact", 2, AccessRights.WriteAccess, AccessRights.ReadAccess, AccessRights.DeleteAccess);
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
        

        public void Dispose()
        {
            crm.ResetEnvironment();
        }
    }
}