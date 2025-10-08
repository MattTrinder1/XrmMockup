using Microsoft.Xrm.Sdk;
using Microsoft.Crm.Sdk.Messages;
using DG.Tools.XrmMockup.Database;
using System;
using System.Linq;
using Microsoft.Xrm.Sdk.Query;
using System.Web.Configuration;
using Microsoft.Xrm.Sdk.Messages;
using System.Windows.Documents;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DG.Tools.XrmMockup
{
    internal class SetProcessRequestHandler : RequestHandler {
        internal SetProcessRequestHandler(Core core, XrmDb db, MetadataSkeleton metadata, Security security) : base(core, db, metadata, security, "SetProcess") {}

        internal override OrganizationResponse Execute(OrganizationRequest orgRequest, EntityReference userRef) {
            var request = MakeRequest<SetProcessRequest>(orgRequest);
            var resp = new SetProcessResponse();


            return resp;
        }
    }
}
