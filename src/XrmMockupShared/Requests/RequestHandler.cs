﻿using DG.Tools.XrmMockup.Database;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Collections.Generic;
using System.Text;

namespace DG.Tools.XrmMockup {
    internal abstract class RequestHandler {
        protected Security security;
        protected Core core;
        protected IXrmDb db;
        protected MetadataSkeleton metadata;
        internal string RequestName;

        internal RequestHandler(Core core, IXrmDb db, MetadataSkeleton metadata, Security security, string RequestName) {
            this.security = security;
            this.core = core;
            this.db = db;
            this.metadata = metadata;
            this.RequestName = RequestName.ToLower();
        }

        internal Boolean HandlesRequest(string RequestName) {
            return this.RequestName == RequestName.ToLower();
        }
        abstract internal OrganizationResponse Execute(OrganizationRequest request, EntityReference userRef);

        public static T MakeRequest<T>(OrganizationRequest req) where T : OrganizationRequest {
            var typedReq = Activator.CreateInstance<T>();
            if (req.RequestName != typedReq.RequestName) {
                throw new MockupException($"Incorrect request type made. The name '{req.RequestName}' does not match '{typedReq.RequestName}'.");
            }
            typedReq.Parameters = req.Parameters;
            return typedReq;
        }

    }
}
