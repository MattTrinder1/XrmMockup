﻿using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System.Threading.Tasks;

namespace DG.Tools.XrmMockup.Database
{

    internal class XrmDb : IXrmDb
    {
        private Dictionary<string, DbTable> TableDict = new Dictionary<string, DbTable>();
        private Dictionary<string, EntityMetadata> EntityMetadata;
        private OrganizationServiceProxy OnlineProxy;

        public XrmDb(Dictionary<string, EntityMetadata> entityMetadata, OrganizationServiceProxy onlineProxy)
        {
            this.EntityMetadata = entityMetadata;
            this.OnlineProxy = onlineProxy;
        }

        public DbTable this[string tableName]
        {
            get
            {
                if (!TableDict.ContainsKey(tableName))
                {
                    if (!EntityMetadata.TryGetValue(tableName, out EntityMetadata entityMetadata))
                    {
                        throw new MockupException($"No EntityMetadata found for entity with logical name '{tableName}'.");
                    }
                    TableDict[tableName] = new DbTable(entityMetadata);
                }
                return TableDict[tableName];
            }
        }

        public void Add(Entity xrmEntity, bool withReferenceChecks = true)
        {
            var dbEntity = ToDbRow(xrmEntity, withReferenceChecks);
            this[dbEntity.Table.TableName][dbEntity.Id] = dbEntity;
        }

        public DbRow ToDbRow(Entity xrmEntity, bool withReferenceChecks = true)
        {
            var primaryIdKey = this[xrmEntity.LogicalName].Metadata.PrimaryIdAttribute;
            if (!xrmEntity.Attributes.ContainsKey(primaryIdKey))
            {
                xrmEntity[primaryIdKey] = xrmEntity.Id;
            }

            var dbEntity = DbRow.FromEntity(xrmEntity, this, withReferenceChecks);
            if (dbEntity.Id != Guid.Empty)
            {
                if (this[dbEntity.Table.TableName][dbEntity.Id] != null)
                {
                    throw new FaultException($"Trying to create entity '{xrmEntity.LogicalName}' and id '{xrmEntity.Id}', but a record already exists with that Id.");
                }
            }
            else
            {
                dbEntity.Id = Guid.NewGuid();
            }

            return dbEntity;
        }

        public void AddRange(IEnumerable<Entity> xrmEntities, bool withReferenceChecks = true)
        {
            foreach (var xrmEntity in xrmEntities) Add(xrmEntity, withReferenceChecks);
        }

        public IEnumerable<DbRow> GetDBEntityRows(string EntityLogicalName)
        {
            return this[EntityLogicalName];
        }

        public void Update(Entity xrmEntity, bool withReferenceChecks = true)
        {
            var currentDbRow = GetDbRow(xrmEntity);

            var dbEntity = DbRow.FromEntity(xrmEntity, withReferenceChecks ? this : null);
            this[dbEntity.Table.TableName][dbEntity.Id] = dbEntity;
        }

        public void Delete(Entity xrmEntity)
        {
            this[xrmEntity.LogicalName].Remove(xrmEntity.Id);
        }

        public bool HasRow(EntityReference reference)
        {
            var entityRef = this[reference.LogicalName][reference.Id];
            if (entityRef != null)
            {
                return true;
            }

#if !(XRM_MOCKUP_2011 || XRM_MOCKUP_2013 || XRM_MOCKUP_2015)
            // Try fetching with key attributes if any
            else if (reference?.KeyAttributes?.Count > 0)
            {
                var currentDbRow = this[reference.LogicalName].FirstOrDefault(row => reference.KeyAttributes.All(kv => row[kv.Key] == kv.Value));

                if (currentDbRow != null)
                {
                    return true;
                }
            }
#endif

            return false;

        }

        internal bool HasTable(string tableName)
        {
            return TableDict.ContainsKey(tableName);
        }

        public bool IsValidEntity(string entityLogicalName)
        {
            return EntityMetadata.TryGetValue(entityLogicalName, out EntityMetadata entityMetadata);
        }

        public void PrefillDBWithOnlineData(QueryExpression queryExpr)
        {
            if (OnlineProxy != null)
            {
                var onlineEntities = OnlineProxy.RetrieveMultiple(queryExpr).Entities;
                foreach (var onlineEntity in onlineEntities)
                {
                    if (this[onlineEntity.LogicalName][onlineEntity.Id] == null)
                    {
                        Add(onlineEntity, true);
                    }
                }
            }
        }

