﻿using System;
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

namespace DG.Tools.XrmMockup {
    internal class RetrieveExchangeRateRequestHandler : RequestHandler {
        internal RetrieveExchangeRateRequestHandler(Core core, IXrmDb db, MetadataSkeleton metadata, Security security) : base(core, db, metadata, security, "RetrieveExchangeRate") { }

        internal override OrganizationResponse Execute(OrganizationRequest orgRequest, EntityReference userRef) {
            var request = MakeRequest<RetrieveExchangeRateRequest>(orgRequest);

            var row = db.GetEntityOrNull(new EntityReference("transactioncurrency", request.TransactionCurrencyId));

            var resp = new RetrieveExchangeRateResponse();
            resp.Results["ExchangeRate"] = row?.GetAttributeValue<decimal>("exchangerate") ?? throw new FaultException($"transactioncurrency With Id = {request.TransactionCurrencyId} Does Not Exist");
            return resp;
        }
    }
}