using Microsoft.Xrm.Sdk;
using Microsoft.Crm.Sdk.Messages;
using DG.Tools.XrmMockup.Database;
using System;
using System.Linq;

namespace DG.Tools.XrmMockup
{
    internal class PublishXmlRequestHandler : RequestHandler {
        internal PublishXmlRequestHandler(Core core, XrmDb db, MetadataSkeleton metadata, Security security) : base(core, db, metadata, security, "PublishXml") {}

        internal override OrganizationResponse Execute(OrganizationRequest orgRequest, EntityReference userRef) {
            var request = MakeRequest<PublishXmlRequest>(orgRequest);
            
            var ret = new PublishXmlResponse();
            ret.Results = new ParameterCollection();
            return ret;
        }
    }
}
