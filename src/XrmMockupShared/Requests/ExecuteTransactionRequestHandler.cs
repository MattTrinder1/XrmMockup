using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using System.ServiceModel;
using Microsoft.Xrm.Sdk.Metadata;
using DG.Tools.XrmMockup.Database;

namespace DG.Tools.XrmMockup
{
    internal class ExecuteTransactionRequestHandler : RequestHandler
    {
        internal ExecuteTransactionRequestHandler(Core core, XrmDb db, MetadataSkeleton metadata, Security security) : base(core, db, metadata, security, "ExecuteTransaction") { }

        internal override OrganizationResponse Execute(OrganizationRequest orgRequest, EntityReference userRef)
        {

            //as XrmMockup is not transactional we will just pass this through to ExecuteMultiple

            var req = new ExecuteMultipleRequestHandler(core, db, metadata, security);

            var newReq = new ExecuteMultipleRequest();
            newReq.Requests = new OrganizationRequestCollection();
            newReq.Requests.AddRange((orgRequest as ExecuteTransactionRequest).Requests);
            newReq.Parameters = new ParameterCollection();
            newReq.Parameters.AddRange((orgRequest as ExecuteTransactionRequest).Parameters);
            newReq.Settings = new ExecuteMultipleSettings() { ReturnResponses = true };

            var resp = (ExecuteMultipleResponse)req.Execute(newReq, userRef);

            var toReturn = new ExecuteTransactionResponse();
            // toReturn.Responses = new OrganizationResponseCollection;
            foreach (var r in resp.Results)
            {
                toReturn.Results.Add(r);
            }

            return toReturn;

        }
    }
}
