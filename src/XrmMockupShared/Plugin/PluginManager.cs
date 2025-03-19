using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.ServiceModel;
using Microsoft.Xrm.Sdk.Metadata;
using System.Runtime.ExceptionServices;
using XrmMockupShared.Plugin;

namespace DG.Tools.XrmMockup
{
    public class InternalPluginStepConfig
    {
        public InternalPluginStepConfig()
        {
            Images = new List<InternalPluginStepImage>();
        }

        public string ClassName { get; set; }
        public int ExecutionStage { get; set; }
        public string EventOperation { get; set; }
        public string LogicalName { get; set; }
        public int Deployment { get; set; }
        public int ExecutionMode { get; set; }
        public string Name { get; set; }
        public int ExecutionOrder { get; set; }
        public string FilteredAttributes { get; set; }
        public string ImpersonatingUserId { get; set; }
        public int IsolationMode { get; set; }

        public List<InternalPluginStepImage> Images { get; set; }

    }

    public class InternalPluginStepImage
    {
        public string Name { get; set; }
        public string EntityAlias { get; set; }
        public int ImageType { get; set; }
        public string Attributes { get; set; }
    }


    internal class PluginManager
    {

        private Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>> registeredPlugins;
        private Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>> temporaryPlugins;
        private Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>> registeredSystemPlugins;

        // Queue for AsyncPlugins
        private Queue<PluginExecutionProvider> pendingAsyncPlugins = new Queue<PluginExecutionProvider>();

        private bool disableRegisteredPlugins = false;

        // List of SystemPlugins to execute
        private List<MockupPlugin> systemPlugins = new List<MockupPlugin>
        {
            new SystemPlugins.UpdateInactiveIncident(),
            new SystemPlugins.DefaultBusinessUnitTeams(),
            new SystemPlugins.DefaultBusinessUnitTeamMembers(),
            new SystemPlugins.SetAnnotationIsDocument()
        };

        public PluginManager(IEnumerable<Type> basePluginTypes, Dictionary<string, EntityMetadata> metadata, List<MetaPlugin> plugins)
        {
            registeredPlugins = new Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>>();
            temporaryPlugins = new Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>>();
            registeredSystemPlugins = new Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>>();

            RegisterPlugins(basePluginTypes, metadata, plugins, registeredPlugins);
            RegisterDirectPlugins(basePluginTypes, metadata, plugins, registeredPlugins);
            RegisterSystemPlugins(registeredSystemPlugins, metadata);
        }

        private void RegisterPlugins(IEnumerable<Type> basePluginTypes, Dictionary<string, EntityMetadata> metadata, List<MetaPlugin> plugins, Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>> register)
        {
            foreach (var basePluginType in basePluginTypes)
            {
                if (basePluginType == null) continue;
                Assembly proxyTypeAssembly = basePluginType.Assembly;

                foreach (var type in proxyTypeAssembly.GetLoadableTypes())
                {
                    if (type.BaseType != null && (type.BaseType == basePluginType || (type.BaseType.IsGenericType && type.BaseType.GetGenericTypeDefinition() == basePluginType)))
                    {
                        RegisterPlugin(type, metadata, plugins, register);
                    }
                }
            }
            SortAllLists(register);
        }

        private void RegisterDirectPlugins(IEnumerable<Type> pluginTypes, Dictionary<string, EntityMetadata> metadata, List<MetaPlugin> plugins, Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>> register)
        {
            if (pluginTypes == null) return;

            foreach (var pluginType in pluginTypes)
            {
                if (pluginType == null) continue;
                Assembly proxyTypeAssembly = pluginType.Assembly;

                foreach (var type in proxyTypeAssembly.GetLoadableTypes())
                {
                    if (!type.IsAbstract && type.GetInterface("IPlugin") == typeof(IPlugin) && type.BaseType == typeof(Object))
                    {
                        RegisterPlugin(type, metadata, plugins, register);
                    }
                }
            }
            SortAllLists(register);
        }