        public DbRow GetDbRow(EntityReference reference, bool withReferenceCheck = true)
        {
            DbRow currentDbRow = null;

            if (reference?.Id != Guid.Empty)
            {
                currentDbRow = this[reference.LogicalName][reference.Id];
                if (currentDbRow == null && OnlineProxy != null)
                {
                    if (!withReferenceCheck)
                        currentDbRow = DbRow.MakeDBRowRef(reference, this);
                    else
                    {
                        var onlineEntity = OnlineProxy.Retrieve(reference.LogicalName, reference.Id, new ColumnSet(true));
                        Add(onlineEntity, withReferenceCheck);
                        currentDbRow = this[reference.LogicalName][reference.Id];
                    }
                }
                if (currentDbRow == null)
                {
                    throw new FaultException($"The record of type '{reference.LogicalName}' with id '{reference.Id}' " +
                        "does not exist. If you use hard-coded records from CRM, then make sure you create those records before retrieving them.");
                }
            }

#if !(XRM_MOCKUP_2011 || XRM_MOCKUP_2013 || XRM_MOCKUP_2015)
            // Try fetching with key attributes if any
            else if (reference?.KeyAttributes?.Count > 0)
            {
                currentDbRow = this[reference.LogicalName].FirstOrDefault(row => reference.KeyAttributes.All(kv => row[kv.Key] == kv.Value));

                if (currentDbRow == null)
                {
                    throw new FaultException($"The record of type '{reference.LogicalName}' with key attributes '{reference.KeyAttributes.ToPrettyString()}' " +
                        "does not exist. If you use hard-coded records from CRM, then make sure you create those records before retrieving them.");
                }
            }
#endif
            // No identification given for the entity, throw error
            else
            {
                throw new FaultException($"Missing a form of identification for the desired record in order to retrieve it.");
            }

            return currentDbRow;
        }


        public DbRow GetDbRow(Entity xrmEntity)
        {
            return GetDbRow(xrmEntity.ToEntityReferenceWithKeyAttributes());
        }



        public DbRow GetDbRow(string logicalName, Guid id)
        {
            return GetDbRow(new EntityReference(logicalName, id));
        }

        public Entity GetEntity(string logicalName, Guid id)
        {
            return GetDbRow(logicalName, id).ToEntity();
        }

        public Entity GetEntity(EntityReference reference)
        {
            var e = GetDbRow(reference).ToEntity();
            SetFormattedValues(e);
            return e;

        }
        private Entity GetUnformattedEntity(EntityReference reference)
        {
            var e = GetDbRow(reference).ToEntity();
            return e;

        }


        #region GetOrNull
        internal DbRow GetDbRowOrNull(EntityReference reference)
        {
            if (HasRow(reference))
                return GetDbRow(reference);
            else
                return null;
        }

        public Entity GetEntityOrNull(EntityReference reference)
        {
            if (HasRow(reference))
                return GetEntity(reference);
            else
                return null;
        }
        private Entity GetUnformattedEntityOrNull(EntityReference reference)
        {
            if (HasRow(reference))
                return GetUnformattedEntity(reference);
            else
                return null;
        }
        #endregion

        public IXrmDb Clone()
        {
            var clonedTables = this.TableDict.ToDictionary(x => x.Key, x => x.Value.Clone());
            var clonedDB = new XrmDb(this.EntityMetadata, this.OnlineProxy)
            {
                TableDict = clonedTables
            };

            return clonedDB;
        }

        public void ResetAccessTeams()
        {
            var accessteams = TableDict["team"]
                                .Where(x => x.ToEntity().GetAttributeValue<OptionSetValue>("teamtype").Value == 1)
                                .Select(x => x.Id)
                                .ToList();
            foreach (var at in accessteams)
            {
                TableDict["team"].Remove(at);
            }
            TableDict.Remove("teammembership");

        }

        public IEnumerable<Entity> GetEntities(string tableName, IEnumerable<ConditionExpression> filters = null)
        {

            var rows = this[tableName];
            var entities = rows.Select(x => x.ToEntity()).ToList();
            Parallel.ForEach(entities, e =>
           {
               SetFormattedValues(e);
           });
            return entities;
        }

