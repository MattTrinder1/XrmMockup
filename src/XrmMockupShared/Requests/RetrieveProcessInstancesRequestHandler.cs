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
    internal class RetrieveProcessInstancesRequestHandler : RequestHandler {
        internal RetrieveProcessInstancesRequestHandler(Core core, XrmDb db, MetadataSkeleton metadata, Security security) : base(core, db, metadata, security, "RetrieveProcessInstances") {}

        internal override OrganizationResponse Execute(OrganizationRequest orgRequest, EntityReference userRef) {
            var request = MakeRequest<RetrieveProcessInstancesRequest>(orgRequest);
            var resp = new RetrieveProcessInstancesResponse();

            var col = new EntityCollection();

            var e = new Entity("businessprocessflowinstance");
            e["name"] = "Lead to Opportunity Sales Process";
            e.Id = Guid.NewGuid();

            col.Entities.Add(e);

            resp.Results["Processes"] = col;


            return resp;
        }
    }
}
