﻿using DG.Tools.XrmMockup.Database;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using System.Text;

namespace XrmMockupShared.Database
{
    class SQLDb : IXrmDb
    {

        private Dictionary<string, EntityMetadata> EntityMetadata;
        private string ConnectionString;

        public SQLDb(Dictionary<string, EntityMetadata> entityMetadata, string connectionString,bool recreateDatabase)
        {
            this.EntityMetadata = entityMetadata;
            this.ConnectionString = connectionString;

            if (recreateDatabase)
            {
                InitialiseDatabase();
            }

        }

        private void InitialiseDatabase()
        {
            foreach (var entityMeta in this.EntityMetadata)
            {
                CreateTable(entityMeta);
            }
        }

        private void DropTableIfExists(string tableName)
        {

            string sql = $"IF (OBJECT_ID('{tableName}') IS NOT NULL )" +
                         "BEGIN " +
                         $"DROP TABLE {tableName} " +
                         "END";

            ExecuteNonQuery(sql);
        }

        private void CreateTable(KeyValuePair<string, EntityMetadata> entityMeta)
        {
            DropTableIfExists(entityMeta.Key);

            var primaryAttribute = entityMeta.Value.Attributes.Where(x => x.LogicalName == entityMeta.Value.PrimaryIdAttribute).Single();
            
            var createSQL = $"create table [{entityMeta.Key}] ( ";

            foreach (var attr in entityMeta.Value.Attributes
                                                    .Where(x => x.AttributeType != AttributeTypeCode.Virtual 
                                                             && x.AttributeType != AttributeTypeCode.EntityName 
                                                             && x.AttributeType != AttributeTypeCode.ManagedProperty
                                                             && x.AttributeOf == null).OrderBy(x=>x.LogicalName))
            {
                createSQL += $" [{attr.LogicalName}] {GetFieldDataType(attr)}, ";

                if (attr.LogicalName == primaryAttribute.LogicalName)
                {
                    createSQL += $"CONSTRAINT PK_{entityMeta.Key}_{attr.LogicalName} PRIMARY KEY CLUSTERED({attr.LogicalName}),";
                }

                //, 


            }

            createSQL = createSQL.Trim().TrimEnd(new char[] { ',' });
            createSQL += " )";

            ExecuteNonQuery(createSQL);
        }

        private string GetFieldDataType(AttributeMetadata attr)
        {
            switch (attr.AttributeType)
            {

                case AttributeTypeCode.Lookup:
                case AttributeTypeCode.Uniqueidentifier:
                case AttributeTypeCode.Owner:
                case AttributeTypeCode.Customer:
                    return "uniqueidentifier";
                case AttributeTypeCode.State:
                case AttributeTypeCode.Status:
                case AttributeTypeCode.Integer:
                case AttributeTypeCode.Picklist:
                    return "int";
                case AttributeTypeCode.DateTime:
                    return "datetime";
                case AttributeTypeCode.BigInt:
                    return "bigint";
                case AttributeTypeCode.Boolean:
                    return "bit";
                case AttributeTypeCode.String:

                    var stringAttr = attr as StringAttributeMetadata;
                    if (stringAttr.MaxLength > 8000)
                    {
                        return $"nvarchar (max)";
                    }
                    else
                    {
                        return $"nvarchar ({(attr as StringAttributeMetadata).MaxLength.ToString()})";
                    }

                case AttributeTypeCode.PartyList:
                    return $"nvarchar (500)";
                case AttributeTypeCode.Memo:
                    return $"nvarchar (MAX)";
                case AttributeTypeCode.Money:
                    return $"money";
                case AttributeTypeCode.Decimal:
                    return $"decimal (20,{(attr as DecimalAttributeMetadata).Precision.ToString()})";
                case AttributeTypeCode.Double:
                    return $"float";

                default:
                    return "";
            }

        }

        private DbTable GetTable(string tableName)
        {
            var dataTable = ExecuteReader($"SELECT * FROM [{tableName}]");
            
            var dbTable = new DbTable(this.EntityMetadata[tableName]);

            foreach (var row in dataTable.Rows)
            { 
                //var dbRow = new DbRow()
            }

            return dbTable;

        }

        private DataTable ExecuteReader(string sql)
        {
            using (var conn = new SqlConnection(this.ConnectionString))
            {

                using (var cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();

                    DataTable dt = new DataTable();
                    dt.Load(reader);
                    return dt;
                    
                }
            }
        }

        private void ExecuteNonQuery(string sql, List<SqlParameter> paramValues = null)
        {
            using (var conn = new SqlConnection(this.ConnectionString))
            {
                using (var cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();
                    if (paramValues != null)
                    {
                        cmd.Parameters.AddRange(paramValues.ToArray());
                    }
                    cmd.ExecuteNonQuery();
                    conn.Close();
                }
            }
        }

        public DbTable this[string tableName] => throw new NotImplementedException();

