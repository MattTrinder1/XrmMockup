using DG.Tools;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Xml;

namespace DG.Tools.XrmMockup.Metadata
{
    class Program
    {

        public static ArgumentParser ParsedArgs;

        static List<string> listOfAssemblies = new List<string>();

        private static Assembly ResolveXrmAssemblies(object sender, ResolveEventArgs args)
        {
            if (listOfAssemblies.Contains(args.Name))
            {
                return null;
            }
            try
            {
                listOfAssemblies.Add(args.Name);
                return AssemblyGetter.GetAssemblyFromName(args.Name);
            }
            finally
            {
                listOfAssemblies.Remove(args.Name);
            }
        }

        static void Main(string[] args)
        {
            ParsedArgs = new ArgumentParser(Arguments.ArgList, args[0]);

            if (ParsedArgs.GetAsType<bool>(Arguments.fetchFromAssemblies))
            {
                AppDomain.CurrentDomain.AssemblyResolve += ResolveXrmAssemblies;
            }

            GenerateMetadata();
        }

        static void GenerateMetadata()
        {
            var auth = new AuthHelper(
                ParsedArgs[Arguments.Url],
                ParsedArgs[Arguments.Username],
                ParsedArgs[Arguments.Password],
                ParsedArgs[Arguments.AuthProvider],
                ParsedArgs[Arguments.Domain],
                ParsedArgs[Arguments.Method],
                ParsedArgs[Arguments.ClientId],
                ParsedArgs[Arguments.ReturnUrl],
                ParsedArgs[Arguments.ClientSecret],
                ParsedArgs[Arguments.ConnectionString]
            );

            Console.WriteLine("Generation of metadata files started");
            var generator = new DataHelper(auth.Authenticate(), ParsedArgs[Arguments.Entities], ParsedArgs[Arguments.Solutions], ParsedArgs.GetAsType<bool>(Arguments.fetchFromAssemblies));
            var outputLocation = ParsedArgs[Arguments.OutDir] ?? Directory.GetCurrentDirectory();

            var skeleton = generator.GetMetadata();

            var serializer = new DataContractSerializer(typeof(MetadataSkeleton));
            var workflowSerializer = new DataContractSerializer(typeof(Entity));
            var securitySerializer = new DataContractSerializer(typeof(SecurityRole));

            var workflowsLocation = Path.Combine(outputLocation, "Workflows");
            var securityLocation = Path.Combine(outputLocation, "SecurityRoles");
            var entityLocation = Path.Combine(outputLocation, "Entities");


            Console.WriteLine("Deleting old files");

            Directory.CreateDirectory(workflowsLocation);
            foreach (var file in Directory.EnumerateFiles(workflowsLocation, "*.xml"))
            {
                File.Delete(file);
            }

            Directory.CreateDirectory(securityLocation);
            foreach (var file in Directory.EnumerateFiles(securityLocation, "*.xml"))
            {
                File.Delete(file);
            }

            Console.WriteLine("Writing files");

            Directory.CreateDirectory(outputLocation);
            Directory.CreateDirectory(entityLocation);

            if (Convert.ToBoolean(ParsedArgs[Arguments.SeperateFiles]))
            {
                foreach (var file in Directory.EnumerateFiles(outputLocation, "*Metadata.xml"))
                {
                    if (Path.GetFileName(file).ToLower() != "additionalmetadata.xml")
                    {
                        File.Delete(file);
                    }

                }

                foreach (var file in Directory.EnumerateFiles(entityLocation, "*Metadata.xml"))
                {
                    File.Delete(file);
                }


                Console.WriteLine("\tEntity Metadata");

                if (Convert.ToBoolean(ParsedArgs[Arguments.SeperateEntityFiles]))
                {
                    foreach (var e in skeleton.EntityMetadata)
                    {
                        Console.WriteLine($"\t\t{e.Key}");
                        serializer = new DataContractSerializer(typeof(KeyValuePair<string, EntityMetadata>));
                        using (var stream = new FileStream(entityLocation + $"/{e.Key}EntityMetadata.xml", FileMode.Create))
                        {
                            using (var writer = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true, IndentChars = "\t" }))
                                serializer.WriteObject(writer, e);
                        }
                    }
                }
                else
                {
                    serializer = new DataContractSerializer(typeof(Dictionary<string, EntityMetadata>));
                    using (var stream = new FileStream(outputLocation + "/EntityMetadata.xml", FileMode.Create))
                    {
                        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true, IndentChars = "\t" }))
                            serializer.WriteObject(writer, skeleton.EntityMetadata);
                    }
                }

                Console.WriteLine("\tDefaultStateStatus Metadata");
                serializer = new DataContractSerializer(typeof(Dictionary<string, Dictionary<int, int>>));
                using (var stream = new FileStream(outputLocation + "/DefaultStateStatusMetadata.xml", FileMode.Create))
                {
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true, IndentChars = "\t" }))
                        serializer.WriteObject(writer, skeleton.DefaultStateStatus);
                }

                Console.WriteLine("\tCurrencies Metadata");
                serializer = new DataContractSerializer(typeof(List<Entity>));
                using (var stream = new FileStream(outputLocation + "/CurrenciesMetadata.xml", FileMode.Create))
                {
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true, IndentChars = "\t" }))
                        serializer.WriteObject(writer, skeleton.Currencies);
                }

                Console.WriteLine("\tBaseOrganization Metadata");
                serializer = new DataContractSerializer(typeof(Entity));
                using (var stream = new FileStream(outputLocation + "/BaseOrganizationMetadata.xml", FileMode.Create))
                {
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true, IndentChars = "\t" }))
                        serializer.WriteObject(writer, skeleton.BaseOrganization);
                }

                Console.WriteLine("\tRootBusinessUnit Metadata");
                serializer = new DataContractSerializer(typeof(Entity));
                using (var stream = new FileStream(outputLocation + "/RootBusinessUnitMetadata.xml", FileMode.Create))
                {
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true, IndentChars = "\t" }))
                        serializer.WriteObject(writer, skeleton.RootBusinessUnit);
                }

                Console.WriteLine("\tPlugins Metadata");
                serializer = new DataContractSerializer(typeof(List<MetaPlugin>));
                using (var stream = new FileStream(outputLocation + "/PluginsMetadata.xml", FileMode.Create))
                {
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true, IndentChars = "\t" }))
                        serializer.WriteObject(writer, skeleton.Plugins);
                }

                Console.WriteLine("\tOptionSets Metadata");
                serializer = new DataContractSerializer(typeof(OptionSetMetadataBase[]));
                using (var stream = new FileStream(outputLocation + "/OptionSetsMetadata.xml", FileMode.Create))
                {
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true, IndentChars = "\t" }))
                        serializer.WriteObject(writer, skeleton.OptionSets);
                }

                Console.WriteLine("\tAccessTeamTemplate Metadata");
                serializer = new DataContractSerializer(typeof(List<Entity>));
                using (var stream = new FileStream(outputLocation + "/AccessTeamTemplatesMetadata.xml", FileMode.Create))
                {
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings() { Indent = true, IndentChars = "\t" }))
                        serializer.WriteObject(writer, skeleton.AccessTeamTemplates);
                }
            }
            else
            {
                using (var stream = new FileStream(outputLocation + "/Metadata.xml", FileMode.Create))
                {
                    serializer.WriteObject(stream, skeleton);
                }
            }

            Console.WriteLine("\tWorkflows");
            foreach (var workflow in generator.GetWorkflows())
            {
                var safeName = ToSafeName(workflow.GetAttributeValue<string>("name"));
                using (var stream = new FileStream($"{workflowsLocation}/{safeName}.xml", FileMode.Create))
                {
                    workflowSerializer.WriteObject(stream, workflow);
                }
            }

            Console.WriteLine("\tSecurity Roles");
            var securityRoles = generator.GetSecurityRoles(skeleton.RootBusinessUnit.Id);
            foreach (var securityRole in securityRoles)
            {
                var safeName = ToSafeName(securityRole.Value.Name);
                using (var stream = new FileStream($"{securityLocation}/{safeName}.xml", FileMode.Create))
                {
                    securitySerializer.WriteObject(stream, securityRole.Value);
                }
            }

            // Write to TypeDeclarations file
            Console.WriteLine("\tType Declarations");

            var typedefFile = Path.Combine(outputLocation, "TypeDeclarations.cs");

            using (var file = new StreamWriter(typedefFile, false))
            {
                file.WriteLine("using System;");
                file.WriteLine("");
                file.WriteLine("namespace DG.Tools.XrmMockup {");
                file.WriteLine("\tpublic struct SecurityRoles {");
                foreach (var securityRole in securityRoles.OrderBy(x => x.Value.Name))
                {
                    file.WriteLine($"\t\tpublic static Guid {ToSafeName(securityRole.Value.Name)} = new Guid(\"{securityRole.Key}\");");
                }
                file.WriteLine("\t}");
                file.WriteLine("}");
            }
        }

        private static bool StartsWithNumber(string str)
        {
            return str.Length > 0 && str[0] >= '0' && str[0] <= '9';
        }

        private static string ToSafeName(string str)
        {
            var compressed = Regex.Replace(str, @"[^\w]", "");
            if (StartsWithNumber(compressed))
            {
                return $"_{compressed}";
            }
            if (String.IsNullOrWhiteSpace(compressed))
            {
                return "_EmptyString";
            }
            return compressed;
        }
    }
}