        private void RegisterPlugin(Type basePluginType, Dictionary<string, EntityMetadata> metadata, List<MetaPlugin> plugins, Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>> register)
        {
            object plugin = null;
            try
            {
                plugin = Activator.CreateInstance(basePluginType);
            }
            catch (Exception ex) when (ex.Source == "mscorlib" || ex.Source == "System.Private.CoreLib")
            {
            }

            if (plugin == null)
            {
                return;
            }

            Action<MockupServiceProviderAndFactory> pluginExecute = null;
            var pluginStepConfigs = new List<InternalPluginStepConfig>();

            //if (basePluginType.GetMethod("PluginProcessingStepConfigs") != null)
            //{ // Matches DAXIF plugin registration

            //    var configs = basePluginType
            //        .GetMethod("PluginProcessingStepConfigs")
            //        .Invoke(plugin, new object[] { })
            //        as IEnumerable<InternalPluginStepConfig>;

            //    pluginStepConfigs.AddRange(configs);

            //    pluginExecute = (provider) =>
            //    {
            //        basePluginType
            //        .GetMethod("Execute")
            //        .Invoke(plugin, new object[] { provider });
            //    };
            //}
            //else
            //{ 
                // Retrieve registration from CRM metadata
                var metaSteps =
                    plugins
                    .Where(x =>
                        x.AssemblyName == basePluginType.FullName &&
                        x.PluginAssemblyName == basePluginType.Assembly.GetName().Name)
                    .ToList();

                // fallback for backwards compatability for old Metadata files
                if (metaSteps == null || metaSteps.Count == 0)
                {
                    metaSteps =
                        plugins
                        .Where(x => x.AssemblyName == basePluginType.FullName)
                        .ToList();
                }

                if (metaSteps == null || metaSteps.Count == 0)
                {
                    throw new MockupException($"Unknown plugin '{basePluginType.FullName}', please use DAXIF registration or make sure the plugin is uploaded to CRM.");
                }

                foreach (var metaStep in metaSteps)
                {
                    var config = new InternalPluginStepConfig()
                    {
                        ClassName = metaStep.AssemblyName,
                        ExecutionStage = metaStep.Stage,
                        EventOperation = metaStep.MessageName,
                        LogicalName = metaStep.PrimaryEntity,
                        Deployment = 0,
                        ExecutionMode = metaStep.Mode,
                        Name = metaStep.Name,
                        ExecutionOrder = metaStep.Rank,
                        FilteredAttributes = metaStep.FilteredAttributes,
                        ImpersonatingUserId = metaStep.ImpersonatingUserId?.ToString(),
                        IsolationMode = metaStep.IsolationMode
                    };

                    foreach (var image in metaStep.Images)
                    {
                        config.Images.Add(new InternalPluginStepImage()
                        {
                            Name = image.Name,
                            EntityAlias= image.EntityAlias,
                            ImageType = image.ImageType,
                            Attributes = image.Attributes
                        });
                    }

                    pluginStepConfigs.Add(config);
                    pluginExecute = (provider) =>
                    {
                        basePluginType
                        .GetMethod("Execute")
                        .Invoke(plugin, new object[] { provider });
                    };
                }
           // }

            // Add discovered plugin triggers
            foreach (var stepConfig in pluginStepConfigs)
            {
                var stage = (ExecutionStage)stepConfig.ExecutionStage;
                var trigger = new PluginTrigger(stepConfig.EventOperation, stage, pluginExecute, stepConfig, metadata);
                AddTrigger(stepConfig.EventOperation.ToLower(), stage, trigger, register);
            }
        }

        public void ResetPlugins()
        {
            disableRegisteredPlugins = false;
            temporaryPlugins = new Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>>();
        }

        public void DisabelRegisteredPlugins(bool disable)
        {
            disableRegisteredPlugins = disable;
        }