        public void Add(Entity xrmEntity, bool withReferenceChecks = true)
        {
            var entityMetadata = this.EntityMetadata[xrmEntity.LogicalName];
            var sqlAttributes = entityMetadata.Attributes.Where(x => x.AttributeType != AttributeTypeCode.Virtual && x.AttributeType != AttributeTypeCode.EntityName && x.AttributeType != AttributeTypeCode.ManagedProperty);

            var primaryAttributes = sqlAttributes.Where(x => x.IsPrimaryId.Value);
            if (primaryAttributes.Count() > 1)
            {
                primaryAttributes = primaryAttributes.Where(x => x.LogicalName == xrmEntity.LogicalName + "id");
            }
            var primaryAttribute = primaryAttributes.SingleOrDefault();

            var sqlFields = new List<string>();
            var sqlValues = new List<string>();
            var paramValues = new List<SqlParameter>();

            sqlFields.Add($"[{primaryAttribute.LogicalName}]");
            sqlValues.Add($"@{primaryAttribute.LogicalName}");
            paramValues.Add(new SqlParameter($"@{primaryAttribute.LogicalName}", xrmEntity.Id));

            //string sqlFields = string.Empty;
            //string sqlValues = string.Empty;

            foreach (var attr in sqlAttributes.Except(new List<AttributeMetadata>() { primaryAttribute }))
            {
                if (xrmEntity.Contains(attr.LogicalName))
                {
                    sqlFields.Add($"[{attr.LogicalName}]");
                    sqlValues.Add($"@{attr.LogicalName}");
                    paramValues.Add(new SqlParameter($"@{attr.LogicalName}", GetSqlValue(xrmEntity, attr)));
                }
            }

            var fieldSQL = string.Join(",", sqlFields);
            var valueSQL = string.Join(",", sqlValues);
            
            var insertSQL = $"insert into [{xrmEntity.LogicalName}] ({fieldSQL}) VALUES ({valueSQL})";

            try
            {
                ExecuteNonQuery(insertSQL, paramValues);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Cannot insert duplicate key"))
                {
                    throw new FaultException(ex.Message);
                }
                else
                {
                    throw;
                }
            }

            
        }

        private object GetSqlValue(Entity xrmEntity, AttributeMetadata attr)
        {

            if (xrmEntity[attr.LogicalName] == null)
            {
                return DBNull.Value;
            }
            else
            {

                switch (attr.AttributeType)
                {

                    case AttributeTypeCode.Lookup:
                    case AttributeTypeCode.Owner:
                    case AttributeTypeCode.Customer:
                        if (xrmEntity[attr.LogicalName] is EntityReference)
                        {
                            return xrmEntity.GetAttributeValue<EntityReference>(attr.LogicalName).Id;
                        }
                        else
                        {
                            //this shouldnt actually happen...
                            return xrmEntity[attr.LogicalName];
                        }

                    case AttributeTypeCode.State:
                    case AttributeTypeCode.Status:
                    case AttributeTypeCode.Picklist:
                        return xrmEntity.GetAttributeValue<OptionSetValue>(attr.LogicalName).Value;
                    case AttributeTypeCode.Money:
                        return xrmEntity.GetAttributeValue<Money>(attr.LogicalName).Value;
                    default:
                        return (xrmEntity[attr.LogicalName]);
                }
            }
        }

        private string TrimTrailingComma(string sql)
        {
            return sql.Trim().TrimEnd(new char[] { ',' });
        }

        private string GetFieldValue(Entity xrmEntity, AttributeMetadata attr)
        {
            switch (attr.AttributeType)
            {

                case AttributeTypeCode.Lookup:
                case AttributeTypeCode.Uniqueidentifier:
                case AttributeTypeCode.Owner:
                case AttributeTypeCode.Customer:
                    return "uniqueidentifier";
                case AttributeTypeCode.State:
                case AttributeTypeCode.Status:
                case AttributeTypeCode.Integer:
                case AttributeTypeCode.Picklist:
                    return "int";
                case AttributeTypeCode.DateTime:
                    return "datetime";
                case AttributeTypeCode.BigInt:
                    return "bigint";
                case AttributeTypeCode.Boolean:
                    return "bit";
                case AttributeTypeCode.String:

                    var stringAttr = attr as StringAttributeMetadata;
                    if (stringAttr.MaxLength > 8000)
                    {
                        return $"nvarchar (max)";
                    }
                    else
                    {
                        return $"nvarchar ({(attr as StringAttributeMetadata).MaxLength.ToString()})";
                    }

                case AttributeTypeCode.PartyList:
                    return $"nvarchar (500)";
                case AttributeTypeCode.Memo:
                    return $"nvarchar (MAX)";
                case AttributeTypeCode.Money:
                    return $"money";
                case AttributeTypeCode.Decimal:
                    return $"decimal (20,{(attr as DecimalAttributeMetadata).Precision.ToString()})";
                case AttributeTypeCode.Double:
                    return $"float";

                default:
                    return "";
            }
        }

