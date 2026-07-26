using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Annotations;
using Reseed.Utils;

namespace Reseed.Data.Providers.FileSystem
{
	public sealed class XmlValueContext
	{
		public readonly string FilePath;
		public readonly string EntityName;
		public readonly string PropertyName;

		public XmlValueContext(
			[NotNull] string filePath,
			[NotNull] string entityName,
			[NotNull] string propertyName)
		{
			this.FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
			this.EntityName = entityName ?? throw new ArgumentNullException(nameof(entityName));
			this.PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
		}
	}

	internal sealed class XmlDataProvider: IVerboseDataProvider
	{
		private readonly string dataFolder;
		private readonly string filePattern;
		private readonly Func<string, bool> fileFilter;
		private readonly IReadOnlyCollection<Func<XmlValueContext, string, string>> valueTransformers;

		public XmlDataProvider(
			[NotNull] string dataFolder,
			[NotNull] string filePattern,
			[NotNull] Func<string, bool> fileFilter)
			: this(
				dataFolder,
				filePattern,
				fileFilter,
				Array.Empty<Func<XmlValueContext, string, string>>())
		{
		}

		public XmlDataProvider(
			[NotNull] string dataFolder,
			[NotNull] string filePattern,
			[NotNull] Func<string, bool> fileFilter,
			[NotNull] IReadOnlyCollection<Func<XmlValueContext, string, string>> valueTransformers)
		{
			this.dataFolder = dataFolder ?? throw new ArgumentNullException(nameof(dataFolder));
			this.filePattern = filePattern ?? throw new ArgumentNullException(nameof(filePattern));
			this.fileFilter = fileFilter ?? throw new ArgumentNullException(nameof(fileFilter));
			this.valueTransformers = valueTransformers ??
				throw new ArgumentNullException(nameof(valueTransformers));
		}

		public VerboseDataProviderResult GetEntitiesDetailed()
		{
			if (!Directory.Exists(dataFolder))
			{
				throw new InvalidOperationException(
					$"Can't find xml data files at '{dataFolder}', specified directory doesn't exist");
			}

			var files = Directory
				.EnumerateFiles(dataFolder, filePattern, SearchOption.AllDirectories)
				.ToArray();

			if (!files.Any())
			{
				throw new InvalidOperationException(
					$"At least one xml data file is required, but found none at '{dataFolder}'. " +
					"Check that data files exist and aren't filtered out " +
					$"by either {nameof(filePattern)} or {nameof(fileFilter)}");
			}

			var (matchingFiles, skippedFiles) = files.PartitionBy(this.fileFilter);

			var entities = matchingFiles
				.SelectMany(ReadFile)
				.ToArray();

			return new VerboseDataProviderResult(
				entities,
				matchingFiles.Select(f => new DataFile(f)).ToArray(),
				skippedFiles.Select(f => new DataFile(f)).ToArray());
		}

		public IReadOnlyCollection<Entity> GetEntities() => 
			GetEntitiesDetailed().Entities;

		private Entity[] ReadFile([NotNull] string path)
		{
			var file = new DataFile(path);
			var rootElements = XDocument.Load(path)
				.Elements()
				.ToArray();

			if (rootElements.Length != 1)
			{
				throw BuildDocumentError(file,
					$"Expected to have the only root element but got {rootElements.Length} elements. " +
					"Make sure that you have valid xml data file. Empty data files aren't allowed");
			}

			var root = rootElements.First();
			var entityElements = root.Elements().ToArray();
			if (entityElements.Length == 0)
			{
				throw BuildDocumentError(file,
					"At least one xml element for entity is required, but found none. " +
					"Empty data files aren't allowed");
			}

			return entityElements
				.Select(e => ParseEntity(file, e))
				.ToArray();
		}

		private Entity ParseEntity(DataFile origin, XElement element)
		{
			AssertNoAttributes(origin, element);

			var entityName = element.Name.LocalName;
			var propertyElements = element.Elements().ToArray();
			if (propertyElements.Length == 0)
			{
				throw BuildDocumentError(
					origin,
					$"At least one xml element for entity attribute is required, but found none at '{element.Name}'. " +
					"Entities without attributes aren't allowed");
			}

			return new Entity(
				origin,
				entityName,
				propertyElements.Select(e => ParseProperty(origin, entityName, e)).ToArray());
		}

		private Property ParseProperty(DataFile origin, string entityName, XElement element)
		{
			AssertNoAttributes(origin, element);
			AssertNoDescendants(origin, element);
			var propertyName = element.Name.LocalName;

			if (this.valueTransformers.Count == 0)
				return new Property(propertyName, element.Value);

			var context = new XmlValueContext(
				origin.FilePath,
				entityName,
				propertyName);
			var transformedValue = this.valueTransformers.Aggregate(
				element.Value,
				(value, transformer) =>
				{
					var result = transformer(context, value);
					if (result == null)
					{
						throw BuildDocumentError(
							origin,
							"XML value transformer returned null for property element " +
							$"'{propertyName}' in entity element '{entityName}'");
					}

					return result;
				});

			return new Property(propertyName, transformedValue);
		}

		private static void AssertNoDescendants(DataFile dataFile, XElement element)
		{
			if (element.Descendants().Any())
			{
				throw BuildDocumentError(
					dataFile,
					"Expected to have flat document structure, " +
					$"but found descendant nodes at property element '{element.Name}'");
			}
		}

		private static void AssertNoAttributes(DataFile origin, XElement element)
		{
			if (element.Attributes().Any())
			{
				throw BuildDocumentError(
					origin,
					$"Attributes aren't supported, but are found at element '{element.Name}'");
			}
		}

		private static Exception BuildDocumentError(DataFile origin, string error) =>
			new InvalidOperationException(
				$"Can't process xml data file {origin}. " +
				error);
	}
}