        internal void SetFormattedValues(Entity entity)
        {
            var validMetadata = this.EntityMetadata[entity.LogicalName].Attributes
                .Where(a => Utility.IsValidForFormattedValues(a));

            validMetadata = validMetadata.Except(validMetadata.Where(x => x.AttributeType == AttributeTypeCode.PartyList));

            var formattedValues = new List<KeyValuePair<string, string>>();
            foreach (var a in entity.Attributes)
            {
                if (a.Value == null) continue;
                var metadataAtt = validMetadata.Where(m => m.LogicalName == a.Key).FirstOrDefault();

                if (metadataAtt != null)
                {
                    EntityMetadata lookupMetadata = null;
                    if (metadataAtt is LookupAttributeMetadata)
                    {
                        if (entity[metadataAtt.LogicalName] is string && (string)entity[metadataAtt.LogicalName] == Guid.Empty.ToString())
                        {
                            //shouldnt happen as lookups should be entity references...
                            continue;
                        }

                        if (EntityMetadata.ContainsKey((metadataAtt as LookupAttributeMetadata).Targets[0]))
                        {
                            lookupMetadata = EntityMetadata[(metadataAtt as LookupAttributeMetadata).Targets[0]];
                        }

                    }
                    var label = GetFormattedValueLabel( metadataAtt, a.Value, entity, lookupMetadata);
                    if (label != null)
                    {
                        var formattedValuePair = new KeyValuePair<string, string>(a.Key, label);
                        formattedValues.Add(formattedValuePair);
                        if (a.Value is EntityReference)
                        {
                            (a.Value as EntityReference).Name = label;
                        }
                    }
                }
            }

            if (formattedValues.Count > 0)
            {
                entity.FormattedValues.AddRange(formattedValues);
            }
        }
        

        internal string GetFormattedValueLabel(AttributeMetadata metadataAtt, object value, Entity entity, EntityMetadata lookupMetadata = null)
        {
            if (metadataAtt is PicklistAttributeMetadata)
            {
                var optionset = (metadataAtt as PicklistAttributeMetadata).OptionSet.Options
                    .Where(opt => opt.Value == (value as OptionSetValue).Value).FirstOrDefault();
                return optionset.Label.UserLocalizedLabel.Label;
            }

            if (metadataAtt is BooleanAttributeMetadata)
            {
                var booleanOptions = (metadataAtt as BooleanAttributeMetadata).OptionSet;
                var label = (bool)value ? booleanOptions.TrueOption.Label : booleanOptions.FalseOption.Label;
                return label.UserLocalizedLabel.Label;
            }

            if (metadataAtt is MoneyAttributeMetadata)
            {
                var currencysymbol =
                    GetUnformattedEntity(
                        GetUnformattedEntity(entity.ToEntityReference())
                        .GetAttributeValue<EntityReference>("transactioncurrencyid"))
                    .GetAttributeValue<string>("currencysymbol");

                return currencysymbol + (value as Money).Value.ToString();
            }

            if (metadataAtt is LookupAttributeMetadata)
            {

                if (value is EntityReference)
                {
                    if (string.IsNullOrEmpty((value as EntityReference).Name))
                    {

                        var lookupEnt = GetUnformattedEntityOrNull(value as EntityReference);
                        if (lookupEnt != null)
                        {
                            var primaryAttr = lookupMetadata.Attributes.SingleOrDefault(x => x.IsPrimaryName.HasValue && x.IsPrimaryName.Value);
                            if (primaryAttr != null)
                            {
                                return lookupEnt.GetAttributeValue<string>(primaryAttr.LogicalName);
                            }
                        }


                    }
                    else
                    {
                        return (value as EntityReference).Name;
                    }

                }
                else
                {
                    Console.WriteLine("No lookup entity exists: ");
                }
            }

            if (metadataAtt is IntegerAttributeMetadata ||
                metadataAtt is DateTimeAttributeMetadata ||
                metadataAtt is MemoAttributeMetadata ||
                metadataAtt is DoubleAttributeMetadata ||
                metadataAtt is DecimalAttributeMetadata)
            {
                return value.ToString();
            }

            return null;
        }

        public IEnumerable<Entity> GetCallerTeamMembership(Guid callerId)
        {
            return this["teammembership"].Select(x => x.ToEntity()).Where(x => x.GetAttributeValue<Guid>("systemuserid") == callerId);
        }

        public IEnumerable<Entity> GetUnformattedEntities(string tableName)
        {
            var rows = this[tableName];
            return rows.Select(x => x.ToEntity()).ToList();
            
        }
    }
}