        public void AddRange(IEnumerable<Entity> xrmEntities, bool withReferenceChecks = true)
        {
            foreach(var xrmEntity in xrmEntities)
            {
                Add(xrmEntity, withReferenceChecks);
            }
        }

        public IXrmDb Clone()
        {
            throw new NotImplementedException();
        }

        public void Delete(Entity xrmEntity)
        {
            var sql = $"delete from {xrmEntity.LogicalName} where {xrmEntity.LogicalName}id = '{xrmEntity.Id.ToString()}'";
            ExecuteReader(sql);
            
        }

        public IEnumerable<DbRow> GetDBEntityRows(string teamMembership)
        {
            throw new NotImplementedException();
        }

        public DbRow GetDbRow(Entity entity)
        {
            throw new NotImplementedException();
        }

        public DbRow GetDbRow(EntityReference reference, bool v)
        {

            // var row = new DbRow()


            throw new NotImplementedException();
        }

        public DbRow GetDbRow(EntityReference entityReference)
        {
            throw new NotImplementedException();
        }

        public DbRow GetDbRowOrNull(EntityReference entityReference)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Entity> GetEntities(string tableName)
        {
            var sql = $"select * from {tableName}";
            var data = ExecuteReader(sql);
            var entities = new List<Entity>();
            foreach (var row in data.Rows)
            {
                entities.Add(RowToEntity((DataRow)row,tableName));
            }
            return entities;
        }

        public Entity GetEntity(EntityReference reference)
        {
            var sql = $"select * from {reference.LogicalName} where {reference.LogicalName}id = '{reference.Id.ToString()}'";
            var data = ExecuteReader(sql);
            if (data.Rows.Count == 0)
            {
                throw new FaultException($"{reference.LogicalName} with {reference.LogicalName}id = '{reference.Id.ToString()}' not found");
            }
            var row = data.Rows[0];
            return RowToEntity(row,reference.LogicalName);
        }

        private Entity RowToEntity(DataRow row,string logicalName)
        {
            var entity = new Entity(logicalName);
            
            var primaryAttributes = this.EntityMetadata[logicalName].Attributes.Where(x => x.IsPrimaryId.Value);
            if (primaryAttributes.Count() > 1)
            {
                primaryAttributes = primaryAttributes.Where(x => x.LogicalName == logicalName + "id");
            }
            var primaryAttribute = primaryAttributes.SingleOrDefault();
            entity.Id = (Guid)row[primaryAttribute.LogicalName];

            foreach (var attr in this.EntityMetadata[logicalName].Attributes)
            {
                if (row.Table.Columns.Contains(attr.LogicalName))
                {
                    if (row[attr.LogicalName] != System.DBNull.Value)
                    {
                        entity[attr.LogicalName] = GetDynamicsValue(row, attr);
                    }

                }
            }

            return entity;
        }

        private object GetDynamicsValue(DataRow row, AttributeMetadata attr)
        {
            switch (attr.AttributeType)
            {

                case AttributeTypeCode.Lookup:
                case AttributeTypeCode.Owner:
                case AttributeTypeCode.Customer:
                    return new EntityReference((attr as LookupAttributeMetadata).Targets[0], (Guid)row[attr.LogicalName]);

                case AttributeTypeCode.State:
                case AttributeTypeCode.Status:
                case AttributeTypeCode.Picklist:
                    return new OptionSetValue((int)row[attr.LogicalName]);
                case AttributeTypeCode.Money:
                    return new Money((decimal)row[attr.LogicalName]);
                default:
                    return (row[attr.LogicalName]);

            }
        }

        public Entity GetEntity(string logicalName, Guid id)
        {
            return GetEntity(new EntityReference(logicalName, id));
        }

        public object GetEntityOrNull(EntityReference entityReference)
        {
            throw new NotImplementedException();
        }

        public bool HasRow(EntityReference entityReference)
        {
            var sql = $"select * from {entityReference.LogicalName} where {entityReference.LogicalName}id = '{entityReference.Id.ToString()}'";
            var data = ExecuteReader(sql);
            return data.Rows.Count > 0;
        }

        public bool IsValidEntity(string logicalName)
        {
            throw new NotImplementedException();
        }

        public void PrefillDBWithOnlineData(QueryExpression queryExpr)
        {
            
        }

        public DbRow ToDbRow(Entity xrmEntity, bool withReferenceChecks = true)
        {
            throw new NotImplementedException();
        }

        public void Update(Entity xrmEntity, bool withReferenceChecks = true)
        {
            throw new NotImplementedException();
        }

        Entity IXrmDb.GetEntityOrNull(EntityReference entityReference)
        {
            var sql = $"select * from {entityReference.LogicalName} where {entityReference.LogicalName}id = '{entityReference.Id.ToString()}'";
            var data = ExecuteReader(sql);
            if (data.Rows.Count == 0)
            {
                return null;
            }
            else
            {
                var row = data.Rows[0];
                return RowToEntity(row, entityReference.LogicalName);
            }
        }
    }
}
