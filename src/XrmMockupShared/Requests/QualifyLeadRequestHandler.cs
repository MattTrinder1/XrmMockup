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

namespace DG.Tools.XrmMockup
{
    internal class QualifyLeadRequestHandler : RequestHandler {
        internal QualifyLeadRequestHandler(Core core, XrmDb db, MetadataSkeleton metadata, Security security) : base(core, db, metadata, security, "QualifyLead") {}

        internal override OrganizationResponse Execute(OrganizationRequest orgRequest, EntityReference userRef) {
            var request = MakeRequest<QualifyLeadRequest>(orgRequest);

            var lead = db.GetDbRow(request.LeadId);
            var entity = core.GetStronglyTypedEntity(lead.ToEntity(), lead.Metadata, new ColumnSet(true));

            var resp = new QualifyLeadResponse();
            resp.Results = new ParameterCollection();
            var created = new EntityReferenceCollection();
           // resp.CreatedEntities = new EntityReferenceCollection();


            var account = new Entity("account", Guid.NewGuid());


            if (request.CreateAccount)
            {

                account["name"] = entity.GetAttributeValue<string>("companyname");

                var req = new CreateRequest();
                req.Target = account;
                core.Execute(req, userRef);

                created.Add(account.ToEntityReference());

            }

            var contact = new Entity("contact", Guid.NewGuid());
            if (request.CreateContact)
            {
                contact["firstname"] = entity.GetAttributeValue<string>("firstname");
                contact["lastname"] = entity.GetAttributeValue<string>("lastname");
                contact["emailaddress1"] = entity.GetAttributeValue<string>("emailaddress1");

                var req = new CreateRequest();
                req.Target = contact;
                core.Execute(req, userRef);

                created.Add(contact.ToEntityReference());

            }

            if (request.CreateOpportunity)
            {
                var opp = new Entity("opportunity",Guid.NewGuid());
                opp["accountid"] = account.ToEntityReference();
                opp["contactid"] = contact.ToEntityReference();
                opp["originatingleadid"] = entity.ToEntityReference();
                opp["ownerid"] = entity.GetAttributeValue<EntityReference>("ownerid");

                var req = new CreateRequest();
                req.Target = opp;
                core.Execute(req, userRef);

                created.Add(opp.ToEntityReference());

            }

            resp.Results["CreatedEntities"] = created;

            return resp;
        }
    }
}