        public void RegisterAdditionalPlugin(Type pluginType, Dictionary<string, EntityMetadata> metadata, List<MetaPlugin> plugins, PluginRegistrationScope scope)
        {
            if (pluginType.GetMethod("PluginProcessingStepConfigs") == null)
                throw new MockupException($"Unknown plugin '{pluginType.FullName}', please use the MockPlugin to register your plugin.");
            if (scope == PluginRegistrationScope.Permanent)
            {
                RegisterPlugin(pluginType, metadata, plugins, registeredPlugins);
            }
            else if (scope == PluginRegistrationScope.Temporary)
            {
                RegisterPlugin(pluginType, metadata, plugins, temporaryPlugins);
            }
        }

        private void RegisterSystemPlugins(Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>> register, Dictionary<string, EntityMetadata> metadata)
        {
            Action<MockupServiceProviderAndFactory> pluginExecute = null;
            var pluginStepConfigs = new List<InternalPluginStepConfig>();   

            foreach (var plugin in systemPlugins)
            {
                pluginStepConfigs.AddRange(plugin.PluginProcessingStepConfigs());
                pluginExecute = (provider) => plugin.Execute(provider);

                // Add discovered plugin triggers
                foreach (var stepConfig in pluginStepConfigs)
                {
                    var operation = stepConfig.EventOperation;
                    var stage = (ExecutionStage)stepConfig.ExecutionStage;
                    var trigger = new PluginTrigger(operation, stage, pluginExecute, stepConfig, metadata);

                    AddTrigger(operation, stage, trigger, register);
                }
            }
            SortAllLists(register);
        }

        public void AddTrigger(string operation, ExecutionStage stage, PluginTrigger trigger, Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>> register)
        {
            if (!register.ContainsKey(operation))
            {
                register.Add(operation, new Dictionary<ExecutionStage, List<PluginTrigger>>());
            }
            if (!register[operation].ContainsKey(stage))
            {
                register[operation].Add(stage, new List<PluginTrigger>());
            }
            register[operation][stage].Add(trigger);
        }

        /// <summary>
        /// Sorts all the registered which shares the same entry point based on their given order
        /// </summary>
        private void SortAllLists(Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>> plugins)
        {
            foreach (var dictEntry in plugins)
            {
                foreach (var listEntry in dictEntry.Value)
                {
                    listEntry.Value.Sort();
                }
            }
        }

        /// <summary>
        /// Trigger all plugin steps which match the given parameters.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="stage"></param>
        /// <param name="entity"></param>
        /// <param name="preImage"></param>
        /// <param name="postImage"></param>
        /// <param name="pluginContext"></param>
        /// <param name="core"></param>
        /// <param name="syncOnly"></param>
        //Post operation - Trigger Sync and Async in that order
        public void Trigger(string operation,
                                ExecutionStage stage,
                                object entity, 
                                Entity preImage, 
                                Entity postImage, 
                                PluginContext pluginContext, 
                                Core core,
                                bool syncOnly)
        {


            if (!disableRegisteredPlugins)
            {
                List<PluginTrigger> toExecuteRegistered = GetPluginsToExecute(registeredPlugins, operation, stage, entity, preImage, postImage, pluginContext, core, syncOnly);

                foreach (var ex in toExecuteRegistered.OrderBy(x => x.GetExecutionOrder()))
                {
                    ex.ExecutePlugin(entity, preImage, postImage, pluginContext, core);
                }
            }

            List<PluginTrigger> toExecuteTemporary = GetPluginsToExecute(temporaryPlugins, operation, stage, entity, preImage, postImage, pluginContext, core, syncOnly);

            foreach (var ex in toExecuteTemporary.OrderBy(x => x.GetExecutionOrder()))
            {
                ex.ExecutePlugin(entity, preImage, postImage, pluginContext, core);
            }


        }

