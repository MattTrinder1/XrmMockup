﻿using Microsoft.Xrm.Sdk;
using Microsoft.Crm.Sdk.Messages;
using DG.Tools.XrmMockup.Database;
using System;
using System.Linq;

namespace DG.Tools.XrmMockup
{
    internal class WhoAmIRequestHandler : RequestHandler {
        internal WhoAmIRequestHandler(Core core, IXrmDb db, MetadataSkeleton metadata, Security security) : base(core, db, metadata, security, "WhoAmI") {}

        internal override OrganizationResponse Execute(OrganizationRequest orgRequest, EntityReference userRef) {
            var request = MakeRequest<WhoAmIRequest>(orgRequest);
            var ret = new WhoAmIResponse();
            var user = core.GetEntity(new EntityReference("systemuser", userRef.Id == default(Guid) ? core.AdminUserRef.Id : userRef.Id));
            ret.Results.Add("UserId", user.Id);
            ret.Results.Add("BusinessUnitId", user.GetAttributeValue<EntityReference>("businessunitid").Id);
            ret.Results.Add("OrganizationId", default(Guid));
            return ret;
        }
    }
}
