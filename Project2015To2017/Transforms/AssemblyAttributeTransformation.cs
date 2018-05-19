using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Project2015To2017.Definition;

namespace Project2015To2017.Transforms
{
	public class AssemblyAttributeTransformation : ITransformation
	{
		public void Transform(Project definition, IProgress<string> progress)
		{
			if (definition.AssemblyAttributes == null)
			{
				return;
			}

			var attributeNodes = AssemblyAttributeNodes(
									definition.AssemblyAttributes,
									definition.PackageConfiguration,
									progress
								);

			definition.AssemblyAttributeProperties = definition.AssemblyAttributeProperties
															   .Concat(attributeNodes)
															   .ToList()
															   .AsReadOnly();

			if (definition.AssemblyAttributes.File != null && PointlessAssemblyInfo(definition.AssemblyAttributes))
			{
				definition.Deletions = definition
										.Deletions
										.Concat(new[] { definition.AssemblyAttributes.File })
										.ToList().AsReadOnly();

				if (AssemblyInfoFolderJustAssemblyInfo(definition.AssemblyAttributes))
				{
					definition.Deletions = definition
						.Deletions
						.Concat(new[] { definition.AssemblyAttributes.File.Directory })
						.ToList().AsReadOnly();
				}
			}
		}

		private bool AssemblyInfoFolderJustAssemblyInfo(AssemblyAttributes assemblyAttributes)
		{
			//Look if only the assembly info file is in the directory
			return assemblyAttributes.File.Directory.EnumerateFileSystemInfos().Count() <= 1;
		}

		private bool PointlessAssemblyInfo(AssemblyAttributes assemblyAttributes)
		{
			var file = assemblyAttributes.FileContents;

			return !file.Members.Any() && !file.AttributeLists.Any();
		}

		private static XElement[] AssemblyAttributeNodes(
									AssemblyAttributes assemblyAttributes,
									PackageConfiguration packageConfig,
									IProgress<string> progress
								 )
		{
			

			progress.Report("Moving attributes from AssemblyInfo to project file");

			var versioningProperties = VersioningProperties(assemblyAttributes, packageConfig, progress);
			var otherProperties = OtherProperties(assemblyAttributes, packageConfig, progress);

			var childNodes = otherProperties
								.Concat(versioningProperties)
								.ToArray();

			if (childNodes.Length == 0)
			{
				//Assume that the assembly info is coming from another file
				//which we don't have sight of so leave it up to consumer to
				//convert over if they wish
				return new[] { new XElement("GenerateAssemblyInfo", "false") };
			}
			else
			{
				return childNodes;
			}
		}

		private static IEnumerable<XElement> OtherProperties(AssemblyAttributes assemblyAttributes,
			PackageConfiguration packageConfig, IProgress<string> progress)
		{
			var toReturn = Properties()
								.Where(x => x != null)
								.ToList();

			assemblyAttributes.Title = null;
			assemblyAttributes.Company = null;
			assemblyAttributes.Description = null;
			assemblyAttributes.Product = null;
			assemblyAttributes.Copyright = null;

			return toReturn;

			IEnumerable<XElement> Properties()
			{
				yield return XElement(assemblyAttributes.Title, "AssemblyTitle");
				yield return XElement(assemblyAttributes.Company, "Company");
				yield return XElement(assemblyAttributes.Product, "Product");

				//And a couple of properties which can be superceded by the package config
				yield return XElement(assemblyAttributes.Description, packageConfig?.Description, "Description", progress);
				yield return XElement(assemblyAttributes.Copyright, packageConfig?.Copyright, "Copyright", progress);

				if (assemblyAttributes.Configuration != null)
				{
					//If it is included, chances are that the developer has used
					//preprocessor flags which we can't yet process
					//so just leave it in AssemblyInfo file
					yield return new XElement("GenerateAssemblyConfigurationAttribute", false);
				}
			}
		}

		private static XElement XElement(string assemblyInfoValue, string packageConfigValue,
											string description, IProgress<string> progress)
		{
			if (packageConfigValue != null && packageConfigValue != assemblyInfoValue)
			{
				if (assemblyInfoValue != null)
				{
					progress.Report(
						$"Taking nuspec {description} property value {packageConfigValue} " +
						$"over AssemblyInfo value {assemblyInfoValue}");
				}

				return XElement(packageConfigValue, description);
			}
			else
			{
				return assemblyInfoValue == null ? null : XElement(assemblyInfoValue, description);
			}
		}

		private static List<XElement> VersioningProperties(AssemblyAttributes assemblyAttributes,
			PackageConfiguration packageConfig, IProgress<string> progress)
		{
			var toReturn = Properties()
								.Where(x => x != null)
								.ToList();

			assemblyAttributes.InformationalVersion = null;
			assemblyAttributes.Version = null;
			assemblyAttributes.FileVersion = null;

			return toReturn;

			IEnumerable<XElement> Properties()
			{
				yield return XElement(assemblyAttributes.InformationalVersion, packageConfig?.Version, "Version", progress);
				yield return XElement(assemblyAttributes.Version, "AssemblyVersion");

				//The AssemblyInfo behaviour was to fallback on the AssemblyVersion for the file version
				//but in the new format, this doesn't happen so we explicitly copy the value across
				yield return XElement(assemblyAttributes.FileVersion, "FileVersion") ??
							 XElement(assemblyAttributes.Version, "FileVersion");
			}
		}

		private static XElement XElement(string attribute, string name)
		{
			return attribute != null ? new XElement(name, attribute) : null;
		}
	}
}