        private List<PluginTrigger> GetPluginsToExecute(Dictionary<string, Dictionary<ExecutionStage, List<PluginTrigger>>> candidatePlugins, string operation, ExecutionStage stage, object entity, Entity preImage, Entity postImage, PluginContext pluginContext, Core core, bool syncOnly)
        {
            var toExecute = new List<PluginTrigger>();

            if (!candidatePlugins.ContainsKey(operation)) return toExecute;
            if (!candidatePlugins[operation].ContainsKey(stage)) return toExecute;


            var opStagePlugins = candidatePlugins[operation][stage];    

            if (syncOnly)
            {
                opStagePlugins = opStagePlugins.Where(x => x.GetExecutionMode() == ExecutionMode.Synchronous).ToList();
            }

            foreach (var plugin in opStagePlugins)
            {
                if (plugin.ShouldExecute(entity, preImage, postImage, pluginContext, core))
                {
                    toExecute.Add(plugin);
                }
            }

            return toExecute;
        }

        public void StageAsync(string operation, ExecutionStage stage,
                object entity, Entity preImage, Entity postImage, PluginContext pluginContext, Core core)
        {
            if (!disableRegisteredPlugins && registeredPlugins.ContainsKey(operation) && registeredPlugins[operation].ContainsKey(stage))
            {
                var asyncExecutors = registeredPlugins[operation][stage].Where(p => p.GetExecutionMode() == ExecutionMode.Asynchronous)
                    .OrderBy(p => p.GetExecutionOrder()).ToList().Select(p => p.ToPluginExecution(entity, preImage, postImage, pluginContext, core));
                asyncExecutors.ToList().ForEach(x => pendingAsyncPlugins.Enqueue(x));
            }
            if (temporaryPlugins.ContainsKey(operation) && temporaryPlugins[operation].ContainsKey(stage))
            {
                var asyncExecutors = temporaryPlugins[operation][stage].Where(p => p.GetExecutionMode() == ExecutionMode.Asynchronous)
                    .OrderBy(p => p.GetExecutionOrder()).ToList().Select(p => p.ToPluginExecution(entity, preImage, postImage, pluginContext, core));
                asyncExecutors.ToList().ForEach(x => pendingAsyncPlugins.Enqueue(x));
            }
        }

        public void TriggerAsyncWaitingJobs()
        {
            while (pendingAsyncPlugins.Count > 0)
            {
                var pendingPlugin = pendingAsyncPlugins.Dequeue();

                if (pendingPlugin != null)
                {
                    pendingPlugin.ExecuteAction();
                }
            }
        }

        public void TriggerSystem(string operation, ExecutionStage stage,
                object entity, Entity preImage, Entity postImage, PluginContext pluginContext, Core core)
        {
            List<PluginTrigger> toExecuteSystem = GetPluginsToExecute(registeredSystemPlugins, operation, stage, entity, preImage, postImage, pluginContext, core, syncOnly: false);

            foreach (var ex in toExecuteSystem.OrderBy(x => x.GetExecutionOrder()))
            {
                ex.ExecutePlugin(entity, preImage, postImage, pluginContext, core);
            }
        }

        internal class PluginTrigger : IComparable<PluginTrigger>
        {
            public Action<MockupServiceProviderAndFactory> pluginExecute;

            string entityName;
            string operation;
            ExecutionStage stage;
            ExecutionMode mode;
            int isolationMode;
            int order = 0;
            Dictionary<string, EntityMetadata> metadata;
            string impersonatingUserId;

            HashSet<string> attributes;
            List<InternalPluginStepImage> images;

            public PluginTrigger(string operation, ExecutionStage stage,
                    Action<MockupServiceProviderAndFactory> pluginExecute, InternalPluginStepConfig stepConfig, Dictionary<string, EntityMetadata> metadata)
            {
                this.pluginExecute = pluginExecute;
                this.entityName = stepConfig.LogicalName;
                this.operation = operation.ToLower();
                this.stage = stage;
                this.isolationMode = stepConfig.IsolationMode;
                this.mode = (ExecutionMode)stepConfig.ExecutionMode;
                this.order = stepConfig.ExecutionOrder;
                this.images = stepConfig.Images;
                this.metadata = metadata;
                this.impersonatingUserId = stepConfig.ImpersonatingUserId;

                var attrs = stepConfig.FilteredAttributes ?? "";
                this.attributes = String.IsNullOrWhiteSpace(attrs) ? new HashSet<string>() : new HashSet<string>(attrs.Split(','));
            }

            public ExecutionMode GetExecutionMode()
            {
                return mode;
            }

            public int GetExecutionOrder()
            {
                return order;
            }

            // Saves "execution" for Async plugins to be executed after sync plugins.
            public PluginExecutionProvider ToPluginExecution(object entityObject, Entity preImage, Entity postImage, PluginContext pluginContext, Core core)
            {
                var entity = entityObject as Entity;
                var entityRef = entityObject as EntityReference;

                var guid = (entity != null) ? entity.Id : entityRef.Id;
                var logicalName = (entity != null) ? entity.LogicalName : entityRef.LogicalName;

                if (VerifyPluginTrigger(entity, logicalName, guid, preImage, postImage, pluginContext))
                {
                    // Create the plugin context
                    var thisPluginContext = CreatePluginContext(pluginContext, guid, logicalName, preImage, postImage);
                    return new PluginExecutionProvider(pluginExecute, new MockupServiceProviderAndFactory(core, thisPluginContext, core.TracingServiceFactory));
                }

                return null;
            }

            public void ExecuteIfMatch(object entityObject, Entity preImage, Entity postImage, PluginContext pluginContext, Core core)
            {
                // Check if it is supposed to execute. Returns preemptively, if it should not.
                var entity = entityObject as Entity;
                var entityRef = entityObject as EntityReference;

                var guid = (entity != null) ? entity.Id : entityRef.Id;
                var logicalName = (entity != null) ? entity.LogicalName : entityRef.LogicalName;

                if (VerifyPluginTrigger(entity, logicalName, guid, preImage, postImage, pluginContext))
                {
                    var thisPluginContext = CreatePluginContext(pluginContext, guid, logicalName, preImage, postImage);

                    //Create Serviceprovider, and execute plugin
                    MockupServiceProviderAndFactory provider = new MockupServiceProviderAndFactory(core, thisPluginContext, core.TracingServiceFactory);
                    try
                    {
                        pluginExecute(provider);
                    }
                    catch (TargetInvocationException e)
                    {
                        ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                    }

                    foreach (var parameter in thisPluginContext.SharedVariables)
                    {
                        pluginContext.SharedVariables[parameter.Key] = parameter.Value;
                    }
                }
            }

            public void ExecutePlugin(object entityObject, Entity preImage, Entity postImage, PluginContext pluginContext, Core core)
            {
                // Check if it is supposed to execute. Returns preemptively, if it should not.
                var entity = entityObject as Entity;
                var entityRef = entityObject as EntityReference;

                var guid = (entity != null) ? entity.Id : entityRef.Id;
                var logicalName = (entity != null) ? entity.LogicalName : entityRef.LogicalName;

                var thisPluginContext = CreatePluginContext(pluginContext, guid, logicalName, preImage, postImage);

                //Create Serviceprovider, and execute plugin
                MockupServiceProviderAndFactory provider = new MockupServiceProviderAndFactory(core, thisPluginContext, core.TracingServiceFactory);
                try
                {
                    pluginExecute(provider);
                }
                catch (TargetInvocationException e)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                }

                foreach (var parameter in thisPluginContext.SharedVariables)
                {
                    pluginContext.SharedVariables[parameter.Key] = parameter.Value;
                }
            }

            public bool ShouldExecute(object entityObject, Entity preImage, Entity postImage, PluginContext pluginContext, Core core)
            {
                // Check if it is supposed to execute. Returns preemptively, if it should not.
                var entity = entityObject as Entity;
                var entityRef = entityObject as EntityReference;

                var guid = (entity != null) ? entity.Id : entityRef.Id;
                var logicalName = (entity != null) ? entity.LogicalName : entityRef.LogicalName;

                if (VerifyPluginTrigger(entity, logicalName, guid, preImage, postImage, pluginContext))
                {
                    return true;
                }
                return false;
            }

            private void CheckInfiniteLoop(PluginContext pluginContext)
            {
                if (pluginContext.Depth > 8)
                {
                    throw new FaultException(
                        "This workflow job was canceled because the workflow that started it included an infinite loop." +
                        " Correct the workflow logic and try again.");
                }
            }

            private void CheckSpecialRequest()
            {
                if (entityName != "" && (operation == nameof(EventOperation.Associate).ToLower() || operation == nameof(EventOperation.Disassociate).ToLower()))
                {
                    throw new MockupException(
                        $"An {operation} plugin step was registered for a specific entity, which can only be registered on AnyEntity");
                }
            }

            private Entity AddPostImageAttributesToEntity(Entity entity, Entity preImage, Entity postImage)
            {
                if (operation == nameof(EventOperation.Update).ToLower() && stage == ExecutionStage.PostOperation)
                {
                    var shadowAddedAttributes = postImage.Attributes.Where(a => !preImage.Attributes.ContainsKey(a.Key) && !entity.Attributes.ContainsKey(a.Key));
                    entity = entity.CloneEntity();
                    entity.Attributes.AddRange(shadowAddedAttributes);
                }
                return entity;
            }

            private bool FilteredAttributesMatches(Entity entity)
            {
                if (operation != nameof(EventOperation.Update).ToLower() || attributes.Count == 0)
                {
                    return true;
                }

                bool foundAttr = false;
                foreach (var attr in entity.Attributes)
                {
                    if (attributes.Contains(attr.Key))
                    {
                        foundAttr = true;
                        break;
                    }
                }
                return foundAttr;
            }

            private bool VerifyPluginTrigger(Entity entity, string logicalName, Guid guid, Entity preImage, Entity postImage, PluginContext pluginContext)
            {
                if (entityName != "" && entityName != logicalName) return false;

                if (entity != null && metadata.GetMetadata(logicalName)?.PrimaryIdAttribute != null)
                {
                    entity[metadata.GetMetadata(logicalName).PrimaryIdAttribute] = guid;
                }

                CheckInfiniteLoop(pluginContext);
                entity = AddPostImageAttributesToEntity(entity, preImage, postImage);
                CheckSpecialRequest();

                if (FilteredAttributesMatches(entity))
                {
                    return true;
                }
                return false;
            }

            private PluginContext CreatePluginContext(PluginContext pluginContext, Guid guid, string logicalName, Entity preImage, Entity postImage)
            {
                var thisPluginContext = pluginContext.Clone();
                thisPluginContext.Mode = (int)this.mode;
                thisPluginContext.Stage = (int)this.stage;
                thisPluginContext.IsolationMode = (int)this.isolationMode;
                if (thisPluginContext.PrimaryEntityId == Guid.Empty)
                {
                    thisPluginContext.PrimaryEntityId = guid;
                }
                thisPluginContext.PrimaryEntityName = logicalName;
                if (Guid.TryParse(this.impersonatingUserId, out Guid impersonatingUserId) && impersonatingUserId != Guid.Empty)
                {
                    thisPluginContext.UserId = impersonatingUserId;
                }

                foreach (var image in this.images)
                {
                    var type = (ImageType)image.ImageType;
                    var cols = image.Attributes != null ? new ColumnSet(image.Attributes.Split(',')) : new ColumnSet(true);
                    if (postImage != null && stage == ExecutionStage.PostOperation && (type == ImageType.PostImage || type == ImageType.Both))
                    {
                        thisPluginContext.PostEntityImages.Add(image.Name, postImage.CloneEntity(metadata.GetMetadata(postImage.LogicalName), cols));
                    }
                    if (preImage != null && type == ImageType.PreImage || type == ImageType.Both)
                    {
                        thisPluginContext.PreEntityImages.Add(image.Name, preImage.CloneEntity(metadata.GetMetadata(preImage.LogicalName), cols));
                    }
                }
                return thisPluginContext;
            }

            public int CompareTo(PluginTrigger other)
            {
                return this.order.CompareTo(other.order);
            }
        }
    }
}